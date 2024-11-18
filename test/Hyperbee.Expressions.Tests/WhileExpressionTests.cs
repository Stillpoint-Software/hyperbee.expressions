using System.Linq.Expressions;

namespace Hyperbee.Expressions.Tests;

[TestClass]
public class WhileExpressionTests
{
    [TestMethod]
    public void WhileExpression_ShouldBreak_WhenConditionMet()
    {
        // Arrange
        var counter = Expression.Variable( typeof( int ), "counter" );
        var counterInit = Expression.Assign( counter, Expression.Constant( 0 ) );

        var condition = Expression.LessThan( counter, Expression.Constant( 10 ) );

        var whileExpr = ExpressionExtensions.While( condition,
            Expression.PostIncrementAssign( counter )
        );

        var block = Expression.Block(
            [counter],
            counterInit,
            whileExpr,
            counter
        );

        // Act
        var lambda = Expression.Lambda<Func<int>>( block );
        var compiledLambda = lambda.Compile();

        var result = compiledLambda();

        // Assert
        Assert.AreEqual( 10, result, "Loop should break when counter == 10." );
    }

    [TestMethod]
    public void WhileExpression_ShouldBreak()
    {
        // Arrange
        var counter = Expression.Variable( typeof( int ), "counter" );
        var counterInit = Expression.Assign( counter, Expression.Constant( 0 ) );

        var condition = Expression.LessThan( counter, Expression.Constant( 10 ) );

        var whileExpr = ExpressionExtensions.While( condition, ( breakLabel, _ ) =>
            Expression.IfThenElse(
                Expression.Equal( counter, Expression.Constant( 5 ) ),
                Expression.Break( breakLabel ),
                Expression.PostIncrementAssign( counter )
            )
        );

        var block = Expression.Block(
            [counter],
            counterInit,
            whileExpr,
            counter
        );

        // Act
        var lambda = Expression.Lambda<Func<int>>( block );
        var compiledLambda = lambda.Compile();

        var result = compiledLambda();

        // Assert
        Assert.AreEqual( 5, result, "Loop should break when counter == 5." );
    }
}
