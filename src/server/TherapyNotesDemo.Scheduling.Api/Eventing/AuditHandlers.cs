using TherapyNotesDemo.Scheduling.Api.Domain;
using TherapyNotesDemo.Scheduling.Api.Infrastructure;

namespace TherapyNotesDemo.Scheduling.Api.Eventing;

public sealed class AuditLogDomainEventHandler : IDomainEventHandler
{
    private readonly AuditLogStore _audit;

    public AuditLogDomainEventHandler(AuditLogStore audit)
    {
        _audit = audit;
    }

    public bool CanHandle(string eventType)
        => eventType is nameof(ClientCreatedDomainEvent) or nameof(AppointmentScheduledDomainEvent);

    public async Task HandleAsync(IDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        var msg = domainEvent switch
        {
            ClientCreatedDomainEvent e => $"Client created: {e.ClientDisplayName}",
            AppointmentScheduledDomainEvent e => $"Appointment scheduled: {e.AppointmentId} (client {e.ClientId}) at {e.StartsAt:O}",
            _ => $"Event: {domainEvent.Type}"
        };

        var entry = new AuditEntry(
            Guid.NewGuid(),
            domainEvent.OccurredAt,
            domainEvent.Type,
            msg
        );

        await _audit.AppendAsync(entry, cancellationToken);
    }
}

