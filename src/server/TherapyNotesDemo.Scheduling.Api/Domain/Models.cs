namespace TherapyNotesDemo.Scheduling.Api.Domain;

public sealed record Client(
    Guid Id,
    string DisplayName,
    DateTimeOffset CreatedAt
);

public sealed record Appointment(
    Guid Id,
    Guid ClientId,
    DateTimeOffset StartsAt,
    int DurationMinutes,
    DateTimeOffset CreatedAt
);

public sealed record AuditEntry(
    Guid Id,
    DateTimeOffset OccurredAt,
    string EventType,
    string Message
);

