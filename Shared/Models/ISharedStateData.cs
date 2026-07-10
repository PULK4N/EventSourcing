namespace EventSourcing.Shared.Models
{
    public interface ISharedStateData
    {
        public Guid Id { get; set; }
        public bool IsDeleted { get; set; }
    }
}
