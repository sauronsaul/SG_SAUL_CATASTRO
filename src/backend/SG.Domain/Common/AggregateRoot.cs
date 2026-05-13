namespace SG.Domain.Common;

public abstract class AggregateRoot
{
    public Guid Id { get; protected set; } = Guid.NewGuid();
    public DateTime CreatedAt { get; protected internal set; }
    public DateTime? UpdatedAt { get; protected internal set; }
    public Guid? CreatedBy { get; protected internal set; }
    public Guid? UpdatedBy { get; protected internal set; }
    public bool IsDeleted { get; protected internal set; }
    public uint RowVersion { get; protected internal set; }

    protected AggregateRoot() { }
}
