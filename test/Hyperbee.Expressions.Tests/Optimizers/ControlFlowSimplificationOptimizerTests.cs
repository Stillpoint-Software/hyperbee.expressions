using System.Linq.Expressions;
using Hyperbee.Expressions.Optimizers;

namespace Hyperbee.Expressions.Tests.Optimizers;

[TestClass]
public class ControlFlowSimplificationOptimizerTests
{
    [TestMethod]
    public void ControlFlowSimplification_ShouldRemoveUnreachableCode()
    {
        // Arrange
        var referenceExpr = Expression.Lambda<Func<int>>(
            Expression.Block(
                new Expression[]
                {
                    Expression.Constant( 10 ), Expression.Condition(
                        Expression.Constant( true ),
                        Expression.Constant( 20 ),
                        Expression.Constant( 30 )
                    )
                }
            )
        );

        var optimizedExpr = new ControlFlowSimplificationOptimizer().Optimize(referenceExpr);

        // Act
        var reference = referenceExpr.Compile();
        var result = reference();

        var optimized = optimizedExpr.Compile();
        var comparand = optimized();

        // Assert
        Assert.AreEqual(20, result);
        Assert.AreEqual(20, comparand);
    }

    [TestMethod]
    public void ControlFlowSimplification_ShouldRemoveInfiniteLoop()
    {
        // Arrange
        var loopLabel = Expression.Label( typeof(void) );
        var referenceExpr = Expression.Lambda<Func<int>>(
            Expression.Block(
                Expression.Loop(
                    Expression.IfThenElse(
                        Expression.Constant( false ),
                        Expression.Break( loopLabel ),
                        Expression.Default( typeof(void) )
                    ),
                    loopLabel
                ),
                Expression.Constant( 5 )
            )
        );

        // Act
        var optimizedExpr = new ControlFlowSimplificationOptimizer().Optimize( referenceExpr );
        var optimized = optimizedExpr.Compile();
        var result = optimized();

        // Assert
        Assert.AreEqual( 5, result );
    }
}
