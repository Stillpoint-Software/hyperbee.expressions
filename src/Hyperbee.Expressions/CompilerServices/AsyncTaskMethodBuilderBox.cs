using System.Runtime.CompilerServices;

namespace Hyperbee.Expressions.CompilerServices;

/// <summary>
/// A class wrapper around <see cref="AsyncTaskMethodBuilder{TResult}"/> (a struct) that provides
/// stable reference semantics for use in dynamically generated async state machines.
/// </summary>
/// <remarks>
/// <para>
/// The async state machine type is built at runtime via <see cref="System.Reflection.Emit.TypeBuilder"/>.
/// Its <c>__builder&lt;&gt;</c> field is accessed and mutated through expression trees
/// (e.g. <see cref="System.Linq.Expressions.Expression.Call(System.Linq.Expressions.Expression,System.Reflection.MethodInfo,System.Linq.Expressions.Expression[])"/>).
/// </para>
/// <para>
/// Expression trees evaluate <see cref="System.Linq.Expressions.MemberExpression"/> access on a
/// value-type field by <b>copying</b> the struct — any mutations made on the copy are immediately
/// discarded. If <see cref="AsyncTaskMethodBuilder{TResult}"/> were stored directly as a struct
/// field, calls to <c>SetResult</c>, <c>SetException</c>, and <c>AwaitUnsafeOnCompleted</c>
/// through the expression tree would silently operate on a throwaway copy, breaking the state
/// machine entirely.
/// </para>
/// <para>
/// This class acts as a typed heap box (analogous to <see cref="StrongBox{T}"/>
/// but with explicit forwarding methods). The state machine's <c>__builder&lt;&gt;</c> field holds
/// a reference to this object. Expression tree calls resolve to forwarding methods on the class,
/// which in turn access <see cref="Builder"/> via <c>ldflda</c> — mutating the struct in-place
/// on the heap — at zero net overhead due to <see cref="MethodImplOptions.AggressiveInlining"/>.
/// </para>
/// </remarks>
public sealed class AsyncTaskMethodBuilderBox<TResult>
{
    /// <summary>
    /// The underlying task method builder struct. Always access via the forwarding methods
    /// on this class; never copy this field or call methods on it directly through an
    /// expression tree, as doing so will silently operate on a throwaway copy.
    /// </summary>
    public AsyncTaskMethodBuilder<TResult> Builder;

    public Task<TResult> Task => Builder.Task;

    [MethodImpl( MethodImplOptions.AggressiveInlining )]
    public void SetStateMachine( IAsyncStateMachine stateMachine )
    {
        Builder.SetStateMachine( stateMachine );
    }

    [MethodImpl( MethodImplOptions.AggressiveInlining )]
    public void Start<TStateMachine>( ref TStateMachine stateMachine ) where TStateMachine : IAsyncStateMachine
    {
        Builder.Start( ref stateMachine );
    }

    [MethodImpl( MethodImplOptions.AggressiveInlining )]
    public void SetResult( TResult result )
    {
        Builder.SetResult( result );
    }

    [MethodImpl( MethodImplOptions.AggressiveInlining )]
    public void SetException( Exception exception )
    {
        Builder.SetException( exception );
    }

    [MethodImpl( MethodImplOptions.AggressiveInlining )]
    public void AwaitOnCompleted<TAwaiter, TStateMachine>(
        ref TAwaiter awaiter,
        ref TStateMachine stateMachine
    ) where TAwaiter : INotifyCompletion where TStateMachine : IAsyncStateMachine
    {
        Builder.AwaitOnCompleted( ref awaiter, ref stateMachine );
    }

    [MethodImpl( MethodImplOptions.AggressiveInlining )]
    public void AwaitUnsafeOnCompleted<TAwaiter, TStateMachine>(
        ref TAwaiter awaiter,
        ref TStateMachine stateMachine
    ) where TAwaiter : ICriticalNotifyCompletion where TStateMachine : IAsyncStateMachine
    {
        Builder.AwaitUnsafeOnCompleted( ref awaiter, ref stateMachine );
    }
}
