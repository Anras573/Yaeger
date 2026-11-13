# ECS storage and queries

Every system reads components through `World.Query<...>()` or a `ComponentStorage<T>`, so their
cost sits under every update and render pass. This page describes how they are laid out and the
rules for mutating while iterating.

## Sparse-set storage

`ComponentStorage<T>` is a sparse set: a dense `T[]` and a parallel dense `Entity[]`, plus a
paged sparse `int[]` indexed by `Entity.Id` that maps an entity to its dense slot.

- Iteration is a linear walk over contiguous memory.
- `TryGet`/`Get` are two array reads (no hashing).
- Removal swaps the last element into the freed slot.
- The sparse array is paged (1024 ids per page, allocated lazily) because entity ids are never
  recycled; only pages that contain ids actually present in a store are allocated.
- `World` finds a store through a per-type static index (`ComponentTypeId<T>`) into an array,
  not a `Dictionary<Type, …>`.

## Queries

`world.Query<T1, T2[, T3[, T4]]>()` returns a `readonly struct` (`Query2`/`Query3`/`Query4`) with a
struct enumerator, so `foreach` allocates nothing:

```csharp
foreach (var (entity, transform, velocity) in world.Query<Transform2D, Velocity2D>())
{
    // ...
}
```

The query iterates the **smallest** of its stores and probes the others, so component order does
not matter for performance; `Query<...>(forceIndex)` pins which store drives. The result also
implements `IEnumerable<(Entity, ...)>`, so `.ToList()`, `.Single()` and other LINQ operators still
work (they box the enumerator, so keep them off hot paths).

## Mutating in place

`world.GetComponentRef<T>(entity)` (or `ComponentStorage<T>.GetRef`) returns a `ref T`, so a
system can modify a component without the copy / modify / `AddComponent` round trip. The
reference is invalidated by adding or removing a `T` on any entity; do not hold it across one.
`TryGetRef` returns a null ref (check with `Unsafe.IsNullRef`) instead of throwing.

## Mutation during iteration

A *structural* change to a store is adding a component to an entity that didn't have it, or
removing one (including through `DestroyEntity`). Overwriting an existing component is not
structural.

| While enumerating … | Allowed? |
|---|---|
| Overwrite an existing entity's component (`AddComponent` on an entity that already has it, or via a `ref`), in any store | Yes |
| Structurally change a store that is **not** being enumerated / not part of the query | Yes |
| Structurally change the store being enumerated, or **any** store named in the query | **No** — the enumerator throws `InvalidOperationException` on its next step |

The query rule covers every store in the query, not just the one driving it, and is checked on
every step including the one that ends the loop. Which store drives depends on relative store
sizes, so a narrower rule would make the same loop throw or not depending on the data. To add,
remove or destroy while looping, snapshot first: `world.Query<...>().ToList()` or
`store.All().ToList()`.

Iteration order is dense order (insertion order, except that removals move the last element into
the vacated slot). Code that needs a deterministic order must sort explicitly.

## Measuring

`Samples/Benchmarks` has a headless `ecs` benchmark that times 2- and 3-component queries against
a reproduction of the previous dictionary + iterator design and reports bytes allocated per pass:

```bash
dotnet run -c Release --project Samples/Benchmarks -- ecs 100000
```
