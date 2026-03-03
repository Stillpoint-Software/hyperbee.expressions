using System.Linq.Expressions;
using Hyperbee.Expressions.Compiler.Tests.TestSupport;
using static System.Linq.Expressions.Expression;
using static Hyperbee.Expressions.ExpressionExtensions;

namespace Hyperbee.Expressions.Compiler.Tests.Integration;

/// <summary>
/// Integration tests verifying loop (Loop / break / continue) patterns inside BlockAsync
/// when the state machine MoveNext is compiled by HEC.
/// </summary>
[TestClass]
public class BlockAsyncLoopTests
{
    // -----------------------------------------------------------------------
    // Await before break — loop exits after one iteration
    // -----------------------------------------------------------------------

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public async Task BlockAsync_AwaitBeforeBreak_BreaksAfterOneIteration( CompilerType compiler )
    {
        // Arrange
        var count = Variable( typeof( int ), "count" );
        var breakLabel = Label( "breakLabel" );

        var block = BlockAsync(
            new[] { count },
            new Expression[]
            {
                Assign( count, Constant( 0 ) ),
                Loop(
                    Block(
                        Await( Call( typeof( Task ), nameof( Task.FromResult ), [typeof( int )], Constant( 1 ) ) ),
                        IfThen(
                            Equal( count, Constant( 1 ) ),
                            Break( breakLabel )
                        ),
                        Assign( count, Add( count, Constant( 1 ) ) )
                    ),
                    breakLabel,
                    null
                ),
                count
            }
        );

        var lambda = Lambda<Func<Task<int>>>( block );
        var compiled = lambda.Compile( compiler );

        // Act
        var result = await compiled();

        // Assert — loop breaks when count == 1
        Assert.AreEqual( 1, result );
    }

    // -----------------------------------------------------------------------
    // Await after loop exits
    // -----------------------------------------------------------------------

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public async Task BlockAsync_AwaitAfterLoop_ReturnsCountAfterAwait( CompilerType compiler )
    {
        // Arrange
        var count = Variable( typeof( int ), "count" );
        var breakLabel = Label( "breakLabel" );

        var block = BlockAsync(
            new[] { count },
            new Expression[]
            {
                Assign( count, Constant( 0 ) ),
                Loop(
                    Block(
                        Assign( count, Add( count, Constant( 1 ) ) ),
                        IfThen(
                            Equal( count, Constant( 2 ) ),
                            Break( breakLabel )
                        )
                    ),
                    breakLabel,
                    null
                ),
                Await( Call( typeof( Task ), nameof( Task.FromResult ), [typeof( int )], Constant( 5 ) ) ),
                count
            }
        );

        var lambda = Lambda<Func<Task<int>>>( block );
        var compiled = lambda.Compile( compiler );

        // Act
        var result = await compiled();

        // Assert — loop breaks at count 2, then await runs, result is count (2)
        Assert.AreEqual( 2, result );
    }

    // -----------------------------------------------------------------------
    // Await before continue — continues to next iteration
    // -----------------------------------------------------------------------

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public async Task BlockAsync_AwaitBeforeContinue_ProcessesIterations( CompilerType compiler )
    {
        // Arrange
        var count = Variable( typeof( int ), "count" );
        var continueLabel = Label( "continueLabel" );
        var breakLabel = Label( "breakLabel" );

        var block = BlockAsync(
            new[] { count },
            new Expression[]
            {
                Assign( count, Constant( 0 ) ),
                Loop(
                    Block(
                        Assign( count, Add( count, Constant( 1 ) ) ),
                        Await( Call( typeof( Task ), nameof( Task.FromResult ), [typeof( int )], Constant( 1 ) ) ),
                        IfThen(
                            LessThan( count, Constant( 2 ) ),
                            Continue( continueLabel )
                        ),
                        Break( breakLabel )
                    ),
                    breakLabel,
                    continueLabel
                ),
                count
            }
        );

        var lambda = Lambda<Func<Task<int>>>( block );
        var compiled = lambda.Compile( compiler );

        // Act
        var result = await compiled();

        // Assert — continues when count < 2, breaks when count == 2
        Assert.AreEqual( 2, result );
    }

