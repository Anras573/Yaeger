namespace Yaeger.ECS;

public readonly record struct Entity : IComparable<Entity>
{
    public readonly int Id;

    internal Entity(int id) => Id = id;

    public override int GetHashCode() => Id;

    public int CompareTo(Entity other) => Id.CompareTo(other.Id);
}
