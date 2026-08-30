using System.Linq.Expressions;
using System.Reflection;
using static System.Linq.Expressions.Expression;
using static Hyperbee.Expressions.ExpressionExtensions;

namespace Hyperbee.Expressions.Compiler.Benchmarks;

/// <summary>
/// Expression trees that exercise coroutine lowering and variable capture.
///
/// These are the paths affected by capture analysis: an async or enumerable block
/// introduces its state-machine body during Reduce(), after the compiler's capture scan
/// has already run. The NoCapture tiers guard against regressing the common case; the
/// Capture tiers measure what correct capture handling costs.
/// </summary>
/// <remarks>
/// Every tier is exposed as a factory, not a shared instance. A coroutine block caches its
/// reduction, so handing the same instance to two compilers would let the first compiler's
/// state machine be reused by the second and make the comparison meaningless.
/// </remarks>
public static class CoroutineExpressions
{
    // Completes synchronously so the benchmarks measure the state machine, not the scheduler.
    public static Task<int> EchoAsync( int value ) => Task.FromResult( value );

    private static MethodInfo EchoMethod => typeof( CoroutineExpressions )
        .GetMethod( nameof( EchoAsync ) )!;

    // Async block with no enclosing-scope references — the common case
    public static Expression<Func<int, Task<int>>> AsyncNoCapture( ExpressionRuntimeOptions options = null )
    {
        var input = Parameter( typeof( int ), "input" );
        var local = Variable( typeof( int ), "local" );

        return Lambda<Func<int, Task<int>>>(
            BlockAsync(
                new[] { local },
                new Expression[]
                {
                    Assign( local, Constant( 7 ) ),
                    Assign( local, Await( Call( EchoMethod, local ) ) ),
                    Assign( local, Add( local, Await( Call( EchoMethod, local ) ) ) ),
                    local
                },
                options
            ),
            input );
    }

    // Options that force MoveNext to stay a delegate the machine invokes on each resume.
    public static ExpressionRuntimeOptions DelegateMoveNext( Action<string> sourceHandler = null ) =>
        new() { EmitMoveNextIntoType = false, SourceHandler = sourceHandler };

    // Options that leave MoveNext to be emitted into the machine's own method.
    public static ExpressionRuntimeOptions EmittedMoveNext( Action<string> sourceHandler = null ) =>
        new() { SourceHandler = sourceHandler };

    // Async block that reads and writes an enclosing lambda parameter
    public static Expression<Func<int, Task<int>>> AsyncCapture()
    {
        var input = Parameter( typeof( int ), "input" );

        return Lambda<Func<int, Task<int>>>(
            BlockAsync(
                Assign( input, Await( Call( EchoMethod, input ) ) ),
                Assign( input, Add( input, Await( Call( EchoMethod, input ) ) ) ),
                input
            ),
            input );
    }

    // Enumerable block with no enclosing-scope references
    public static Expression<Func<int, IEnumerable<int>>> EnumerableNoCapture( ExpressionRuntimeOptions options = null )
    {
        var input = Parameter( typeof( int ), "input" );
        var local = Variable( typeof( int ), "local" );

        return Lambda<Func<int, IEnumerable<int>>>(
            BlockEnumerable(
                new[] { local },
                new Expression[]
                {
                    Assign( local, Constant( 7 ) ),
                    YieldReturn( local ),
                    YieldReturn( Add( local, Constant( 1 ) ) )
                },
                options
            ),
            input );
    }

    // Enumerable block that reads an enclosing lambda parameter
    public static Expression<Func<int, IEnumerable<int>>> EnumerableCapture()
    {
        var input = Parameter( typeof( int ), "input" );

        return Lambda<Func<int, IEnumerable<int>>>(
            BlockEnumerable(
                YieldReturn( input ),
                YieldReturn( Add( input, Constant( 1 ) ) )
            ),
            input );
    }

    // Task.Yield always suspends, so each await is a separate MoveNext invocation.
    private static MethodInfo YieldMethod => typeof( Task )
        .GetMethod( nameof( Task.Yield ), Type.EmptyTypes )!;

    // Sixteen real suspensions -- sixteen MoveNext invocations, where reaching MoveNext
    // through a delegate field would cost sixteen indirections instead of none.
    public static Expression<Func<int, Task<int>>> AsyncSuspending( ExpressionRuntimeOptions options = null )
    {
        var input = Parameter( typeof( int ), "input" );
        var local = Variable( typeof( int ), "local" );

        var body = new List<Expression> { Assign( local, Constant( 0 ) ) };

        for ( var index = 0; index < 16; index++ )
        {
            body.Add( Await( Call( YieldMethod ) ) );
            body.Add( Assign( local, Add( local, Constant( 1 ) ) ) );
        }

        body.Add( local );

        return Lambda<Func<int, Task<int>>>(
            BlockAsync( new[] { local }, body.ToArray(), options ),
            input );
    }

    // An ordinary captured variable — the pre-existing StrongBox path, as a control
    public static Expression<Func<int, int>> NestedClosure()
    {
        var input = Parameter( typeof( int ), "input" );
        var inner = Lambda<Func<int>>( Add( input, Constant( 1 ) ) );

        return Lambda<Func<int, int>>(
            Add( Invoke( inner ), Invoke( inner ) ),
            input );
    }
}
