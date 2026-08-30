using System.Linq.Expressions;
using Hyperbee.Expressions.Compiler.Tests.TestSupport;
using Hyperbee.Expressions.CompilerServices;
using static System.Linq.Expressions.Expression;
using static Hyperbee.Expressions.ExpressionExtensions;

namespace Hyperbee.Expressions.Compiler.Tests.Integration;

/// <summary>
/// A variable shared between a coroutine and its enclosing scope has to stay shared across a
/// suspension: the enclosing scope can write it while the machine is parked, and the machine
/// can write it before the enclosing scope reads again.
/// </summary>
/// <remarks>
/// Carrying such a variable by value in a state-machine field would be a snapshot, not a
/// share. These tests pin the difference, which only shows up when the coroutine actually
/// suspends -- a synchronously-completing await never gives the enclosing scope a chance to
/// interleave.
/// </remarks>
[TestClass]
public class CoroutineSharingTests
{
    private static TaskCompletionSource _gate = new();

    public static Task Gate() => _gate.Task;

    private static Expression GateCall() =>
        Call( typeof( CoroutineSharingTests ), nameof( Gate ), Type.EmptyTypes );

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public async Task EnclosingWriteDuringSuspension_IsVisibleToTheCoroutine( CompilerType compiler )
    {
        // Arrange
        //   value = 1
        //   task  = async { await gate; return value }
        //   value = 2
        //   release gate  ->  the coroutine must observe 2
        var value = Variable( typeof( int ), "value" );
        var task = Variable( typeof( Task<int> ), "task" );

        var body = Block(
            new[] { value, task },
            Assign( value, Constant( 1 ) ),
            Assign( task, BlockAsync( Await( GateCall() ), value ) ),
            Assign( value, Constant( 2 ) ),
            task );

        var lambda = Lambda<Func<Task<int>>>( body );
        var compiled = lambda.Compile( compiler );

        _gate = new TaskCompletionSource( TaskCreationOptions.RunContinuationsAsynchronously );

        // Act
        var pending = compiled();
        _gate.SetResult();

        var result = await pending;

        // Assert
        Assert.AreEqual( 2, result );
    }

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public async Task CoroutineWriteDuringSuspension_IsVisibleToTheEnclosingScope( CompilerType compiler )
    {
        // Arrange: the coroutine writes the shared variable after resuming; the enclosing
        // scope reads it once the task completes.
        var value = Variable( typeof( int ), "value" );
        var task = Variable( typeof( Task ), "task" );

        var body = Block(
            new[] { value, task },
            Assign( value, Constant( 1 ) ),
            Assign( task, BlockAsync(
                Await( GateCall() ),
                Assign( value, Constant( 42 ) ) ) ),
            task );

        var lambda = Lambda<Func<Task>>( body );
        var compiledOuter = lambda.Compile( compiler );

        // The read has to happen after the task completes, so the value is returned through
        // a second lambda over the same variable.
        var read = Lambda<Func<int>>( value );

        var pair = Lambda<Func<ValueTuple<Task, Func<int>>>>(
            Block(
                new[] { value, task },
                Assign( value, Constant( 1 ) ),
                Assign( task, BlockAsync(
                    Await( GateCall() ),
                    Assign( value, Constant( 42 ) ) ) ),
                New(
                    typeof( ValueTuple<Task, Func<int>> ).GetConstructor( [typeof( Task ), typeof( Func<int> )] )!,
                    task,
                    read ) ) );

        var compiled = pair.Compile( compiler );

        _gate = new TaskCompletionSource( TaskCreationOptions.RunContinuationsAsynchronously );

        // Act
        var (pending, reader) = compiled();
        _gate.SetResult();
        await pending;

        // Assert
        Assert.AreEqual( 42, reader() );

        GC.KeepAlive( compiledOuter );
    }

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public async Task SharedVariable_SurvivesMultipleSuspensions( CompilerType compiler )
    {
        // Arrange
        var value = Variable( typeof( int ), "value" );
        var task = Variable( typeof( Task<int> ), "task" );

        var body = Block(
            new[] { value, task },
            Assign( value, Constant( 10 ) ),
            Assign( task, BlockAsync(
                Await( GateCall() ),
                Assign( value, Add( value, Constant( 1 ) ) ),
                Await( GateCall() ),
                Add( value, Constant( 100 ) ) ) ),
            task );

        var lambda = Lambda<Func<Task<int>>>( body );
        var compiled = lambda.Compile( compiler );

        _gate = new TaskCompletionSource( TaskCreationOptions.RunContinuationsAsynchronously );

        // Act
        var pending = compiled();
        _gate.SetResult();

        var result = await pending;

        // Assert
        Assert.AreEqual( 111, result );
    }

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public async Task SharedLambdaParameter_IsVisibleToTheCoroutine( CompilerType compiler )
    {
        // Arrange
        var input = Parameter( typeof( int ), "input" );

        var lambda = Lambda<Func<int, Task<int>>>(
            BlockAsync( Await( GateCall() ), Add( input, Constant( 1 ) ) ),
            input );

        var compiled = lambda.Compile( compiler );

        _gate = new TaskCompletionSource( TaskCreationOptions.RunContinuationsAsynchronously );

        // Act
        var pending = compiled( 41 );
        _gate.SetResult();

        var result = await pending;

        // Assert
        Assert.AreEqual( 42, result );
    }

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public async Task ConcurrentMachines_DoNotShareState( CompilerType compiler )
    {
        // Arrange: two invocations of the same delegate must not see each other's variable.
        var input = Parameter( typeof( int ), "input" );
        var local = Variable( typeof( int ), "local" );

        var lambda = Lambda<Func<int, Task<int>>>(
            Block(
                new[] { local },
                Assign( local, input ),
                BlockAsync( Await( GateCall() ), Multiply( local, Constant( 2 ) ) ) ),
            input );

        var compiled = lambda.Compile( compiler );

        _gate = new TaskCompletionSource( TaskCreationOptions.RunContinuationsAsynchronously );

        // Act
        var first = compiled( 3 );
        var second = compiled( 5 );

        _gate.SetResult();

        var results = await Task.WhenAll( first, second );

        // Assert
        Assert.AreEqual( 6, results[0] );
        Assert.AreEqual( 10, results[1] );
    }

