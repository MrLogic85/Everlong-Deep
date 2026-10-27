using System.Diagnostics;
using EverlongDeep.Simulation.Beliefs;
using EverlongDeep.Simulation.Things;

namespace EverlongDeep.Simulation.People;

/// <summary>A person in the world, as <c>docs/08 Social Model.md</c> → "Person" defines.</summary>
public sealed class Person(PersonId id, string name, IReadOnlyList<Trait> traits, ThingId place)
{
    private readonly List<Relation> _relations = [];
    private readonly List<Belief> _beliefs = [];

    public PersonId Id { get; } = id;
    public string Name { get; } = name;
    public IReadOnlyList<Trait> Traits { get; } = traits;
    /// <summary>The place or ship this person is in.</summary>
    public ThingId Place { get; } = place;

    /// <summary>The people this person knows, in the order they met.</summary>
    public IReadOnlyList<Relation> Relations => _relations;

    /// <summary>What this person believes happened, in the order they came to believe it.</summary>
    public IReadOnlyList<Belief> Beliefs => _beliefs;

    /// <summary>This person's relation to <paramref name="other"/>, or null if they have never met.</summary>
    public Relation? RelationTo(PersonId other) => _relations.Find(relation => relation.Other == other);

    /// <summary>Starts only this person's side of knowing <paramref name="other"/>; the caller starts the other.</summary>
    internal Relation Meet(PersonId other, double opinion, double trust)
    {
        Debug.Assert(other != Id, "A person has no relation to themselves");
        Debug.Assert(RelationTo(other) is null, "A person meets someone only once");

        var relation = new Relation(other, opinion, trust);
        _relations.Add(relation);
        return relation;
    }

    internal Belief Believe(Claim claim, double confidence, double relevance)
    {
        var belief = new Belief(Id, claim, confidence, relevance);
        _beliefs.Add(belief);
        return belief;
    }
}
