using System.Collections;

namespace Hyperbee.Expressions.Transformation;

internal class AsyncLocalDictionary<TKey, TValue> : IDisposable, IEnumerable<KeyValuePair<TKey, TValue>>
{
    private readonly Dictionary<TKey, TValue> _dictionary = new();
    private int _referenceCount;

    private static readonly AsyncLocal<AsyncLocalDictionary<TKey, TValue>> AsyncLocal = new();

    public static AsyncLocalDictionary<TKey, TValue> GetInstance()
    {
        var instance = AsyncLocal.Value;

        if ( instance == null )
        {
            instance = new AsyncLocalDictionary<TKey, TValue>();
            AsyncLocal.Value = instance;
        }

        Interlocked.Increment( ref instance._referenceCount );
        return instance;
    }

    void IDisposable.Dispose()
    {
        if ( Interlocked.Decrement( ref _referenceCount ) != 0 )
            return;

        _dictionary.Clear();
        AsyncLocal.Value = null;
    }

    public TValue this[TKey key]
    {
        get => _dictionary[key];
        set => _dictionary[key] = value;
    }

    public void Add( TKey key, TValue value ) => _dictionary.Add( key, value );

    public void Clear() => _dictionary.Clear();

    public bool Contains( TKey key ) => _dictionary.ContainsKey( key );

    public bool Remove( TKey key, out TValue value ) => _dictionary.Remove( key, out value );

    public bool TryAdd( TKey key, TValue value ) => _dictionary.TryAdd( key, value );

    public bool TryGetItem( TKey key, out TValue value ) => _dictionary.TryGetValue( key, out value );

    public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator() => _dictionary.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