    [TestMethod]
    public async Task Rewriter_IsUsableWithTheSystemCompiler()
    {
        // The rewriter is a performance opt-in, not a correctness requirement. Applying it
        // by hand ahead of any compiler has to preserve semantics.
        var value = Variable( typeof( int ), "value" );
        var task = Variable( typeof( Task<int> ), "task" );

        var lambda = Lambda<Func<Task<int>>>(
            Block(
                new[] { value, task },
                Assign( value, Constant( 1 ) ),
                Assign( task, BlockAsync( Await( GateCall() ), value ) ),
                Assign( value, Constant( 2 ) ),
                task ) );

        var rewritten = CoroutineClosureRewriter.Rewrite( lambda );

        Assert.AreNotSame( lambda, rewritten );

        var compiled = (Func<Task<int>>) rewritten.Compile();

        _gate = new TaskCompletionSource( TaskCreationOptions.RunContinuationsAsynchronously );

        var pending = compiled();
        _gate.SetResult();

        Assert.AreEqual( 2, await pending );
    }

    [TestMethod]
    public void Rewriter_LeavesTreesWithNothingSharedAlone()
    {
        var local = Variable( typeof( int ), "local" );

        var lambda = Lambda<Func<Task<int>>>(
            BlockAsync(
                new[] { local },
                Assign( local, Constant( 7 ) ),
                Await( GateCall() ),
                local ) );

        Assert.AreSame( lambda, CoroutineClosureRewriter.Rewrite( lambda ) );
    }

    // Enumerable blocks suspend at every yield, so the enclosing scope can interleave
    // between MoveNext calls without any gate.

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public void EnclosingWriteBetweenYields_IsVisibleToTheSequence( CompilerType compiler )
    {
        // Arrange
        var value = Variable( typeof( int ), "value" );
        var sequence = Variable( typeof( IEnumerable<int> ), "sequence" );
        var incoming = Parameter( typeof( int ), "incoming" );

        var lambda = Lambda<Func<ValueTuple<IEnumerable<int>, Action<int>>>>(
            Block(
                new[] { value, sequence },
                Assign( value, Constant( 1 ) ),
                Assign( sequence, BlockEnumerable( YieldReturn( value ), YieldReturn( value ) ) ),
                New(
                    typeof( ValueTuple<IEnumerable<int>, Action<int>> )
                        .GetConstructor( [typeof( IEnumerable<int> ), typeof( Action<int> )] )!,
                    sequence,
                    Lambda<Action<int>>( Assign( value, incoming ), incoming ) ) ) );

        var (source, set) = lambda.Compile( compiler )();

        // Act
        using var enumerator = source.GetEnumerator();

        Assert.IsTrue( enumerator.MoveNext() );
        var first = enumerator.Current;

        set( 2 );

        Assert.IsTrue( enumerator.MoveNext() );
        var second = enumerator.Current;

        // Assert
        Assert.AreEqual( 1, first );
        Assert.AreEqual( 2, second );
    }

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public void SequenceWriteBetweenYields_IsVisibleToTheEnclosingScope( CompilerType compiler )
    {
        // Arrange: the sequence writes the shared variable; the enclosing scope reads it
        // through a lambda over the same variable.
        var value = Variable( typeof( int ), "value" );
        var sequence = Variable( typeof( IEnumerable<int> ), "sequence" );

        var lambda = Lambda<Func<ValueTuple<IEnumerable<int>, Func<int>>>>(
            Block(
                new[] { value, sequence },
                Assign( value, Constant( 1 ) ),
                Assign( sequence, BlockEnumerable(
                    YieldReturn( Constant( 0 ) ),
                    Assign( value, Constant( 99 ) ),
                    YieldReturn( Constant( 0 ) ) ) ),
                New(
                    typeof( ValueTuple<IEnumerable<int>, Func<int>> )
                        .GetConstructor( [typeof( IEnumerable<int> ), typeof( Func<int> )] )!,
                    sequence,
                    Lambda<Func<int>>( value ) ) ) );

        var (source, read) = lambda.Compile( compiler )();

        // Act
        using var enumerator = source.GetEnumerator();

        Assert.IsTrue( enumerator.MoveNext() );
        var before = read();

        Assert.IsTrue( enumerator.MoveNext() );
        var after = read();

        // Assert
        Assert.AreEqual( 1, before );
        Assert.AreEqual( 99, after );
    }

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public void SharedLambdaParameter_IsVisibleToTheSequence( CompilerType compiler )
    {
        // Arrange
        var input = Parameter( typeof( int ), "input" );

        var lambda = Lambda<Func<int, IEnumerable<int>>>(
            BlockEnumerable( YieldReturn( input ), YieldReturn( Add( input, Constant( 1 ) ) ) ),
            input );

        // Act
        var result = lambda.Compile( compiler )( 41 ).ToArray();

        // Assert
        CollectionAssert.AreEqual( new[] { 41, 42 }, result );
    }
}
