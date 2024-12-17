using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Immutable;

namespace Hyperbee.Expressions.CompilerServices.Collections;

// a dictionary comprised of a stack of dictionaries

public record LinkedDictionaryNode<TKey, TValue>
{
    public string Name { get; init; }
    public IDictionary<TKey, TValue> Dictionary { get; init; }
}

public enum KeyValueOptions
{
    None,
    All,
    Current,
    First
}

public interface ILinkedDictionary<TKey, TValue> : IDictionary<TKey, TValue>
{
    IEqualityComparer<TKey> Comparer { get; }

    string Name { get; }

    IEnumerable<LinkedDictionaryNode<TKey, TValue>> Nodes();
    IEnumerable<KeyValuePair<TKey, TValue>> Items( KeyValueOptions options = KeyValueOptions.None );

    TValue this[TKey key, KeyValueOptions options] { set; } // let and set support
    void Clear( KeyValueOptions options );
    bool Remove( TKey key, KeyValueOptions options );

    void Push( IEnumerable<KeyValuePair<TKey, TValue>> collection = default );
    void Push( string name, IEnumerable<KeyValuePair<TKey, TValue>> collection = default );
    LinkedDictionaryNode<TKey, TValue> Pop();
}

public class LinkedDictionary<TKey, TValue> : ILinkedDictionary<TKey, TValue>
{
    public IEqualityComparer<TKey> Comparer { get; }

    public ImmutableStack<LinkedDictionaryNode<TKey, TValue>> _nodes = [];

    // ctors

    public LinkedDictionary()
    {
    }

    public LinkedDictionary( IEqualityComparer<TKey> comparer )
        : this( null, comparer )
    {
    }

    public LinkedDictionary( IEnumerable<KeyValuePair<TKey, TValue>> collection )
        : this( collection, null )
    {
    }

    public LinkedDictionary( IEnumerable<KeyValuePair<TKey, TValue>> collection, IEqualityComparer<TKey> comparer )
    {
        Comparer = comparer;

        if ( collection != null )
            Push( collection );
    }

    public LinkedDictionary( ILinkedDictionary<TKey, TValue> inner )
        : this( inner, null )
    {
    }

    public LinkedDictionary( ILinkedDictionary<TKey, TValue> inner, IEnumerable<KeyValuePair<TKey, TValue>> collection )
    {
        Comparer = inner.Comparer;
        _nodes = ImmutableStack.CreateRange( inner.Nodes() );

        if ( collection != null )
            Push( collection );
    }

    // Stack

    public void Push( IEnumerable<KeyValuePair<TKey, TValue>> collection = default )
    {
        Push( null, collection );
    }

    public void Push( string name, IEnumerable<KeyValuePair<TKey, TValue>> collection = default )
    {
        var dictionary = collection == null
            ? new ConcurrentDictionary<TKey, TValue>( Comparer )
            : new ConcurrentDictionary<TKey, TValue>( collection, Comparer );

        _nodes = _nodes.Push( new LinkedDictionaryNode<TKey, TValue>
        {
            Name = name ?? Guid.NewGuid().ToString(),
            Dictionary = dictionary
        } );
    }

    public LinkedDictionaryNode<TKey, TValue> Pop()
    {
        _nodes = _nodes.Pop( out var node );
        return node;
    }

    // ILinkedDictionary

    public string Name => _nodes.PeekRef().Name;

    public TValue this[TKey key, KeyValueOptions options]
    {
        set
        {
            // support both 'let' and 'set' style assignments
            //
            //  'set' will assign value to the nearest existing key, or to the current node if no key is found. 
            //  'let' will assign value to the current node dictionary.

            if ( options != KeyValueOptions.Current )
            {
                // find and set if exists in an inner node
                foreach ( var scope in _nodes.Where( scope => scope.Dictionary.ContainsKey( key ) ) )
                {
                    scope.Dictionary[key] = value;
                    return;
                }
            }

            // set in current node
            _nodes.PeekRef().Dictionary[key] = value;
        }
    }

