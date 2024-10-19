using System.Linq.Expressions;

namespace Hyperbee.Expressions.Tests;

[TestClass]
public class ForExpressionTests
{
    [TestMethod]
    public void ForExpression_ShouldLoopCorrectly()
    {
        // Arrange
        var counter = Expression.Variable( typeof(int), "counter" );
        var counterInit = Expression.Assign( counter, Expression.Constant( 0 ) );

        var condition = Expression.LessThan( counter, Expression.Constant( 5 ) );
        var iteration = Expression.PostIncrementAssign( counter );

        var writeLineMethod = typeof(Console).GetMethod( "WriteLine", [typeof(int)] );
        var body = Expression.Call( writeLineMethod!, counter );

        var forExpr = ExpressionExtensions.For( counterInit, condition, iteration, body );

        // Wrap in a block to capture the counter value
        var block = Expression.Block(
            [counter],
            forExpr, 
            counter // Return counter
        );

        // Act
        var lambda = Expression.Lambda<Func<int>>( block ).Compile();
        var result = lambda();

        // Assert
        Assert.AreEqual( 5, result, "Counter should be 5 after the loop finishes." );
    }

    [TestMethod]
    public void ForExpression_ShouldSupportCustomBreak()
    {
        // Arrange

        var writeLine = typeof(Console).GetMethod( "WriteLine", [typeof(int)] )!;

        var counter = Expression.Variable( typeof(int), "counter" );
        var counterInit = Expression.Assign( counter, Expression.Constant( 0 ) );

        var condition = Expression.LessThan( counter, Expression.Constant( 10 ) );
        var iteration = Expression.PostIncrementAssign( counter );

        var forExpr = ExpressionExtensions.For( counterInit, condition, iteration, ( breakLabel, continueLabel ) => 
            Expression.IfThenElse( 
                Expression.Equal( counter, Expression.Constant( 5 ) ), 
                Expression.Break( breakLabel ), // Use break label when counter == 5
                Expression.Call( writeLine, counter )
        ) );

        var block = Expression.Block(
            [counter],
            forExpr,
            counter // Return counter
        );

        // Act
        var lambda = Expression.Lambda<Func<int>>( block ).Compile();
        var result = lambda();

        // Assert
        Assert.AreEqual( 5, result, "Loop should break when counter reaches 5." );
    }
}
