namespace EverlongDeep.Simulation.Places;

/// <summary>An abstract location such as a tavern; who is present is read from the people's places.</summary>
public sealed class Place(PlaceId id, string name)
{
    public PlaceId Id { get; } = id;
    public string Name { get; } = name;
}
