using System.Linq.Expressions;
using Hyperbee.Expressions.Compiler.Tests.TestSupport;
using static System.Linq.Expressions.Expression;
using static Hyperbee.Expressions.ExpressionExtensions;

namespace Hyperbee.Expressions.Compiler.Tests.Integration;

/// <summary>
/// Integration tests verifying conditional (IfThen / Condition) patterns inside BlockAsync
/// when the state machine MoveNext is compiled by HEC.
/// </summary>
[TestClass]
public class BlockAsyncConditionalTests
{
    // -----------------------------------------------------------------------
    // IfThen with an awaited condition — void result
    // -----------------------------------------------------------------------

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public async Task BlockAsync_IfThenAwaitedCondition_VoidResult( CompilerType compiler )
    {
        // Arrange
        var block = BlockAsync(
            new Expression[]
            {
                IfThen(
                    Await( Call( typeof( Task ), nameof( Task.FromResult ), [typeof( bool )], Constant( true ) ) ),
                    Constant( 1 )
                )
            }
        );

        var lambda = Lambda<Func<Task>>( block );
        var compiled = lambda.Compile( compiler );

        // Act & Assert — should complete without throwing
        await compiled();
    }

    // -----------------------------------------------------------------------
    // Condition: constant test, await in true branch
    // -----------------------------------------------------------------------

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public async Task BlockAsync_ConditionalTrueBranchAwaited_ReturnsAwaitedValue( CompilerType compiler )
    {
        // Arrange
        var result = Variable( typeof( int ), "result" );

        var block = BlockAsync(
            new[] { result },
            new Expression[]
            {
                Assign( result,
                    Condition(
                        Constant( true ),
                        Await( Call( typeof( Task ), nameof( Task.FromResult ), [typeof( int )], Constant( 1 ) ) ),
                        Constant( 0 )
                    )
                ),
                result
            }
        );

        var lambda = Lambda<Func<Task<int>>>( block );
        var compiled = lambda.Compile( compiler );

        // Act
        var value = await compiled();

        // Assert
        Assert.AreEqual( 1, value );
    }

    // -----------------------------------------------------------------------
    // Condition: constant test (false), await in false branch
    // -----------------------------------------------------------------------

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public async Task BlockAsync_ConditionalFalseBranchAwaited_ReturnsAwaitedValue( CompilerType compiler )
    {
        // Arrange
        var block = BlockAsync(
            new Expression[]
            {
                Condition(
                    Constant( false ),
                    Constant( 0 ),
                    Await( Call( typeof( Task ), nameof( Task.FromResult ), [typeof( int )], Constant( 2 ) ) )
                )
            }
        );

        var lambda = Lambda<Func<Task<int>>>( block );
        var compiled = lambda.Compile( compiler );

        // Act
        var result = await compiled();

        // Assert
        Assert.AreEqual( 2, result );
    }

    // -----------------------------------------------------------------------
    // Condition: awaited boolean used as the test itself
    // -----------------------------------------------------------------------

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public async Task BlockAsync_AwaitedConditionTest_SelectsTrueBranch( CompilerType compiler )
    {
        // Arrange
        var block = BlockAsync(
            new Expression[]
            {
                Condition(
                    Await( Call( typeof( Task ), nameof( Task.FromResult ), [typeof( bool )], Constant( true ) ) ),
                    Constant( 1 ),
                    Constant( 0 )
                )
            }
        );

        var lambda = Lambda<Func<Task<int>>>( block );
        var compiled = lambda.Compile( compiler );

        // Act
        var result = await compiled();

        // Assert
        Assert.AreEqual( 1, result );
    }

