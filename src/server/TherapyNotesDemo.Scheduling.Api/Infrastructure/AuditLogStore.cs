using Microsoft.EntityFrameworkCore;
using TherapyNotesDemo.Scheduling.Api.Domain;

namespace TherapyNotesDemo.Scheduling.Api.Infrastructure;

public sealed class AuditLogStore
{
    private readonly SchedulingDbContext _db;

    public AuditLogStore(SchedulingDbContext db)
    {
        _db = db;
    }

    public async Task AppendAsync(AuditEntry entry, CancellationToken cancellationToken)
    {
        _db.AuditEntries.Add(new AuditEntryRow
        {
            Id = entry.Id,
            OccurredAtUtc = entry.OccurredAt.UtcDateTime,
            EventType = entry.EventType,
            Message = entry.Message
        });

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AuditEntry>> GetAllAsync(CancellationToken cancellationToken)
    {
        return await _db.AuditEntries
            .OrderByDescending(e => e.OccurredAtUtc)
            .Select(e => new AuditEntry(
                e.Id,
                new DateTimeOffset(DateTime.SpecifyKind(e.OccurredAtUtc, DateTimeKind.Utc)),
                e.EventType,
                e.Message))
            .ToListAsync(cancellationToken);
    }
}
