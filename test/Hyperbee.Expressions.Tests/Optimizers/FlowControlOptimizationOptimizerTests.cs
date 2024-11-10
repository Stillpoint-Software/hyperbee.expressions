using System.Linq.Expressions;
using Hyperbee.Expressions.Optimizers;

namespace Hyperbee.Expressions.Tests.Optimizers;

[TestClass]
public class FlowControlOptimizationOptimizerTests
{
    [TestMethod]
    public void FlowControlOptimization_ShouldRemoveUnreferencedLabel()
    {
        // Arrange
        var unusedLabel = Expression.Label( "unused" );
        var referenceExpr = Expression.Lambda<Func<int>>(
            Expression.Block(
                Expression.Label( unusedLabel ),
                Expression.Constant( 5 )
            )
        );

        var optimizedExpr = new GotoLabelTryOptimizer().Optimize( referenceExpr );

        // Act
        var reference = referenceExpr.Compile();
        var result = reference();

        var optimized = optimizedExpr.Compile();
        var comparand = optimized();

        // Assert
        Assert.AreEqual( 5, result );
        Assert.AreEqual( 5, comparand );
    }

    [TestMethod]
    public void FlowControlOptimization_ShouldSimplifyEmptyTryCatch()
    {
        // Arrange
        var referenceExpr = Expression.Lambda<Func<int>>(
            Expression.TryCatch(
                Expression.Constant( 5 ),
                Expression.Catch( Expression.Parameter( typeof( Exception ), "ex" ), Expression.Constant( 10 ) )
            )
        );

        var optimizedExpr = new GotoLabelTryOptimizer().Optimize( referenceExpr );

        // Act
        var reference = referenceExpr.Compile();
        var result = reference();

        var optimized = optimizedExpr.Compile();
        var comparand = optimized();

        // Assert
        Assert.AreEqual( 5, result );
        Assert.AreEqual( 5, comparand );
    }
}
