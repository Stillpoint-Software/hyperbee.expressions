using System.Buffers;
using System.Collections;
using System.Runtime.CompilerServices;

namespace Hyperbee.Expressions.Collections;

public class PooledArray<T> : IDisposable, IReadOnlyList<T>
{
    private T[] _array;
    private int _count;
    private bool _disposed;
    private const int DoublingThreshold = 1024;
    private const int FixedIncrement = 256;

    public PooledArray( int initialCapacity = 16 )
    {
        _array = ArrayPool<T>.Shared.Rent( initialCapacity );
        _count = 0;
        _disposed = false;
    }

    public ReadOnlySpan<T> AsReadOnlySpan() => new( _array, 0, _count );
    public ReadOnlySpan<T> AsReadOnlySpan( int start, int count ) => new( _array, start, Math.Min( count, _count ) );

    public Span<T> AsSpan() => new( _array, 0, _count );
    public Span<T> AsSpan( int start, int count ) => new( _array, start, Math.Min( count, _count ) );

    public int Count => _count;

    public T this[int index]
    {
        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        get
        {
            if ( index < 0 || index >= _count )
                throw new ArgumentOutOfRangeException( nameof( index ) );

            return _array[index];
        }
        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        set
        {
            EnsureCapacity( index + 1 );
            _array[index] = value;

            if ( index >= _count )
                _count = index + 1;
        }
    }

    private void EnsureCapacity( int requiredCapacity )
    {
        if ( requiredCapacity <= _array.Length )
            return;

        var newCapacity = _array.Length < DoublingThreshold
            ? _array.Length * 2
            : _array.Length + FixedIncrement;

        if ( newCapacity < requiredCapacity )
            newCapacity = requiredCapacity;

        var newArray = ArrayPool<T>.Shared.Rent( newCapacity );

        Array.Copy( _array, newArray, _count );
        ArrayPool<T>.Shared.Return( _array, clearArray: true );

        _array = newArray;
    }

    [MethodImpl( MethodImplOptions.AggressiveInlining )]
    public void Add( T item )
    {
        EnsureCapacity( _count + 1 );
        _array[_count++] = item;
    }

    public PooledArray<T> CopyTo( Func<T, bool> predicate )
    {
        var result = new PooledArray<T>( _count );

        for ( var i = 0; i < _count; i++ )
        {
            if ( predicate( _array[i] ) )
            {
                result.Add( _array[i] );
            }
        }

        return result;
    }

    public void CopyTo( PooledArray<T> destination, int sourceIndex, int destinationIndex, int count )
    {
        if ( destination == null )
            throw new ArgumentNullException( nameof( destination ) );

        if ( count == 0 )
            return;

        if ( sourceIndex < 0 || sourceIndex >= _count )
            throw new ArgumentOutOfRangeException( nameof( sourceIndex ), "Source index is out of range." );

        if ( count < 0 )
            throw new ArgumentOutOfRangeException( nameof( count ), "Count cannot be negative." );

        if ( sourceIndex + count > _count )
            throw new ArgumentException( "The source array does not have enough elements." );

        if ( destinationIndex + count > destination.Count )
            throw new ArgumentException( "Destination array is too small to hold the copied elements." );

        var sourceSpan = AsSpan( sourceIndex, count );
        var destinationSpan = destination.AsSpan( destinationIndex, count );

        sourceSpan.CopyTo( destinationSpan );
    }

    public void CopyTo( T[] destination, int sourceIndex, int destinationIndex, int count )
    {
        if ( destination == null )
            throw new ArgumentNullException( nameof( destination ) );

        if ( count == 0 )
            return;

        if ( sourceIndex < 0 || sourceIndex >= _count )
            throw new ArgumentOutOfRangeException( nameof( sourceIndex ), "Start index is out of range." );

        if ( count < 0 )
            count = _count - sourceIndex; // Default to the remaining elements

        if ( sourceIndex + count > _count )
            throw new ArgumentOutOfRangeException( nameof( count ), "Count exceeds available elements from the start index." );

        if ( destinationIndex + count > destination.Length )
            throw new ArgumentException( "Destination array is too small to hold the copied elements." );

        Array.Copy( _array, sourceIndex, destination, 0, count );
    }

    public void Insert( int index, T item )
    {
        if ( index < 0 || index > _count )
            throw new ArgumentOutOfRangeException( nameof( index ) );

        EnsureCapacity( _count + 1 );

        for ( var i = _count; i > index; i-- )
        {
            _array[i] = _array[i - 1];
        }

        _array[index] = item;
        _count++;
    }

    public void Remove( int index )
    {
        if ( index < 0 || index >= _count )
            throw new ArgumentOutOfRangeException( nameof( index ) );

        for ( var i = index; i < _count - 1; i++ )
        {
            _array[i] = _array[i + 1];
        }

        _array[--_count] = default;
    }

    public void Remove( Func<T, bool> predicate )
    {
        var shiftIndex = 0;

        for ( var i = 0; i < _count; i++ )
        {
            if ( predicate( _array[i] ) )
            {
                continue;
            }

            if ( shiftIndex != i )
            {
                _array[shiftIndex] = _array[i];
            }

            shiftIndex++;
        }

        Array.Clear( _array, shiftIndex, _count - shiftIndex );
        _count = shiftIndex;
    }

    public void Remove( Func<T, int, bool> predicate )
    {
        if ( predicate == null )
            throw new ArgumentNullException( nameof( predicate ) );

        var shiftIndex = 0;

        for ( var i = 0; i < _count; i++ )
        {
            if ( predicate( _array[i], shiftIndex ) ) // Pass the current item and the projected index
            {
                continue;
            }

            if ( shiftIndex != i )
            {
                _array[shiftIndex] = _array[i];
            }

            shiftIndex++;
        }

        Array.Clear( _array, shiftIndex, _count - shiftIndex );
        _count = shiftIndex;
    }
    
    public void Resize( int newSize )
    {
        if ( newSize < 0 )
            throw new ArgumentOutOfRangeException( nameof( newSize ), "Size cannot be negative." );

        if ( newSize < _count )
        {
            // Shrink the array
            Array.Clear( _array, newSize, _count - newSize );
        }
        else if ( newSize > _array.Length )
        {
            EnsureCapacity( newSize );
        }

        _count = newSize;
    }

    [MethodImpl( MethodImplOptions.AggressiveInlining )]
    public void Sort( Comparison<T> comparison )
    {
        Array.Sort( _array, 0, _count, Comparer<T>.Create( comparison ) );
    }

    public IEnumerator<T> GetEnumerator()
    {
        for ( var i = 0; i < _count; i++ )
        {
            yield return _array[i];
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public void Dispose()
    {
        if ( _disposed )
        {
            return;
        }

        ArrayPool<T>.Shared.Return( _array, clearArray: true );
        _disposed = true;
    }
}
