namespace EventSourcing.Shared.Models
{
    public interface ISharedStateData
    {
        public AggregateId Id { get; init; }
        public bool IsDeleted { get; set; }
    }
}
