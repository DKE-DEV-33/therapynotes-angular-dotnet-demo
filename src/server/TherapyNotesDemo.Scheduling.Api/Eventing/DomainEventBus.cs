using System.Threading.Channels;
using TherapyNotesDemo.Scheduling.Api.Domain;

namespace TherapyNotesDemo.Scheduling.Api.Eventing;

public interface IDomainEventBus
{
    ValueTask PublishAsync(IDomainEvent domainEvent, CancellationToken cancellationToken);
    IAsyncEnumerable<IDomainEvent> ReadAllAsync(CancellationToken cancellationToken);
}

public sealed class DomainEventBus : IDomainEventBus
{
    private readonly Channel<IDomainEvent> _channel = Channel.CreateUnbounded<IDomainEvent>(
        new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });

    public ValueTask PublishAsync(IDomainEvent domainEvent, CancellationToken cancellationToken)
        => _channel.Writer.WriteAsync(domainEvent, cancellationToken);

    public IAsyncEnumerable<IDomainEvent> ReadAllAsync(CancellationToken cancellationToken)
        => _channel.Reader.ReadAllAsync(cancellationToken);
}

public interface IDomainEventHandler
{
    bool CanHandle(string eventType);
    Task HandleAsync(IDomainEvent domainEvent, CancellationToken cancellationToken);
}

public sealed class DomainEventDispatcherHostedService : BackgroundService
{
    private readonly IDomainEventBus _bus;
    private readonly IEnumerable<IDomainEventHandler> _handlers;
    private readonly ILogger<DomainEventDispatcherHostedService> _logger;

    public DomainEventDispatcherHostedService(
        IDomainEventBus bus,
        IEnumerable<IDomainEventHandler> handlers,
        ILogger<DomainEventDispatcherHostedService> logger)
    {
        _bus = bus;
        _handlers = handlers;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var evt in _bus.ReadAllAsync(stoppingToken))
        {
            foreach (var handler in _handlers)
            {
                if (!handler.CanHandle(evt.Type)) continue;

                try
                {
                    await handler.HandleAsync(evt, stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Domain event handler failed for {EventType} ({EventId})", evt.Type, evt.Id);
                }
            }
        }
    }
}

