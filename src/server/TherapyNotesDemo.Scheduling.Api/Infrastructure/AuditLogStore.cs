using TherapyNotesDemo.Scheduling.Api.Domain;

namespace TherapyNotesDemo.Scheduling.Api.Infrastructure;

public sealed class AuditLogStore
{
    private readonly string _path;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public AuditLogStore(IWebHostEnvironment env)
    {
        _path = Path.Combine(env.ContentRootPath, "data", "audit.jsonl");
    }

    public async Task AppendAsync(AuditEntry entry, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            await JsonlFile.AppendAsync(_path, entry, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<IReadOnlyList<AuditEntry>> GetAllAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var entries = await JsonlFile.ReadAllAsync<AuditEntry>(_path, cancellationToken);
            return entries
                .OrderByDescending(e => e.OccurredAt)
                .ToList();
        }
        finally
        {
            _gate.Release();
        }
    }
}

