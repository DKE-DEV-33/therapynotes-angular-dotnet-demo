using System.Text.Json;
using Microsoft.AspNetCore.Http.HttpResults;
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

builder.Services.AddSingleton<ClientStore>();
builder.Services.AddSingleton<AppointmentStore>();
builder.Services.AddSingleton<AuditLogStore>();
builder.Services.AddSingleton<OutboxStore>();

builder.Services.AddSingleton<IDomainEventBus, DomainEventBus>();
builder.Services.AddSingleton<IDomainEventHandler, AuditLogDomainEventHandler>();
builder.Services.AddHostedService<DomainEventDispatcherHostedService>();

var app = builder.Build();

app.UseCors("client");
app.UseHttpsRedirection();

var api = app.MapGroup("/api");

api.MapGet("/clients", (ClientStore clients) => Results.Ok(clients.GetAll()));

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

    var client = clients.Add(request.DisplayName);

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

api.MapGet("/appointments", (
    DateTimeOffset? from,
    DateTimeOffset? to,
    AppointmentStore appointments) =>
{
    return Results.Ok(appointments.GetAll(from, to));
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

    if (clients.Get(request.ClientId) is null)
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

    var appointment = appointments.Add(request.ClientId, request.StartsAt, request.DurationMinutes);

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
