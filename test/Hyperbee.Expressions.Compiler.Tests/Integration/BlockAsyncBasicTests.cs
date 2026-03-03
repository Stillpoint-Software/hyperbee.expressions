using System.Linq.Expressions;
using Hyperbee.Expressions.Compiler.Tests.TestSupport;
using static System.Linq.Expressions.Expression;
using static Hyperbee.Expressions.ExpressionExtensions;

namespace Hyperbee.Expressions.Compiler.Tests.Integration;

/// <summary>
/// Integration tests for basic BlockAsync patterns compiled by HEC.
/// Covers faulted/canceled tasks, deferred suspension, variable hoisting, arithmetic,
/// return labels, nested blocks, lambda parameters.
/// </summary>
[TestClass]
public class BlockAsyncBasicTests
{
    // -----------------------------------------------------------------------
    // Faulted task — exception propagates through await
    // -----------------------------------------------------------------------

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public async Task BlockAsync_FaultedTask_ThrowsException( CompilerType compiler )
    {
        // Arrange
        var faulted = Task.FromException<int>( new InvalidOperationException( "hec-test" ) );

        var block = BlockAsync(
            new Expression[] { Await( Constant( faulted ) ) }
        );

        var lambda = Lambda<Func<Task<int>>>( block );
        var compiled = lambda.Compile( compiler );

        // Act & Assert
        await Assert.ThrowsExactlyAsync<InvalidOperationException>( async () => await compiled() );
    }

    // -----------------------------------------------------------------------
    // Canceled task — TaskCanceledException propagates
    // -----------------------------------------------------------------------

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public async Task BlockAsync_CanceledTask_ThrowsCanceled( CompilerType compiler )
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        var canceled = Task.FromCanceled<int>( cts.Token );

        var block = BlockAsync(
            new Expression[] { Await( Constant( canceled ) ) }
        );

        var lambda = Lambda<Func<Task<int>>>( block );
        var compiled = lambda.Compile( compiler );

