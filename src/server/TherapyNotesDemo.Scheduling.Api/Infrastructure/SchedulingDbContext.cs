using Microsoft.EntityFrameworkCore;

namespace TherapyNotesDemo.Scheduling.Api.Infrastructure;

public sealed class SchedulingDbContext : DbContext
{
    public SchedulingDbContext(DbContextOptions<SchedulingDbContext> options) : base(options)
    {
    }

    public DbSet<ClientRow> Clients => Set<ClientRow>();
    public DbSet<AppointmentRow> Appointments => Set<AppointmentRow>();
    public DbSet<AuditEntryRow> AuditEntries => Set<AuditEntryRow>();
    public DbSet<OutboxMessageRow> OutboxMessages => Set<OutboxMessageRow>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ClientRow>(b =>
        {
            b.ToTable("clients");
            b.HasKey(x => x.Id);
            b.Property(x => x.DisplayName).HasMaxLength(200).IsRequired();
            b.Property(x => x.CreatedAt).IsRequired();
            b.HasIndex(x => x.CreatedAt);
        });

        modelBuilder.Entity<AppointmentRow>(b =>
        {
            b.ToTable("appointments");
            b.HasKey(x => x.Id);
            b.Property(x => x.ClientId).IsRequired();
            b.Property(x => x.StartsAt).IsRequired();
            b.Property(x => x.DurationMinutes).IsRequired();
            b.Property(x => x.CreatedAt).IsRequired();
            b.HasIndex(x => x.StartsAt);
            b.HasIndex(x => x.ClientId);
        });

        modelBuilder.Entity<AuditEntryRow>(b =>
        {
            b.ToTable("audit_entries");
            b.HasKey(x => x.Id);
            b.Property(x => x.OccurredAt).IsRequired();
            b.Property(x => x.EventType).HasMaxLength(200).IsRequired();
            b.Property(x => x.Message).HasMaxLength(2000).IsRequired();
            b.HasIndex(x => x.OccurredAt);
            b.HasIndex(x => x.EventType);
        });

        modelBuilder.Entity<OutboxMessageRow>(b =>
        {
            b.ToTable("outbox_messages");
            b.HasKey(x => x.Id);
            b.Property(x => x.OccurredAt).IsRequired();
            b.Property(x => x.Type).HasMaxLength(200).IsRequired();
            b.Property(x => x.DataJson).IsRequired();
            b.Property(x => x.Status).HasMaxLength(40).IsRequired();
            b.Property(x => x.ClaimedUntil);
            b.Property(x => x.CompletedAt);
            b.HasIndex(x => x.OccurredAt);
            b.HasIndex(x => x.Status);
            b.HasIndex(x => x.ClaimedUntil);
        });
    }
}

public sealed class ClientRow
{
    public Guid Id { get; set; }
    public string DisplayName { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class AppointmentRow
{
    public Guid Id { get; set; }
    public Guid ClientId { get; set; }
    public DateTimeOffset StartsAt { get; set; }
    public int DurationMinutes { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class AuditEntryRow
{
    public Guid Id { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
    public string EventType { get; set; } = "";
    public string Message { get; set; } = "";
}

public sealed class OutboxMessageRow
{
    public Guid Id { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
    public string Type { get; set; } = "";
    public string DataJson { get; set; } = "";
    public string Status { get; set; } = "Pending";
    public DateTimeOffset? ClaimedUntil { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
}

