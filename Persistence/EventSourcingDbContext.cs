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
    public DbSet<UniqueEventConstraint> UniqueEventConstraints { get; set; }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.Entity<SerializedEventPayload>().HasKey(e => new { e.AggregateId, e.OrderNumber });

        builder.Entity<UniqueEventConstraint>(entity =>
        {
            entity.ToTable("UniqueEventConstraints");

            entity.HasKey(constraint => constraint.ConstraintHash).IsClustered(false);

            entity
                .Property(constraint => constraint.ConstraintHash)
                .HasMaxLength(32)
                .IsFixedLength()
                .ValueGeneratedNever();
        });
    }
}
