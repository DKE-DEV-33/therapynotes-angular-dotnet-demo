using System.Text;
using System.Text.Json;

namespace TherapyNotesDemo.Scheduling.Api.Infrastructure;

public static class JsonlFile
{
    public static async Task AppendAsync<T>(string path, T value, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        var json = JsonSerializer.Serialize(value);
        var line = json + "\n";
        var bytes = Encoding.UTF8.GetBytes(line);

        await using var stream = new FileStream(
            path,
            FileMode.Append,
            FileAccess.Write,
            FileShare.Read,
            bufferSize: 16 * 1024,
            useAsync: true);

        await stream.WriteAsync(bytes, cancellationToken);
    }

    public static async Task<IReadOnlyList<T>> ReadAllAsync<T>(string path, CancellationToken cancellationToken)
    {
        if (!File.Exists(path))
        {
            return Array.Empty<T>();
        }

        var lines = await File.ReadAllLinesAsync(path, cancellationToken);
        var results = new List<T>(lines.Length);

        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            results.Add(JsonSerializer.Deserialize<T>(line)!);
        }

        return results;
    }
}