    // -----------------------------------------------------------------------
    // Both branches awaited — true condition selects true branch
    // -----------------------------------------------------------------------

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public async Task BlockAsync_BothBranchesAwaited_TrueCondition_SelectsTrue( CompilerType compiler )
    {
        // Arrange
        var block = BlockAsync(
            new Expression[]
            {
                Condition(
                    Constant( true ),
                    Await( Call( typeof( Task ), nameof( Task.FromResult ), [typeof( int )], Constant( 10 ) ) ),
                    Await( Call( typeof( Task ), nameof( Task.FromResult ), [typeof( int )], Constant( 20 ) ) )
                )
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
    // Both branches awaited — false condition selects false branch
    // -----------------------------------------------------------------------

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public async Task BlockAsync_BothBranchesAwaited_FalseCondition_SelectsFalse( CompilerType compiler )
    {
        // Arrange
        var block = BlockAsync(
            new Expression[]
            {
                Condition(
                    Constant( false ),
                    Await( Call( typeof( Task ), nameof( Task.FromResult ), [typeof( int )], Constant( 10 ) ) ),
                    Await( Call( typeof( Task ), nameof( Task.FromResult ), [typeof( int )], Constant( 20 ) ) )
                )
            }
        );

        var lambda = Lambda<Func<Task<int>>>( block );
        var compiled = lambda.Compile( compiler );

        // Act
        var result = await compiled();

        // Assert
        Assert.AreEqual( 20, result );
    }

    // -----------------------------------------------------------------------
    // Await before and after a non-async conditional
    // -----------------------------------------------------------------------

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public async Task BlockAsync_AwaitBeforeAndAfterConditional_ReturnsLast( CompilerType compiler )
    {
        // Arrange
        var block = BlockAsync(
            new Expression[]
            {
                Await( Call( typeof( Task ), nameof( Task.FromResult ), [typeof( int )], Constant( 5 ) ) ),
                Condition( Constant( true ), Constant( 10 ), Constant( 0 ) ),
                Await( Call( typeof( Task ), nameof( Task.FromResult ), [typeof( int )], Constant( 15 ) ) )
            }
        );

        var lambda = Lambda<Func<Task<int>>>( block );
        var compiled = lambda.Compile( compiler );

        // Act
        var result = await compiled();

        // Assert
        Assert.AreEqual( 15, result );
    }

    // -----------------------------------------------------------------------
    // Two sequential conditionals, each with awaited branches
    // -----------------------------------------------------------------------

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public async Task BlockAsync_SequentialConditionalsWithAwaits_ReturnsSecondFalseBranch( CompilerType compiler )
    {
        // Arrange — first conditional takes true branch (10), second takes false branch (2)
        var block = BlockAsync(
            new Expression[]
            {
                Condition(
                    Constant( true ),
                    Await( Call( typeof( Task ), nameof( Task.FromResult ), [typeof( int )], Constant( 10 ) ) ),
                    Await( Call( typeof( Task ), nameof( Task.FromResult ), [typeof( int )], Constant( 5 ) ) )
                ),
                Condition(
                    Constant( false ),
                    Await( Call( typeof( Task ), nameof( Task.FromResult ), [typeof( int )], Constant( 1 ) ) ),
                    Await( Call( typeof( Task ), nameof( Task.FromResult ), [typeof( int )], Constant( 2 ) ) )
                )
            }
        );

        var lambda = Lambda<Func<Task<int>>>( block );
        var compiled = lambda.Compile( compiler );

        // Act
        var result = await compiled();

        // Assert
        Assert.AreEqual( 2, result );
    }

    // -----------------------------------------------------------------------
    // Nested conditionals — outer true selects inner, inner false branch
    // -----------------------------------------------------------------------

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public async Task BlockAsync_NestedConditionals_ReturnsInnerFalseBranch( CompilerType compiler )
    {
        // Arrange — outer true → inner; inner false → 10
        var block = BlockAsync(
            new Expression[]
            {
                Condition(
                    Constant( true ),
                    Condition(
                        Constant( false ),
                        Await( Call( typeof( Task ), nameof( Task.FromResult ), [typeof( int )], Constant( 5 ) ) ),
                        Await( Call( typeof( Task ), nameof( Task.FromResult ), [typeof( int )], Constant( 10 ) ) )
                    ),
                    Constant( 0 )
                )
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
    // Await the Task returned by a Condition expression
    // -----------------------------------------------------------------------

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public async Task BlockAsync_AwaitConditionalTask_ReturnsTrueBranchTask( CompilerType compiler )
    {
        // Arrange — condition selects which Task<int> to produce, then that task is awaited
        var block = BlockAsync(
            new Expression[]
            {
                Await(
                    Condition(
                        Constant( true ),
                        Call( typeof( Task ), nameof( Task.FromResult ), [typeof( int )], Constant( 15 ) ),
                        Call( typeof( Task ), nameof( Task.FromResult ), [typeof( int )], Constant( 20 ) )
                    )
                )
            }
        );

        var lambda = Lambda<Func<Task<int>>>( block );
        var compiled = lambda.Compile( compiler );

        // Act
        var result = await compiled();

        // Assert
        Assert.AreEqual( 15, result );
    }
}
