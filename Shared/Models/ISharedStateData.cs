namespace EventSourcing.Shared.Models
{
    public interface ISharedStateData
    {
        public AggregateId Id { get; set; }
        public bool IsDeleted { get; set; }
    }
}
