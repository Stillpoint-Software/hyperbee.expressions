using System.Runtime.CompilerServices;

namespace Hyperbee.Expressions.Collections;

public class PooledStack<T> : IDisposable
{
    private readonly PooledArray<T> _array;
    private int _top;
    private bool _disposed;

    public PooledStack( int initialCapacity = 16 )
    {
        _array = new PooledArray<T>( initialCapacity );
        _top = 0;
        _disposed = false;
    }

    public int Count => _top;

    [MethodImpl( MethodImplOptions.AggressiveInlining )]
    public void Push( T item )
    {
        _array[_top++] = item; // `PooledArray` will automatically resize if needed 
    }

    [MethodImpl( MethodImplOptions.AggressiveInlining )]
    public T Pop()
    {
        if ( _top == 0 )
            throw new InvalidOperationException( "Stack is empty." );

        T item = _array[--_top];
        _array[_top] = default; // Clear the reference
        return item;
    }

    [MethodImpl( MethodImplOptions.AggressiveInlining )]
    public T Peek()
    {
        if ( _top == 0 )
            throw new InvalidOperationException( "Stack is empty." );

        return _array[_top - 1];
    }

    [MethodImpl( MethodImplOptions.AggressiveInlining )]
    public void Clear()
    {
        _array.Resize( 0 );
        _top = 0;
    }

    public void Dispose()
    {
        if ( _disposed )
        {
            return;
        }

        _array.Dispose();
        _disposed = true;
    }
}
