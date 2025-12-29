using Hyperbee.Expressions.Tests.TestSupport;
using static System.Linq.Expressions.Expression;

using static Hyperbee.Expressions.ExpressionExtensions;

namespace Hyperbee.Expressions.Tests;

[TestClass]
public class BlockYieldLoopTests
{
    [TestMethod]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Interpret )]
    public void YieldBlock_ShouldYieldSuccessfully_WithYieldBeforeBreak( CompilerType compiler )
    {
        // Arrange: Await before break in a loop
        var loopCount = Variable( typeof( int ), "count" );
        var breakLabel = Label( "breakLabel" );
        var block = BlockEnumerable(
            [loopCount],
            Assign( loopCount, Constant( 0 ) ),
            Loop(
                Block(
                    YieldReturn( Constant( 1 ) ), // Await before break
                    IfThen(
                        Equal( loopCount, Constant( 1 ) ),
                        Break( breakLabel )
                    ),
                    Assign( loopCount, Add( loopCount, Constant( 1 ) ) )
                ),
                breakLabel,
                null // No continue label
            ),
            loopCount
        );
        var lambda = Lambda<Func<IEnumerable<int>>>( block );
        var compiledLambda = lambda.Compile( compiler );

        // Act
        var result = compiledLambda();

        // Assert
        Assert.AreEqual( 1, result.First() ); // Loop should break after 1 iteration
    }

    [TestMethod]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Interpret )]
    public void YieldBlock_ShouldYieldSuccessfully_WithYieldAfterLoop( CompilerType compiler )
    {
        // Arrange: Yield after break in a loop
        var loopCount = Variable( typeof( int ), "count" );
        var breakLabel = Label( "breakLabel" );

        var block = BlockEnumerable(
            [loopCount],
            Assign( loopCount, Constant( 0 ) ),
            Loop(
                Block(
                    Assign( loopCount, Add( loopCount, Constant( 1 ) ) ),
                    IfThen(
                        Equal( loopCount, Constant( 2 ) ),
                        Break( breakLabel )
                    )
                ),
                breakLabel,
                null
            ),
            YieldReturn( Constant( 5 ) ), // yield after loop ends
            loopCount
        );
        var lambda = Lambda<Func<IEnumerable<int>>>( block );
        var compiledLambda = lambda.Compile( compiler );

        // Act
        var result = compiledLambda();

        // Assert
        Assert.AreEqual( 5, result.First() ); // Loop breaks after 2 iterations
    }

    [TestMethod]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Interpret )]
    public void YieldBlock_ShouldYieldSuccessfully_WithOutLoopBreakOrContinue( CompilerType compiler )
    {
        // Arrange: Yield before continue in a loop
        var loopCount = Variable( typeof( int ), "count" );

        var block = BlockEnumerable(
            [loopCount],
            Assign( loopCount, Constant( 0 ) ),
            Loop(
                Block(
                    Assign( loopCount, Add( loopCount, Constant( 1 ) ) ),
                    IfThenElse(
                        LessThan( loopCount, Constant( 3 ) ),
                        YieldReturn( loopCount ),
                        YieldBreak()
                    )
                )
            ),
            loopCount
        );
        var lambda = Lambda<Func<IEnumerable<int>>>( block );
        var compiledLambda = lambda.Compile( compiler );

        // Act
        var result = compiledLambda().ToArray();

        // Assert
        Assert.HasCount( 2, result ); // Loop continues past the first iteration
        Assert.AreEqual( 1, result[0] );
        Assert.AreEqual( 2, result[1] );
    }

    [TestMethod]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Interpret )]
    public void YieldBlock_ShouldYieldSuccessfully_WithYieldBeforeContinue( CompilerType compiler )
    {
        // Arrange: Yield before continue in a loop
        var loopCount = Variable( typeof( int ), "count" );
        var continueLabel = Label( "continueLabel" );
        var breakLabel = Label( "breakLabel" );
        var block = BlockEnumerable(
            [loopCount],
            Assign( loopCount, Constant( 0 ) ),
            Loop(
                Block(
                    Assign( loopCount, Add( loopCount, Constant( 1 ) ) ),
                    YieldReturn( loopCount ),
                    IfThen(
                        LessThan( loopCount, Constant( 2 ) ),
                        Continue( continueLabel )
                    ),
                    Break( breakLabel )
                ),
                breakLabel,
                continueLabel
            ),
            loopCount
        );
        var lambda = Lambda<Func<IEnumerable<int>>>( block );
        var compiledLambda = lambda.Compile( compiler );

        // Act
        var result = compiledLambda().ToArray();

        // Assert
        Assert.HasCount( 2, result ); // Loop continues past the first iteration
        Assert.AreEqual( 1, result[0] );
        Assert.AreEqual( 2, result[1] );
    }

    [TestMethod]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Interpret )]
    public void YieldBlock_ShouldYieldSuccessfully_WithYieldAfterContinue( CompilerType compiler )
    {
        // Arrange: yield after continue in a loop
        var loopCount = Variable( typeof( int ), "count" );
        var continueLabel = Label( "continueLabel" );
        var breakLabel = Label( "breakLabel" );
        var block = BlockEnumerable(
            [loopCount],
            Assign( loopCount, Constant( 0 ) ),
            Loop(
                Block(
                    Assign( loopCount, Add( loopCount, Constant( 1 ) ) ),
                    IfThen(
                        Equal( loopCount, Constant( 1 ) ),
                        Continue( continueLabel )
                    ),
                    YieldReturn( loopCount ),
                    Break( breakLabel )
                ),
                breakLabel,
                continueLabel
            ),
            loopCount
        );
        var lambda = Lambda<Func<IEnumerable<int>>>( block );
        var compiledLambda = lambda.Compile( compiler );

        // Act
        var result = compiledLambda().ToArray();

        // Assert
        Assert.HasCount( 1, result ); // Loop continues past the first iteration
        Assert.AreEqual( 2, result[0] );
    }

    [TestMethod]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Interpret )]
    public void YieldBlock_ShouldYieldSuccessfully_WithMultipleYieldInLoop( CompilerType compiler )
    {
        // Arrange: Multiple awaits in a loop
        var loopCount = Variable( typeof( int ), "count" );
        var breakLabel = Label( "breakLabel" );
        var block = BlockEnumerable(
            [loopCount],
            Assign( loopCount, Constant( 0 ) ),
            Loop(
                Block(
                    YieldReturn( loopCount ),
                    Assign( loopCount, Add( loopCount, Constant( 1 ) ) ),
                    YieldReturn( loopCount ),
                    Break( breakLabel )
                ),
                breakLabel,
                null // No continue label
            ),
            loopCount
        );
        var lambda = Lambda<Func<IEnumerable<int>>>( block );
        var compiledLambda = lambda.Compile( compiler );

        // Act
        var result = compiledLambda().ToArray();

        // Assert
        Assert.HasCount( 2, result );
        Assert.AreEqual( 0, result[0] );
        Assert.AreEqual( 1, result[1] );
    }

    [TestMethod]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Interpret )]
    public void YieldBlock_ShouldYieldSuccessfully_WithBreakAndContinueLabels( CompilerType compiler )
    {
        // Arrange: Use both breakLabel and continueLabel in the loop
        var loopCount = Variable( typeof( int ), "count" );
        var breakLabel = Label( "breakLabel" );
        var continueLabel = Label( "continueLabel" );

        var block = BlockEnumerable(
            [loopCount],
            Assign( loopCount, Constant( 0 ) ),
            Loop(
                Block(
                    Assign( loopCount, Add( loopCount, Constant( 1 ) ) ),
                    YieldReturn( loopCount ),
                    IfThen(
                        Equal( loopCount, Constant( 3 ) ),
                        Break( breakLabel )
                    ),
                    IfThen(
                        LessThan( loopCount, Constant( 5 ) ),
                        Continue( continueLabel )
                    )
                ),
                breakLabel,
                continueLabel
            ),
            loopCount
        );

        var lambda = Lambda<Func<IEnumerable<int>>>( block );
        var compiledLambda = lambda.Compile( compiler );

        // Act
        var result = compiledLambda().ToArray();

        // Assert
        Assert.HasCount( 3, result );
        Assert.AreEqual( 1, result[0] );
        Assert.AreEqual( 2, result[1] );
        Assert.AreEqual( 3, result[2] );
    }
}
