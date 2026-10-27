namespace EverlongDeep.Simulation.People;

/// <summary>
/// The bonds a relation holds, as <c>docs/08 Social Model.md</c> → "Relation" defines. Each names what the
/// other person is to the holder: A's relation to B holding <see cref="Captain"/> means B is A's captain.
/// </summary>
[Flags]
public enum Ties
{
    None = 0,
    Parent = 1 << 0,
    Child = 1 << 1,
    Sibling = 1 << 2,
    Spouse = 1 << 3,
    Captain = 1 << 4,
    Crew = 1 << 5,
    Friend = 1 << 6,
    Rival = 1 << 7,
}
