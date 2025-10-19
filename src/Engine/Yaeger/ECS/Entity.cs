namespace Yaeger.ECS;

public readonly record struct Entity
{
    public readonly int Id;
    internal Entity(int id) => Id = id;
    public override int GetHashCode() => Id;
}