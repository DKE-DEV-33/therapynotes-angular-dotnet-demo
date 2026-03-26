using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace TherapyNotesDemo.Scheduling.Api.Infrastructure;

public sealed record OutboxMessage(
    Guid Id,
    DateTimeOffset OccurredAt,
    string Type,
    JsonElement Data
);

public sealed record ClaimedOutboxMessage(
    OutboxMessage Message,
    DateTimeOffset ClaimedUntil
);

public sealed class OutboxStore
{
    private readonly SchedulingDbContext _db;

    public OutboxStore(SchedulingDbContext db)
    {
        _db = db;
    }

    public async Task EnqueueAsync(OutboxMessage message, CancellationToken cancellationToken)
    {
        _db.OutboxMessages.Add(new OutboxMessageRow
        {
            Id = message.Id,
            OccurredAtUtc = message.OccurredAt.UtcDateTime,
            Type = message.Type,
            DataJson = message.Data.GetRawText(),
            Status = "Pending",
            ClaimedUntilUtc = null,
            CompletedAtUtc = null
        });

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ClaimedOutboxMessage>> ClaimAsync(int max, TimeSpan leaseTime, CancellationToken cancellationToken)
    {
        if (max <= 0) return Array.Empty<ClaimedOutboxMessage>();

        var nowUtc = DateTime.UtcNow;
        var claimedUntilUtc = nowUtc.Add(leaseTime);

        await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);

        var rows = await _db.OutboxMessages
            .Where(m =>
                m.Status == "Pending" ||
                (m.Status == "Claimed" && m.ClaimedUntilUtc != null && m.ClaimedUntilUtc <= nowUtc))
            .OrderBy(m => m.OccurredAtUtc)
            .Take(max)
            .ToListAsync(cancellationToken);

        foreach (var row in rows)
        {
            row.Status = "Claimed";
            row.ClaimedUntilUtc = claimedUntilUtc;
        }

        if (rows.Count > 0)
        {
            await _db.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);
        }

        var claimed = new List<ClaimedOutboxMessage>(rows.Count);
        foreach (var row in rows)
        {
            var json = JsonDocument.Parse(row.DataJson);
            var element = json.RootElement.Clone();
            json.Dispose();

            claimed.Add(new ClaimedOutboxMessage(
                new OutboxMessage(row.Id, new DateTimeOffset(row.OccurredAtUtc, TimeSpan.Zero), row.Type, element),
                new DateTimeOffset(claimedUntilUtc, TimeSpan.Zero)));
        }

        return claimed;
    }

    public async Task CompleteAsync(IEnumerable<Guid> messageIds, CancellationToken cancellationToken)
    {
        var ids = messageIds.Distinct().ToList();
        if (ids.Count == 0) return;

        var nowUtc = DateTime.UtcNow;

        var rows = await _db.OutboxMessages
            .Where(m => ids.Contains(m.Id))
            .ToListAsync(cancellationToken);

        foreach (var row in rows)
        {
            row.Status = "Completed";
            row.CompletedAtUtc = nowUtc;
            row.ClaimedUntilUtc = null;
        }

        await _db.SaveChangesAsync(cancellationToken);
    }
}
