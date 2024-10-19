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

        // Condition: while (counter < 10)
        var condition = Expression.LessThan( counter, Expression.Constant( 10 ) );

        var whileExpr = ExpressionExtensions.While( condition, 

            // increment counter
            Expression.PostIncrementAssign( counter )
        );

        // Block to initialize the counter and execute the loop
        var block = Expression.Block(
            [counter],
            counterInit,
            whileExpr,
            counter
        );

        // Act
        var lambda = Expression.Lambda<Func<int>>( block ).Compile();
        var result = lambda();

        // Assert
        Assert.AreEqual( 10, result, "Loop should break when counter == 10." );
    }

    [TestMethod]
    public void WhileExpression_ShouldBreak()
    {
        // Arrange
        var counter = Expression.Variable( typeof( int ), "counter" );
        var counterInit = Expression.Assign( counter, Expression.Constant( 0 ) );

        // Condition: while (counter < 10)
        var condition = Expression.LessThan( counter, Expression.Constant( 10 ) );
        
        var whileExpr = ExpressionExtensions.While( condition, ( breakLabel, _ ) =>

            // if (counter == 5) break; else counter++
            Expression.IfThenElse(
                Expression.Equal( counter, Expression.Constant( 5 ) ),
                Expression.Break( breakLabel ),
                Expression.PostIncrementAssign( counter )
            )
        );

        // Block to initialize the counter and execute the loop
        var block = Expression.Block(
            [counter],
            counterInit,
            whileExpr,
            counter
        );

        // Act
        var lambda = Expression.Lambda<Func<int>>( block ).Compile();
        var result = lambda();

        // Assert
        Assert.AreEqual( 5, result, "Loop should break when counter == 5." );
    }
}
