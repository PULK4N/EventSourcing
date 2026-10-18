using EventSourcing.Persistence.Models;
using EventSourcing.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace EventSourcing.Persistence;

public class EventSourcingDbContext : DbContext
{
    public EventSourcingDbContext(DbContextOptions<EventSourcingDbContext> options)
        : base(options) { }

    public DbSet<SerializedEventPayload> SerializedEventPayload { get; set; }
    public DbSet<SerializedPayloadMessage> SerializedPayloadMessage { get; set; }
    public DbSet<UniqueConstraint> UniqueConstraints { get; set; }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.Entity<SerializedEventPayload>().HasKey(e => new { e.AggregateId, e.OrderNumber });

        builder.Entity<UniqueConstraint>(entity =>
        {
            entity.ToTable("UniqueConstraints");
            entity.HasKey(constraint => constraint.Id);
            entity.Property(constraint => constraint.Hash).HasMaxLength(32).IsFixedLength();
            entity.HasIndex(constraint => constraint.Hash).IsUnique();
            entity.HasIndex(constraint => constraint.AggregateId);
        });
    }
}
