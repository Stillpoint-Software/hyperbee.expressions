using System.Linq.Expressions;
using Hyperbee.Expressions.Optimizers;

namespace Hyperbee.Expressions.Tests.Optimizers;

[TestClass]
public class FlowControlOptimizerTests
{
    [TestMethod]
    public void FlowControl_ShouldRemoveUnreachableCode()
    {
        // Before: .Block(.Constant(1), .Constant(2))
        // After:  .Constant(1)

        // Arrange
        var block = Expression.Block( Expression.Constant( 1 ), Expression.Constant( 2 ) );
        var optimizer = new FlowControlOptimizer();

        // Act
        var result = optimizer.Optimize( block );
        var value = ((ConstantExpression) ((BlockExpression) result).Expressions[0]).Value;

        // Assert
        Assert.AreEqual( 1, value );
    }

    [TestMethod]
    public void FlowControl_ShouldSimplifyEmptyTryCatch()
    {
        // Before: .TryCatch(.Empty(), .Catch(...))
        // After:  .Empty()

        // Arrange
        var tryCatch = Expression.TryCatch(
            Expression.Empty(),
            Expression.Catch( Expression.Parameter( typeof( Exception ) ), Expression.Empty() )
        );
        var optimizer = new FlowControlOptimizer();

        // Act
        var result = optimizer.Optimize( tryCatch );

        // Assert
        Assert.IsInstanceOfType( result, typeof( DefaultExpression ) );
    }

    [TestMethod]
    public void FlowControl_ShouldRemoveInfiniteLoop()
    {
        // Before: .Loop(.Constant(1))
        // After:  .Empty()

        // Arrange
        var loop = Expression.Loop( Expression.Constant( 1 ) );
        var optimizer = new FlowControlOptimizer();

        // Act
        var result = optimizer.Optimize( loop );

        // Assert
        Assert.IsInstanceOfType( result, typeof( DefaultExpression ) );
    }

    [TestMethod]
    public void FlowControl_ShouldSimplifyNestedConditionalExpression()
    {
        // Before: .Block(.IfThenElse(.Constant(true), .IfThenElse(.Constant(false), .Break(), .Constant("B"))))
        // After:  .Constant("B")

        // Arrange
        var innerCondition = Expression.IfThenElse(
            Expression.Constant( false ),
            Expression.Break( Expression.Label() ),
            Expression.Constant( "B" )
        );
        var outerCondition = Expression.IfThenElse(
            Expression.Constant( true ),
            innerCondition,
            Expression.Constant( "C" )
        );
        var block = Expression.Block( outerCondition );
        var optimizer = new FlowControlOptimizer();

        // Act
        var result = optimizer.Optimize( block );
        var value = ((ConstantExpression) result).Value;

        // Assert
        Assert.AreEqual( "B", value );
    }

    [TestMethod]
    public void FlowControl_ShouldSimplifyLoopWithComplexCondition()
    {
        // Before: .Loop(.IfThenElse(.Constant(false), .Break(), .Constant(1)))
        // After:  .Empty()

        // Arrange
        var loopCondition = Expression.IfThenElse(
            Expression.Constant( false ),
            Expression.Break( Expression.Label() ),
            Expression.Constant( 1 )
        );
        var loop = Expression.Loop( loopCondition );
        var optimizer = new FlowControlOptimizer();

        // Act
        var result = optimizer.Optimize( loop );

        // Assert
        Assert.IsInstanceOfType( result, typeof( DefaultExpression ) );
    }
}
