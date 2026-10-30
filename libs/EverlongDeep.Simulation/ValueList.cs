using System.Collections;
using System.Runtime.CompilerServices;

namespace EverlongDeep.Simulation;

/// <summary>
/// A read-only list equal to another with the same items in the same order, so a record holding one compares by
/// value, as a <see cref="LogRecord"/> must for a seeded run to be compared with another.
/// </summary>
[CollectionBuilder(typeof(ValueList), nameof(ValueList.Create))]
public sealed class ValueList<T> : IReadOnlyList<T>, IEquatable<ValueList<T>>
{
    private readonly T[] _items;

    internal ValueList(T[] items) => _items = items;

    public T this[int index] => _items[index];

    public int Count => _items.Length;

    public IEnumerator<T> GetEnumerator() => ((IEnumerable<T>)_items).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public bool Equals(ValueList<T>? other) => other is not null && _items.SequenceEqual(other._items);

    public override bool Equals(object? obj) => Equals(obj as ValueList<T>);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (T item in _items)
        {
            hash.Add(item);
        }

        return hash.ToHashCode();
    }

    // For assert messages and test failures.
    public override string ToString() => $"[{string.Join(", ", _items)}]";
}

/// <summary>Builds a <see cref="ValueList{T}"/> from a collection expression.</summary>
public static class ValueList
{
    public static ValueList<T> Create<T>(ReadOnlySpan<T> items) => new(items.ToArray());
}
