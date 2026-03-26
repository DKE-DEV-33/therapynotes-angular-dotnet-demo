using Microsoft.EntityFrameworkCore;
using TherapyNotesDemo.Scheduling.Api.Domain;

namespace TherapyNotesDemo.Scheduling.Api.Infrastructure;

public sealed class ClientStore
{
    private readonly SchedulingDbContext _db;

    public ClientStore(SchedulingDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<Client>> GetAllAsync(CancellationToken cancellationToken)
    {
        return await _db.Clients
            .OrderByDescending(c => c.CreatedAt)
            .Select(c => new Client(c.Id, c.DisplayName, c.CreatedAt))
            .ToListAsync(cancellationToken);
    }

    public async Task<Client> AddAsync(string displayName, CancellationToken cancellationToken)
    {
        var row = new ClientRow
        {
            Id = Guid.NewGuid(),
            DisplayName = displayName.Trim(),
            CreatedAt = DateTimeOffset.UtcNow
        };

        _db.Clients.Add(row);
        await _db.SaveChangesAsync(cancellationToken);

        return new Client(row.Id, row.DisplayName, row.CreatedAt);
    }

    public async Task<Client?> GetAsync(Guid clientId, CancellationToken cancellationToken)
    {
        var row = await _db.Clients.FirstOrDefaultAsync(c => c.Id == clientId, cancellationToken);
        return row is null ? null : new Client(row.Id, row.DisplayName, row.CreatedAt);
    }

    public async Task EnsureSeededAsync(CancellationToken cancellationToken)
    {
        if (await _db.Clients.AnyAsync(cancellationToken))
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        _db.Clients.AddRange(
            new ClientRow { Id = Guid.NewGuid(), DisplayName = "Alex Morgan", CreatedAt = now.AddMinutes(-30) },
            new ClientRow { Id = Guid.NewGuid(), DisplayName = "Taylor Kim", CreatedAt = now.AddMinutes(-25) },
            new ClientRow { Id = Guid.NewGuid(), DisplayName = "Jordan Patel", CreatedAt = now.AddMinutes(-20) }
        );

        await _db.SaveChangesAsync(cancellationToken);
    }
}

public sealed class AppointmentStore
{
    private readonly SchedulingDbContext _db;

    public AppointmentStore(SchedulingDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<Appointment>> GetAllAsync(DateTimeOffset? from, DateTimeOffset? to, CancellationToken cancellationToken)
    {
        IQueryable<AppointmentRow> query = _db.Appointments;
        if (from is not null) query = query.Where(a => a.StartsAt >= from.Value);
        if (to is not null) query = query.Where(a => a.StartsAt <= to.Value);

        return await query
            .OrderBy(a => a.StartsAt)
            .Select(a => new Appointment(a.Id, a.ClientId, a.StartsAt, a.DurationMinutes, a.CreatedAt))
            .ToListAsync(cancellationToken);
    }

    public async Task<Appointment> AddAsync(Guid clientId, DateTimeOffset startsAt, int durationMinutes, CancellationToken cancellationToken)
    {
        var row = new AppointmentRow
        {
            Id = Guid.NewGuid(),
            ClientId = clientId,
            StartsAt = startsAt,
            DurationMinutes = durationMinutes,
            CreatedAt = DateTimeOffset.UtcNow
        };

        _db.Appointments.Add(row);
        await _db.SaveChangesAsync(cancellationToken);

        return new Appointment(row.Id, row.ClientId, row.StartsAt, row.DurationMinutes, row.CreatedAt);
    }
}
