namespace TherapyNotesDemo.Contracts;

public interface IIntegrationEvent
{
    Guid Id { get; }
    DateTimeOffset OccurredAt { get; }
    string Type { get; }
}

public sealed record ClientCreatedIntegrationEvent(
    Guid Id,
    DateTimeOffset OccurredAt,
    Guid ClientId,
    string ClientDisplayName
) : IIntegrationEvent
{
    public string Type => nameof(ClientCreatedIntegrationEvent);
}

public sealed record AppointmentScheduledIntegrationEvent(
    Guid Id,
    DateTimeOffset OccurredAt,
    Guid AppointmentId,
    Guid ClientId,
    DateTimeOffset StartsAt,
    int DurationMinutes
) : IIntegrationEvent
{
    public string Type => nameof(AppointmentScheduledIntegrationEvent);
}
