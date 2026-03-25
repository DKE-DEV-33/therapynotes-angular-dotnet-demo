namespace TherapyNotesDemo.Scheduling.Api.Domain;

public interface IDomainEvent
{
    Guid Id { get; }
    DateTimeOffset OccurredAt { get; }
    string Type { get; }
}

public sealed record ClientCreatedDomainEvent(
    Guid Id,
    DateTimeOffset OccurredAt,
    Guid ClientId,
    string ClientDisplayName
) : IDomainEvent
{
    public string Type => nameof(ClientCreatedDomainEvent);
}

public sealed record AppointmentScheduledDomainEvent(
    Guid Id,
    DateTimeOffset OccurredAt,
    Guid AppointmentId,
    Guid ClientId,
    DateTimeOffset StartsAt,
    int DurationMinutes
) : IDomainEvent
{
    public string Type => nameof(AppointmentScheduledDomainEvent);
}

