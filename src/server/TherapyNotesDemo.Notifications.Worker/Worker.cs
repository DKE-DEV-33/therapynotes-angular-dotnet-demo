using Microsoft.Extensions.Options;
using System.Text.Json;
using TherapyNotesDemo.Contracts;

namespace TherapyNotesDemo.Notifications.Worker;

public sealed class Worker : BackgroundService
{
    private readonly SchedulingOutboxClient _outbox;
    private readonly NotificationLog _log;
    private readonly IdempotencyStore _idempotency;
    private readonly IOptionsMonitor<SchedulingApiOptions> _options;
    private readonly ILogger<Worker> _logger;

    public Worker(
        SchedulingOutboxClient outbox,
        NotificationLog log,
        IdempotencyStore idempotency,
        IOptionsMonitor<SchedulingApiOptions> options,
        ILogger<Worker> logger)
    {
        _outbox = outbox;
        _log = log;
        _idempotency = idempotency;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Notifications worker started");

        while (!stoppingToken.IsCancellationRequested)
        {
            var opts = _options.CurrentValue;

            try
            {
                var claimed = await _outbox.ClaimAsync(opts.MaxBatchSize, opts.LeaseSeconds, stoppingToken);
                if (claimed.Count == 0)
                {
                    await Task.Delay(TimeSpan.FromSeconds(opts.PollSeconds), stoppingToken);
                    continue;
                }

                var completed = new List<Guid>(claimed.Count);

                foreach (var msg in claimed)
                {
                    var handled = await HandleAsync(msg, stoppingToken);
                    if (handled)
                    {
                        completed.Add(msg.Id);
                    }
                }

                if (completed.Count > 0)
                {
                    await _outbox.CompleteAsync(completed, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // graceful shutdown
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Worker loop failed; retrying");
                await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
            }
        }

        _logger.LogInformation("Notifications worker stopped");
    }

    private async Task<bool> HandleAsync(ClaimedOutboxMessageDto msg, CancellationToken cancellationToken)
    {
        if (await _idempotency.IsProcessedAsync(msg.Id, cancellationToken))
        {
            _logger.LogInformation("Skipping already-processed message {EventType} ({EventId})", msg.Type, msg.Id);
            return true;
        }

        if (msg.Type == nameof(AppointmentScheduledIntegrationEvent))
        {
            var e = JsonSerializer.Deserialize<AppointmentScheduledIntegrationEvent>(msg.Data.GetRawText());
            if (e is null) return false;

            await _log.AppendAsync(
                $"[Email] Appointment scheduled for client {e.ClientId} at {e.StartsAt:O} (duration {e.DurationMinutes}m)",
                cancellationToken);

            await _idempotency.MarkProcessedAsync(msg.Id, msg.Type, msg.OccurredAt, cancellationToken);
            _logger.LogInformation("Processed {EventType} ({EventId})", msg.Type, msg.Id);
            return true;
        }

        if (msg.Type == nameof(ClientCreatedIntegrationEvent))
        {
            var e = JsonSerializer.Deserialize<ClientCreatedIntegrationEvent>(msg.Data.GetRawText());
            if (e is null) return false;

            await _log.AppendAsync(
                $"[Email] Welcome email queued for new client {e.ClientDisplayName} ({e.ClientId})",
                cancellationToken);

            await _idempotency.MarkProcessedAsync(msg.Id, msg.Type, msg.OccurredAt, cancellationToken);
            _logger.LogInformation("Processed {EventType} ({EventId})", msg.Type, msg.Id);
            return true;
        }

        _logger.LogWarning("Skipping unknown event type: {EventType} ({EventId})", msg.Type, msg.Id);
        await _idempotency.MarkProcessedAsync(msg.Id, msg.Type, msg.OccurredAt, cancellationToken);
        return true;
    }
}
