using System.Linq.Expressions;
using Hyperbee.Expressions.Optimizers;

namespace Hyperbee.Expressions.Tests.Optimizers;

[TestClass]
public class AccessSimplificationOptimizerTests
{
    [TestMethod]
    public void AccessSimplification_ShouldEliminateNullCoalesce()
    {
        // Arrange
        var referenceExpr = Expression.Lambda<Func<int>>(
            Expression.Coalesce( Expression.Constant( null, typeof( int? ) ), Expression.Constant( 5 ) )
        );

        var optimizedExpr = new AccessSimplificationOptimizer().Optimize( referenceExpr );

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
    public void AccessSimplification_ShouldSimplifyConstantArrayIndexing()
    {
        // Arrange
        var array = Expression.Constant( (int[]) [10, 20, 30] );
        var referenceExpr = Expression.Lambda<Func<int>>(
            Expression.ArrayIndex( array, Expression.Constant( 1 ) )
        );

        var optimizedExpr = new AccessSimplificationOptimizer().Optimize( referenceExpr );

        // Act
        var reference = referenceExpr.Compile();
        var result = reference();

        var optimized = optimizedExpr.Compile();
        var comparand = optimized();

        // Assert
        Assert.AreEqual( 20, result );
        Assert.AreEqual( 20, comparand );
    }
}
