using System.Text;

namespace TherapyNotesDemo.Notifications.Worker;

public sealed class NotificationLog
{
    private readonly string _path;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public NotificationLog(IHostEnvironment env)
    {
        _path = Path.Combine(env.ContentRootPath, "data", "notifications.log");
    }

    public async Task AppendAsync(string line, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);

        await _gate.WaitAsync(cancellationToken);
        try
        {
            await using var stream = new FileStream(
                _path,
                FileMode.Append,
                FileAccess.Write,
                FileShare.Read,
                bufferSize: 8 * 1024,
                useAsync: true);

            var bytes = Encoding.UTF8.GetBytes($"{DateTimeOffset.UtcNow:O} {line}\n");
            await stream.WriteAsync(bytes, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }
}