        // Act & Assert
        await Assert.ThrowsExactlyAsync<TaskCanceledException>( async () => await compiled() );
    }

    // -----------------------------------------------------------------------
    // Deferred (not-yet-complete) task — exercises the suspend/resume path
    // -----------------------------------------------------------------------

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public async Task BlockAsync_DelayedTask_ReturnsResult( CompilerType compiler )
    {
        // Arrange
        var delayed = Task.Delay( 50 ).ContinueWith( _ => 42 );

        var block = BlockAsync(
            new Expression[] { Await( Constant( delayed ) ) }
        );

        var lambda = Lambda<Func<Task<int>>>( block );
        var compiled = lambda.Compile( compiler );

        // Act
        var result = await compiled();

        // Assert
        Assert.AreEqual( 42, result );
    }

    // -----------------------------------------------------------------------
    // Nested BlockAsync — inner async block is awaited by outer
    // -----------------------------------------------------------------------

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public async Task BlockAsync_NestedBlockAsync_ReturnsInnerResult( CompilerType compiler )
    {
        // Arrange
        var inner = BlockAsync(
            new Expression[] { Await( Call( typeof( Task ), nameof( Task.FromResult ), [typeof( int )], Constant( 7 ) ) ) }
        );

        var block = BlockAsync(
            new Expression[]
            {
                Await( Call( typeof( Task ), nameof( Task.FromResult ), [typeof( int )], Constant( 3 ) ) ),
                Await( inner )
            }
        );

        var lambda = Lambda<Func<Task<int>>>( block );
        var compiled = lambda.Compile( compiler );

        // Act
        var result = await compiled();

        // Assert
        Assert.AreEqual( 7, result );
    }

    // -----------------------------------------------------------------------
    // Mixed sync and async — sync constant is discarded, last await returned
    // -----------------------------------------------------------------------

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public async Task BlockAsync_MixedSyncAsync_ReturnsLastValue( CompilerType compiler )
    {
        // Arrange
        var block = BlockAsync(
            new Expression[]
            {
                Constant( 10 ),
                Await( Call( typeof( Task ), nameof( Task.FromResult ), [typeof( int )], Constant( 99 ) ) )
            }
        );

        var lambda = Lambda<Func<Task<int>>>( block );
        var compiled = lambda.Compile( compiler );

        // Act
        var result = await compiled();

        // Assert
        Assert.AreEqual( 99, result );
    }

    // -----------------------------------------------------------------------
    // Variable preserved across await suspension point
    // -----------------------------------------------------------------------

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public async Task BlockAsync_VariablePreservedAcrossAwait_AccumulatesCorrectly( CompilerType compiler )
    {
        // Arrange
        var result = Variable( typeof( int ), "result" );

        var block = BlockAsync(
            new[] { result },
            new Expression[]
            {
                Assign( result, Constant( 5 ) ),
                Await( Call( typeof( Task ), nameof( Task.FromResult ), [typeof( int )], Constant( 0 ) ) ),
                Assign( result, Add( result, Constant( 10 ) ) ),
                result
            }
        );

        var lambda = Lambda<Func<Task<int>>>( block );
        var compiled = lambda.Compile( compiler );

        // Act
        var value = await compiled();

        // Assert
        Assert.AreEqual( 15, value );
    }

    // -----------------------------------------------------------------------
    // Three sequential awaits — last value returned
    // -----------------------------------------------------------------------

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public async Task BlockAsync_ThreeSequentialAwaits_ReturnsLast( CompilerType compiler )
    {
        // Arrange
        var block = BlockAsync(
            new Expression[]
            {
                Await( Call( typeof( Task ), nameof( Task.FromResult ), [typeof( int )], Constant( 1 ) ) ),
                Await( Call( typeof( Task ), nameof( Task.FromResult ), [typeof( int )], Constant( 2 ) ) ),
                Await( Call( typeof( Task ), nameof( Task.FromResult ), [typeof( int )], Constant( 3 ) ) )
            }
        );

        var lambda = Lambda<Func<Task<int>>>( block );
        var compiled = lambda.Compile( compiler );

        // Act
        var result = await compiled();

        // Assert
        Assert.AreEqual( 3, result );
    }

    // -----------------------------------------------------------------------
    // Awaited values used in arithmetic
    // -----------------------------------------------------------------------

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public async Task BlockAsync_AwaitResultsInArithmetic_ReturnsProduct( CompilerType compiler )
    {
        // Arrange
        var a = Variable( typeof( int ), "a" );
        var b = Variable( typeof( int ), "b" );

        var block = BlockAsync(
            new[] { a, b },
            new Expression[]
            {
                Assign( a, Await( Call( typeof( Task ), nameof( Task.FromResult ), [typeof( int )], Constant( 6 ) ) ) ),
                Assign( b, Await( Call( typeof( Task ), nameof( Task.FromResult ), [typeof( int )], Constant( 4 ) ) ) ),
                Multiply( a, b )
            }
        );

        var lambda = Lambda<Func<Task<int>>>( block );
        var compiled = lambda.Compile( compiler );

        // Act
        var result = await compiled();

        // Assert
        Assert.AreEqual( 24, result );
    }

    // -----------------------------------------------------------------------
    // Return label — early exit via awaited value
    // -----------------------------------------------------------------------

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public async Task BlockAsync_ReturnLabel_ReturnsEarlyValue( CompilerType compiler )
    {
        // Arrange
        var returnLabel = Label( typeof( int ) );

        var block = BlockAsync(
            new Expression[]
            {
                IfThenElse(
                    Constant( true ),
                    Return( returnLabel, Await( Call( typeof( Task ), nameof( Task.FromResult ), [typeof( int )], Constant( 10 ) ) ) ),
                    Return( returnLabel, Await( Call( typeof( Task ), nameof( Task.FromResult ), [typeof( int )], Constant( 20 ) ) ) )
                ),
                Label( returnLabel, Constant( 30 ) )
            }
        );

        var lambda = Lambda<Func<Task<int>>>( block );
        var compiled = lambda.Compile( compiler );

        // Act
        var result = await compiled();

        // Assert
        Assert.AreEqual( 10, result );
    }

    // -----------------------------------------------------------------------
    // Await non-generic Task.CompletedTask (void async block)
    // -----------------------------------------------------------------------

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public async Task BlockAsync_AwaitCompletedTask_CompletesSuccessfully( CompilerType compiler )
    {
        // Arrange
        var block = BlockAsync(
            new Expression[] { Await( Constant( Task.CompletedTask, typeof( Task ) ) ) }
        );

        var lambda = Lambda<Func<Task>>( block );
        var compiled = lambda.Compile( compiler );

        // Act & Assert — should not throw
        await compiled();
    }

    // -----------------------------------------------------------------------
    // Await result used directly in Add without an intermediate variable
    // -----------------------------------------------------------------------

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public async Task BlockAsync_AwaitedValueInAdd_ReturnsSum( CompilerType compiler )
    {
        // Arrange — Add(Await(...), Constant) with no intermediate variable
        var block = BlockAsync(
            new Expression[]
            {
                Add(
                    Await( Call( typeof( Task ), nameof( Task.FromResult ), [typeof( int )], Constant( 72 ) ) ),
                    Constant( 5 )
                )
            }
        );

        var lambda = Lambda<Func<Task<int>>>( block );
        var compiled = lambda.Compile( compiler );

        // Act
        var result = await compiled();

        // Assert
        Assert.AreEqual( 77, result );
    }
}
