using TherapyNotesDemo.Scheduling.Api.Domain;

namespace TherapyNotesDemo.Scheduling.Api.Infrastructure;

public sealed class ClientStore
{
    private readonly object _gate = new();
    private readonly List<Client> _clients = new();

    public ClientStore()
    {
        var now = DateTimeOffset.UtcNow;
        _clients.Add(new Client(Guid.NewGuid(), "Alex Morgan", now.AddMinutes(-30)));
        _clients.Add(new Client(Guid.NewGuid(), "Taylor Kim", now.AddMinutes(-25)));
        _clients.Add(new Client(Guid.NewGuid(), "Jordan Patel", now.AddMinutes(-20)));
    }

    public IReadOnlyList<Client> GetAll()
    {
        lock (_gate)
        {
            return _clients
                .OrderByDescending(c => c.CreatedAt)
                .ToList();
        }
    }

    public Client Add(string displayName)
    {
        var client = new Client(Guid.NewGuid(), displayName.Trim(), DateTimeOffset.UtcNow);
        lock (_gate)
        {
            _clients.Add(client);
        }
        return client;
    }

    public Client? Get(Guid clientId)
    {
        lock (_gate)
        {
            return _clients.FirstOrDefault(c => c.Id == clientId);
        }
    }
}

public sealed class AppointmentStore
{
    private readonly object _gate = new();
    private readonly List<Appointment> _appointments = new();

    public IReadOnlyList<Appointment> GetAll(DateTimeOffset? from = null, DateTimeOffset? to = null)
    {
        lock (_gate)
        {
            IEnumerable<Appointment> query = _appointments;
            if (from is not null) query = query.Where(a => a.StartsAt >= from.Value);
            if (to is not null) query = query.Where(a => a.StartsAt <= to.Value);
            return query
                .OrderBy(a => a.StartsAt)
                .ToList();
        }
    }

    public Appointment Add(Guid clientId, DateTimeOffset startsAt, int durationMinutes)
    {
        var appointment = new Appointment(Guid.NewGuid(), clientId, startsAt, durationMinutes, DateTimeOffset.UtcNow);
        lock (_gate)
        {
            _appointments.Add(appointment);
        }
        return appointment;
    }
}