    // -----------------------------------------------------------------------
    // Await after continue (skipped on first iteration)
    // -----------------------------------------------------------------------

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public async Task BlockAsync_AwaitAfterContinue_SkipsOnFirstIteration( CompilerType compiler )
    {
        // Arrange
        var count = Variable( typeof( int ), "count" );
        var continueLabel = Label( "continueLabel" );
        var breakLabel = Label( "breakLabel" );

        var block = BlockAsync(
            new[] { count },
            new Expression[]
            {
                Assign( count, Constant( 0 ) ),
                Loop(
                    Block(
                        Assign( count, Add( count, Constant( 1 ) ) ),
                        IfThen(
                            Equal( count, Constant( 1 ) ),
                            Continue( continueLabel )
                        ),
                        Await( Call( typeof( Task ), nameof( Task.FromResult ), [typeof( int )], Constant( 3 ) ) ),
                        Break( breakLabel )
                    ),
                    breakLabel,
                    continueLabel
                ),
                count
            }
        );

        var lambda = Lambda<Func<Task<int>>>( block );
        var compiled = lambda.Compile( compiler );

        // Act
        var result = await compiled();

        // Assert — await skipped on iteration 1, executed on iteration 2; count = 2
        Assert.AreEqual( 2, result );
    }

    // -----------------------------------------------------------------------
    // Multiple awaits inside loop body
    // -----------------------------------------------------------------------

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public async Task BlockAsync_MultipleAwaitsInLoop_AllExecute( CompilerType compiler )
    {
        // Arrange
        var count = Variable( typeof( int ), "count" );
        var breakLabel = Label( "breakLabel" );

        var block = BlockAsync(
            new[] { count },
            new Expression[]
            {
                Assign( count, Constant( 0 ) ),
                Loop(
                    Block(
                        Await( Call( typeof( Task ), nameof( Task.FromResult ), [typeof( int )], Constant( 1 ) ) ),
                        Assign( count, Add( count, Constant( 1 ) ) ),
                        Await( Call( typeof( Task ), nameof( Task.FromResult ), [typeof( int )], Constant( 3 ) ) ),
                        Break( breakLabel )
                    ),
                    breakLabel,
                    null
                ),
                count
            }
        );

        var lambda = Lambda<Func<Task<int>>>( block );
        var compiled = lambda.Compile( compiler );

        // Act
        var result = await compiled();

        // Assert — single iteration with two awaits, count incremented once
        Assert.AreEqual( 1, result );
    }

    // -----------------------------------------------------------------------
    // Loop with both break and continue labels, await on each iteration
    // -----------------------------------------------------------------------

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public async Task BlockAsync_BreakAndContinueLabels_BreaksAtThree( CompilerType compiler )
    {
        // Arrange
        var count = Variable( typeof( int ), "count" );
        var breakLabel = Label( "breakLabel" );
        var continueLabel = Label( "continueLabel" );

        var block = BlockAsync(
            new[] { count },
            new Expression[]
            {
                Assign( count, Constant( 0 ) ),
                Loop(
                    Block(
                        Assign( count, Add( count, Constant( 1 ) ) ),
                        Await( Call( typeof( Task ), nameof( Task.FromResult ), [typeof( int )], Constant( 1 ) ) ),
                        IfThen(
                            Equal( count, Constant( 3 ) ),
                            Break( breakLabel )
                        ),
                        IfThen(
                            LessThan( count, Constant( 5 ) ),
                            Continue( continueLabel )
                        )
                    ),
                    breakLabel,
                    continueLabel
                ),
                count
            }
        );

        var lambda = Lambda<Func<Task<int>>>( block );
        var compiled = lambda.Compile( compiler );

        // Act
        var result = await compiled();

        // Assert — loop breaks when count reaches 3
        Assert.AreEqual( 3, result );
    }
}
