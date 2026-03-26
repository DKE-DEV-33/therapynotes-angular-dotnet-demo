using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace TherapyNotesDemo.Notifications.Worker;

public sealed record ClaimedOutboxMessageDto(
    Guid Id,
    DateTimeOffset OccurredAt,
    string Type,
    JsonElement Data,
    DateTimeOffset ClaimedUntil
);

public sealed class SchedulingApiOptions
{
    public string BaseUrl { get; init; } = "http://localhost:5080";
    public string ApiKey { get; init; } = "dev-worker-key";
    public int PollSeconds { get; init; } = 2;
    public int LeaseSeconds { get; init; } = 30;
    public int MaxBatchSize { get; init; } = 25;
}

public sealed class SchedulingOutboxClient
{
    private readonly HttpClient _http;
    private readonly IOptionsMonitor<SchedulingApiOptions> _options;

    public SchedulingOutboxClient(HttpClient http, IOptionsMonitor<SchedulingApiOptions> options)
    {
        _http = http;
        _options = options;
    }

    public async Task<IReadOnlyList<ClaimedOutboxMessageDto>> ClaimAsync(int max, int leaseSeconds, CancellationToken cancellationToken)
    {
        var opts = _options.CurrentValue;
        var url = $"{opts.BaseUrl.TrimEnd('/')}/internal/outbox/claim";

        using var req = new HttpRequestMessage(HttpMethod.Post, url);
        req.Headers.TryAddWithoutValidation("X-Internal-Api-Key", opts.ApiKey);
        req.Content = JsonContent.Create(new { Max = max, LeaseSeconds = leaseSeconds });

        using var resp = await _http.SendAsync(req, cancellationToken);

        resp.EnsureSuccessStatusCode();

        var messages = await resp.Content.ReadFromJsonAsync<List<ClaimedOutboxMessageDto>>(cancellationToken: cancellationToken);
        return messages ?? [];
    }

    public async Task CompleteAsync(IReadOnlyList<Guid> ids, CancellationToken cancellationToken)
    {
        if (ids.Count == 0) return;

        var opts = _options.CurrentValue;
        var url = $"{opts.BaseUrl.TrimEnd('/')}/internal/outbox/complete";

        using var req = new HttpRequestMessage(HttpMethod.Post, url);
        req.Headers.TryAddWithoutValidation("X-Internal-Api-Key", opts.ApiKey);
        req.Content = JsonContent.Create(new { MessageIds = ids });

        using var resp = await _http.SendAsync(req, cancellationToken);
        resp.EnsureSuccessStatusCode();
    }
}
