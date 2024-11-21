using static System.Linq.Expressions.Expression;
using static Hyperbee.Expressions.ExpressionExtensions;

namespace Hyperbee.Expressions.Tests;

[TestClass]
public class ForExpressionTests
{
    [TestMethod]
    public void ForExpression_ShouldLoopCorrectly()
    {
        // Arrange
        var counter = Variable( typeof( int ), "counter" );
        var counterInit = Assign( counter, Constant( 0 ) );

        var condition = LessThan( counter, Constant( 5 ) );
        var iteration = PostIncrementAssign( counter );

        var writeLineMethod = typeof( Console ).GetMethod( "WriteLine", [typeof( int )] );
        var body = Call( writeLineMethod!, counter );

        var forExpr = For( counterInit, condition, iteration, body );

        // Wrap in a block to capture the counter value
        var block = Block(
            [counter],
            forExpr,
            counter // Return counter
        );

        // Act
        var lambda = Lambda<Func<int>>( block );
        var compiledLambda = lambda.Compile();

        var result = compiledLambda();

        // Assert
        Assert.AreEqual( 5, result, "Counter should be 5 after the loop finishes." );
    }

    [TestMethod]
    public void ForExpression_ShouldSupportCustomBreak()
    {
        // Arrange
        var writeLine = typeof( Console ).GetMethod( "WriteLine", [typeof( int )] )!;

        var counter = Variable( typeof( int ), "counter" );
        var counterInit = Assign( counter, Constant( 0 ) );

        var condition = LessThan( counter, Constant( 10 ) );
        var iteration = PostIncrementAssign( counter );

        var forExpr = For( counterInit, condition, iteration, ( breakLabel, continueLabel ) =>
            IfThenElse(
                Equal( counter, Constant( 5 ) ),
                Break( breakLabel ), // break when counter == 5
                Call( writeLine, counter )
        ) );

        var block = Block(
            [counter],
            forExpr,
            counter
        );

        // Act
        var lambda = Lambda<Func<int>>( block );
        var compiledLambda = lambda.Compile();

        var result = compiledLambda();

        // Assert
        Assert.AreEqual( 5, result, "Loop should break when counter reaches 5." );
    }
}
