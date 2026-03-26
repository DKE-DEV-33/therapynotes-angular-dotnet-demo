using System.Text.Json;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using TherapyNotesDemo.Contracts;
using TherapyNotesDemo.Scheduling.Api.Domain;
using TherapyNotesDemo.Scheduling.Api.Eventing;
using TherapyNotesDemo.Scheduling.Api.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCors(options =>
{
    options.AddPolicy("client", policy =>
    {
        policy
            .WithOrigins("http://localhost:4200")
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

var dataDir = Path.Combine(builder.Environment.ContentRootPath, "data");
Directory.CreateDirectory(dataDir);
var dbPath = Path.Combine(dataDir, "scheduling.db");

builder.Services.AddDbContext<SchedulingDbContext>(options =>
{
    options.UseSqlite($"Data Source={dbPath}");
});

builder.Services.AddScoped<ClientStore>();
builder.Services.AddScoped<AppointmentStore>();
builder.Services.AddScoped<AuditLogStore>();
builder.Services.AddScoped<OutboxStore>();

builder.Services.AddSingleton<IDomainEventBus, DomainEventBus>();
builder.Services.AddScoped<IDomainEventHandler, AuditLogDomainEventHandler>();
builder.Services.AddHostedService<DomainEventDispatcherHostedService>();

var app = builder.Build();

// Create database and seed demo data on startup.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<SchedulingDbContext>();
    const int schemaVersion = 2;

    // Simple demo-friendly schema versioning: if schema changes, recreate the DB.
    // This keeps local setup smooth without migrations.
    db.Database.EnsureCreated();
    var conn = db.Database.GetDbConnection();
    conn.Open();
    try
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "CREATE TABLE IF NOT EXISTS __schema (key TEXT PRIMARY KEY, value TEXT NOT NULL);";
        cmd.ExecuteNonQuery();

        cmd.CommandText = "SELECT value FROM __schema WHERE key = 'version' LIMIT 1;";
        var existing = cmd.ExecuteScalar() as string;
        if (!int.TryParse(existing, out var existingVersion) || existingVersion != schemaVersion)
        {
            conn.Close();
            db.Database.EnsureDeleted();
            db.Database.EnsureCreated();

            var conn2 = db.Database.GetDbConnection();
            conn2.Open();
            using var cmd2 = conn2.CreateCommand();
            cmd2.CommandText = "CREATE TABLE IF NOT EXISTS __schema (key TEXT PRIMARY KEY, value TEXT NOT NULL);";
            cmd2.ExecuteNonQuery();
            cmd2.CommandText = "INSERT OR REPLACE INTO __schema (key, value) VALUES ('version', $v);";
            var p = cmd2.CreateParameter();
            p.ParameterName = "$v";
            p.Value = schemaVersion.ToString();
            cmd2.Parameters.Add(p);
            cmd2.ExecuteNonQuery();
            conn2.Close();
        }
    }
    finally
    {
        if (conn.State == System.Data.ConnectionState.Open)
        {
            conn.Close();
        }
    }

    var clients = scope.ServiceProvider.GetRequiredService<ClientStore>();
    clients.EnsureSeededAsync(CancellationToken.None).GetAwaiter().GetResult();
}

app.UseCors("client");
app.UseHttpsRedirection();

var api = app.MapGroup("/api");

api.MapGet("/clients", async (ClientStore clients, CancellationToken cancellationToken) =>
{
    return Results.Ok(await clients.GetAllAsync(cancellationToken));
});

api.MapPost("/clients", async Task<Results<Ok<Client>, BadRequest<string>>>(
    CreateClientRequest request,
    ClientStore clients,
    IDomainEventBus bus,
    OutboxStore outbox,
    CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(request.DisplayName))
    {
        return TypedResults.BadRequest("DisplayName is required.");
    }

    var client = await clients.AddAsync(request.DisplayName, cancellationToken);

    var domainEvent = new ClientCreatedDomainEvent(
        Guid.NewGuid(),
        DateTimeOffset.UtcNow,
        client.Id,
        client.DisplayName
    );

    await bus.PublishAsync(domainEvent, cancellationToken);

    var integrationEvent = new ClientCreatedIntegrationEvent(
        Guid.NewGuid(),
        DateTimeOffset.UtcNow,
        client.Id,
        client.DisplayName
    );

    await outbox.EnqueueAsync(
        new OutboxMessage(
            integrationEvent.Id,
            integrationEvent.OccurredAt,
            integrationEvent.Type,
            JsonSerializer.SerializeToElement(integrationEvent)
        ),
        cancellationToken);

    return TypedResults.Ok(client);
});

api.MapGet("/appointments", async (
    DateTimeOffset? from,
    DateTimeOffset? to,
    AppointmentStore appointments,
    CancellationToken cancellationToken) =>
{
    return Results.Ok(await appointments.GetAllAsync(from, to, cancellationToken));
});

api.MapPost("/appointments", async Task<Results<Ok<Appointment>, BadRequest<string>>>(
    CreateAppointmentRequest request,
    ClientStore clients,
    AppointmentStore appointments,
    IDomainEventBus bus,
    OutboxStore outbox,
    CancellationToken cancellationToken) =>
{
    if (request.ClientId == Guid.Empty)
    {
        return TypedResults.BadRequest("ClientId is required.");
    }

    if (await clients.GetAsync(request.ClientId, cancellationToken) is null)
    {
        return TypedResults.BadRequest("ClientId is unknown.");
    }

    if (request.DurationMinutes is < 15 or > 240)
    {
        return TypedResults.BadRequest("DurationMinutes must be between 15 and 240.");
    }

    if (request.StartsAt == default)
    {
        return TypedResults.BadRequest("StartsAt is required.");
    }

    var appointment = await appointments.AddAsync(request.ClientId, request.StartsAt, request.DurationMinutes, cancellationToken);

    var domainEvent = new AppointmentScheduledDomainEvent(
        Guid.NewGuid(),
        DateTimeOffset.UtcNow,
        appointment.Id,
        appointment.ClientId,
        appointment.StartsAt,
        appointment.DurationMinutes
    );

    await bus.PublishAsync(domainEvent, cancellationToken);

    var integrationEvent = new AppointmentScheduledIntegrationEvent(
        Guid.NewGuid(),
        DateTimeOffset.UtcNow,
        appointment.Id,
        appointment.ClientId,
        appointment.StartsAt,
        appointment.DurationMinutes
    );

    await outbox.EnqueueAsync(
        new OutboxMessage(
            integrationEvent.Id,
            integrationEvent.OccurredAt,
            integrationEvent.Type,
            JsonSerializer.SerializeToElement(integrationEvent)
        ),
        cancellationToken);

    return TypedResults.Ok(appointment);
});

api.MapGet("/audit", async (AuditLogStore audit, CancellationToken cancellationToken) =>
{
    return Results.Ok(await audit.GetAllAsync(cancellationToken));
});

// Minimal outbox API for the worker service. In a real system this would be protected.
var internalApi = app.MapGroup("/internal");
internalApi.AddEndpointFilter(async (context, next) =>
{
    var configured = builder.Configuration["InternalApi:ApiKey"];
    if (string.IsNullOrWhiteSpace(configured))
    {
        return Results.Problem("Internal API key is not configured.", statusCode: 500);
    }

    var provided = context.HttpContext.Request.Headers["X-Internal-Api-Key"].ToString();
    if (!string.Equals(configured, provided, StringComparison.Ordinal))
    {
        return Results.Unauthorized();
    }

    return await next(context);
});

internalApi.MapPost("/outbox/claim", async (
    ClaimOutboxRequest request,
    OutboxStore outbox,
    CancellationToken cancellationToken) =>
{
    var max = request.Max is > 0 and <= 100 ? request.Max : 25;
    var leaseSeconds = request.LeaseSeconds is >= 5 and <= 300 ? request.LeaseSeconds : 30;

    var claimed = await outbox.ClaimAsync(max, TimeSpan.FromSeconds(leaseSeconds), cancellationToken);

    return Results.Ok(claimed.Select(c => new
    {
        c.Message.Id,
        c.Message.OccurredAt,
        c.Message.Type,
        Data = c.Message.Data,
        c.ClaimedUntil
    }));
});

internalApi.MapPost("/outbox/complete", async (
    CompleteOutboxRequest request,
    OutboxStore outbox,
    CancellationToken cancellationToken) =>
{
    var ids = request.MessageIds
        .Where(id => id != Guid.Empty)
        .Distinct()
        .ToList();

    await outbox.CompleteAsync(ids, cancellationToken);
    return Results.NoContent();
});

app.MapGet("/", () =>
{
    return Results.Ok(new
    {
        service = "TherapyNotesDemo.Scheduling.Api",
        status = "ok",
        endpoints = new[]
        {
            "GET /health",
            "GET /api/clients",
            "POST /api/clients",
            "GET /api/appointments",
            "POST /api/appointments",
            "GET /api/audit",
            "POST /internal/outbox/claim",
            "POST /internal/outbox/complete"
        }
    });
});

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.Run();

sealed record CreateClientRequest(string DisplayName);
sealed record CreateAppointmentRequest(Guid ClientId, DateTimeOffset StartsAt, int DurationMinutes);
sealed record ClaimOutboxRequest(int Max, int LeaseSeconds);
sealed record CompleteOutboxRequest(IReadOnlyList<Guid> MessageIds);