    public IEnumerable<LinkedDictionaryNode<TKey, TValue>> Nodes() => _nodes;

    public IEnumerable<KeyValuePair<TKey, TValue>> Items( KeyValueOptions options = KeyValueOptions.None )
    {
        var keys = options == KeyValueOptions.First ? new HashSet<TKey>( Comparer ) : null;

        foreach ( var scope in _nodes )
        {
            foreach ( var pair in scope.Dictionary )
            {
                if ( options == KeyValueOptions.First )
                {
                    if ( keys!.Contains( pair.Key ) )
                        continue;

                    keys.Add( pair.Key );
                }

                yield return pair;
            }

            if ( options == KeyValueOptions.Current )
                break;
        }
    }

    public void Clear( KeyValueOptions options )
    {
        if ( options != KeyValueOptions.Current && options != KeyValueOptions.First )
        {
            _nodes.Pop( out var node );
            _nodes = [node];
        }

        _nodes.PeekRef().Dictionary.Clear();
    }

    public bool Remove( TKey key, KeyValueOptions options )
    {
        var result = false;

        foreach ( var _ in _nodes.Where( scope => scope.Dictionary.Remove( key ) ) )
        {
            result = true;

            if ( options == KeyValueOptions.First )
                break;
        }

        return result;
    }

    // IDictionary

    public TValue this[TKey key]
    {
        get
        {
            if ( !TryGetValue( key, out var result ) )
                throw new KeyNotFoundException();

            return result;
        }

        set => this[key, KeyValueOptions.First] = value;
    }

    public bool IsReadOnly => false;
    public int Count => _nodes.Count();

    public void Add( TKey key, TValue value )
    {
        if ( ContainsKey( key ) )
            throw new ArgumentException( "Key already exists." );

        this[key, KeyValueOptions.First] = value;
    }

    public void Clear() => Clear( KeyValueOptions.All );

    public bool ContainsKey( TKey key )
    {
        return _nodes.Any( scope => scope.Dictionary.ContainsKey( key ) );
    }

    public bool Remove( TKey key ) => Remove( key, KeyValueOptions.First );

    public bool TryGetValue( TKey key, out TValue value )
    {
        foreach ( var scope in _nodes )
        {
            if ( scope.Dictionary.TryGetValue( key, out value ) )
                return true;
        }

        value = default;
        return false;
    }

    // ICollection

    void ICollection<KeyValuePair<TKey, TValue>>.Add( KeyValuePair<TKey, TValue> item )
    {
        var (key, value) = item;
        Add( key, value );
    }

    bool ICollection<KeyValuePair<TKey, TValue>>.Contains( KeyValuePair<TKey, TValue> item )
    {
        return _nodes.Any( scope => scope.Dictionary.Contains( item ) );
    }

    void ICollection<KeyValuePair<TKey, TValue>>.CopyTo( KeyValuePair<TKey, TValue>[] array, int arrayIndex )
    {
        ArgumentNullException.ThrowIfNull( array, nameof( array ) );

        if ( (uint) arrayIndex > (uint) array.Length )
            throw new IndexOutOfRangeException();

        if ( array.Length - arrayIndex < Count )
            throw new IndexOutOfRangeException( "Array plus offset is out of range." );

        foreach ( var current in _nodes.Select( scope => scope.Dictionary ).Where( current => current.Count != 0 ) )
        {
            current.CopyTo( array, arrayIndex );
            arrayIndex += current.Count;
        }
    }

    bool ICollection<KeyValuePair<TKey, TValue>>.Remove( KeyValuePair<TKey, TValue> item )
    {
        return _nodes.Any( scope => scope.Dictionary.Remove( item ) );
    }

    ICollection<TKey> IDictionary<TKey, TValue>.Keys => Items( KeyValueOptions.First ).Select( pair => pair.Key ).ToArray();
    ICollection<TValue> IDictionary<TKey, TValue>.Values => Items( KeyValueOptions.First ).Select( pair => pair.Value ).ToArray();

    // Enumeration

    public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator() => Items( KeyValueOptions.All ).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
