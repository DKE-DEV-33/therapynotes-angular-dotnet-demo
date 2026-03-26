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
            b.Property(x => x.CreatedAtUtc).IsRequired();
            b.HasIndex(x => x.CreatedAtUtc);
        });

        modelBuilder.Entity<AppointmentRow>(b =>
        {
            b.ToTable("appointments");
            b.HasKey(x => x.Id);
            b.Property(x => x.ClientId).IsRequired();
            b.Property(x => x.StartsAtUtc).IsRequired();
            b.Property(x => x.DurationMinutes).IsRequired();
            b.Property(x => x.CreatedAtUtc).IsRequired();
            b.HasIndex(x => x.StartsAtUtc);
            b.HasIndex(x => x.ClientId);
        });

        modelBuilder.Entity<AuditEntryRow>(b =>
        {
            b.ToTable("audit_entries");
            b.HasKey(x => x.Id);
            b.Property(x => x.OccurredAtUtc).IsRequired();
            b.Property(x => x.EventType).HasMaxLength(200).IsRequired();
            b.Property(x => x.Message).HasMaxLength(2000).IsRequired();
            b.HasIndex(x => x.OccurredAtUtc);
            b.HasIndex(x => x.EventType);
        });

        modelBuilder.Entity<OutboxMessageRow>(b =>
        {
            b.ToTable("outbox_messages");
            b.HasKey(x => x.Id);
            b.Property(x => x.OccurredAtUtc).IsRequired();
            b.Property(x => x.Type).HasMaxLength(200).IsRequired();
            b.Property(x => x.DataJson).IsRequired();
            b.Property(x => x.Status).HasMaxLength(40).IsRequired();
            b.Property(x => x.ClaimedUntilUtc);
            b.Property(x => x.CompletedAtUtc);
            b.HasIndex(x => x.OccurredAtUtc);
            b.HasIndex(x => x.Status);
            b.HasIndex(x => x.ClaimedUntilUtc);
        });
    }
}

public sealed class ClientRow
{
    public Guid Id { get; set; }
    public string DisplayName { get; set; } = "";
    public DateTime CreatedAtUtc { get; set; }
}

public sealed class AppointmentRow
{
    public Guid Id { get; set; }
    public Guid ClientId { get; set; }
    public DateTime StartsAtUtc { get; set; }
    public int DurationMinutes { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

public sealed class AuditEntryRow
{
    public Guid Id { get; set; }
    public DateTime OccurredAtUtc { get; set; }
    public string EventType { get; set; } = "";
    public string Message { get; set; } = "";
}

public sealed class OutboxMessageRow
{
    public Guid Id { get; set; }
    public DateTime OccurredAtUtc { get; set; }
    public string Type { get; set; } = "";
    public string DataJson { get; set; } = "";
    public string Status { get; set; } = "Pending";
    public DateTime? ClaimedUntilUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
}
