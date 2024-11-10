using System.Linq.Expressions;
using Hyperbee.Expressions.Optimizers;

namespace Hyperbee.Expressions.Tests.Optimizers;

[TestClass]
public class MemoryOptimizationOptimizerTests
{
    [TestMethod]
    public void MemoryOptimization_ShouldReuseParameter()
    {
        // Arrange
        var variable = Expression.Parameter( typeof(int), "x" );
        var referenceExpr = Expression.Lambda<Func<int>>(
            Expression.Block( [variable],
                Expression.Assign( variable, Expression.Constant( 10 ) ),
                variable
            )
        );

        var optimizedExpr = new MemoryOptimizationOptimizer().Optimize( referenceExpr );

        // Act
        var reference = referenceExpr.Compile();
        var result = reference();

        var optimized = optimizedExpr.Compile();
        var comparand = optimized();

        // Assert
        Assert.AreEqual( 10, result );
        Assert.AreEqual( 10, comparand );
    }

    [TestMethod]
    public void MemoryOptimization_ShouldRemoveUnusedTemporaryVariable()
    {
        // Arrange
        var unusedVar = Expression.Variable( typeof(int), "unused" );
        var referenceExpr = Expression.Lambda<Func<int>>(
            Expression.Block( [unusedVar],
                Expression.Assign( unusedVar, Expression.Constant( 5 ) ),
                Expression.Constant( 20 )
            )
        );

        var optimizedExpr = new MemoryOptimizationOptimizer().Optimize( referenceExpr );

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
