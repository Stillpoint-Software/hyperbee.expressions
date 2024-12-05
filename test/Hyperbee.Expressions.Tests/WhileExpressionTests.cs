using static System.Linq.Expressions.Expression;
using static Hyperbee.Expressions.ExpressionExtensions;

namespace Hyperbee.Expressions.Tests;

[TestClass]
public class WhileExpressionTests
{
    [TestMethod]
    public void WhileExpression_ShouldBreak_WhenConditionMet()
    {
        // Arrange
        var counter = Variable( typeof( int ), "counter" );
        var counterInit = Assign( counter, Constant( 0 ) );

        var condition = LessThan( counter, Constant( 10 ) );

        var whileExpr = While( condition,
            PostIncrementAssign( counter )
        );

        var block = Block(
            [counter],
            counterInit,
            whileExpr,
            counter
        );

        // Act
        var lambda = Lambda<Func<int>>( block );
        var compiledLambda = lambda.Compile();

        var result = compiledLambda();

        // Assert
        Assert.AreEqual( 10, result, "Loop should break when counter == 10." );
    }

    [TestMethod]
    public void WhileExpression_ShouldBreak()
    {
        // Arrange
        var counter = Variable( typeof( int ), "counter" );
        var counterInit = Assign( counter, Constant( 0 ) );

        var condition = LessThan( counter, Constant( 10 ) );

        var whileExpr = While( condition, ( breakLabel, _ ) =>
            IfThenElse(
                Equal( counter, Constant( 5 ) ),
                Break( breakLabel ),
                PostIncrementAssign( counter )
            )
        );

        var block = Block(
            [counter],
            counterInit,
            whileExpr,
            counter
        );

        // Act
        var lambda = Lambda<Func<int>>( block );
        var compiledLambda = lambda.Compile();

        var result = compiledLambda();

        // Assert
        Assert.AreEqual( 5, result, "Loop should break when counter == 5." );
    }
}
