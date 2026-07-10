using EventSourcing.Persistence.Models;
using Microsoft.EntityFrameworkCore;

namespace EventSourcing.Persistence;

public class EventSourcingDbContext : DbContext
{
    public EventSourcingDbContext(DbContextOptions<EventSourcingDbContext> options)
        : base(options) { }

    public DbSet<SerializedEventPayload> SerializedEventPayload { get; set; }
    public DbSet<SerializedPayloadMessage> SerializedPayloadMessage { get; set; }


    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder
            .Entity<SerializedEventPayload>()
            .HasIndex(ep => new { ep.AggregateId, ep.OrderNumber })
            .IsUnique();
    }
}
