using System.Text.Json;

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
    private sealed record OutboxState(
        Dictionary<Guid, OutboxStateEntry> Entries
    );

    private sealed record OutboxStateEntry(
        string Status,
        DateTimeOffset? ClaimedUntil
    );

    private readonly string _messagesPath;
    private readonly string _statePath;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public OutboxStore(IWebHostEnvironment env)
    {
        var root = Path.Combine(env.ContentRootPath, "data");
        _messagesPath = Path.Combine(root, "outbox.jsonl");
        _statePath = Path.Combine(root, "outbox-state.json");
    }

    public async Task EnqueueAsync(OutboxMessage message, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            await JsonlFile.AppendAsync(_messagesPath, message, cancellationToken);
            var state = await LoadStateAsync(cancellationToken);
            state.Entries[message.Id] = new OutboxStateEntry("Pending", null);
            await SaveStateAsync(state, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<IReadOnlyList<ClaimedOutboxMessage>> ClaimAsync(int max, TimeSpan leaseTime, CancellationToken cancellationToken)
    {
        if (max <= 0) return Array.Empty<ClaimedOutboxMessage>();

        await _gate.WaitAsync(cancellationToken);
        try
        {
            var now = DateTimeOffset.UtcNow;
            var messages = await JsonlFile.ReadAllAsync<OutboxMessage>(_messagesPath, cancellationToken);
            var state = await LoadStateAsync(cancellationToken);

            // Release expired claims.
            foreach (var (id, entry) in state.Entries.ToList())
            {
                if (entry.Status == "Claimed" && entry.ClaimedUntil is not null && entry.ClaimedUntil <= now)
                {
                    state.Entries[id] = new OutboxStateEntry("Pending", null);
                }
            }

            var pending = messages
                .Where(m => state.Entries.TryGetValue(m.Id, out var entry) && entry.Status == "Pending")
                .OrderBy(m => m.OccurredAt)
                .Take(max)
                .ToList();

            var claimedUntil = now.Add(leaseTime);
            var claimed = new List<ClaimedOutboxMessage>(pending.Count);

            foreach (var msg in pending)
            {
                state.Entries[msg.Id] = new OutboxStateEntry("Claimed", claimedUntil);
                claimed.Add(new ClaimedOutboxMessage(msg, claimedUntil));
            }

            if (claimed.Count > 0)
            {
                await SaveStateAsync(state, cancellationToken);
            }

            return claimed;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task CompleteAsync(IEnumerable<Guid> messageIds, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var state = await LoadStateAsync(cancellationToken);
            foreach (var id in messageIds)
            {
                if (!state.Entries.ContainsKey(id)) continue;
                state.Entries[id] = new OutboxStateEntry("Completed", null);
            }
            await SaveStateAsync(state, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<OutboxState> LoadStateAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_statePath))
        {
            return new OutboxState(new Dictionary<Guid, OutboxStateEntry>());
        }

        var json = await File.ReadAllTextAsync(_statePath, cancellationToken);
        return JsonSerializer.Deserialize<OutboxState>(json)!;
    }

    private async Task SaveStateAsync(OutboxState state, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_statePath)!);
        var json = JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(_statePath, json, cancellationToken);
    }
}

