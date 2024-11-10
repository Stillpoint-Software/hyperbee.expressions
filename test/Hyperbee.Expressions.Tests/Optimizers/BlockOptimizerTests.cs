using System.Linq.Expressions;
using Hyperbee.Expressions.Optimizers;

namespace Hyperbee.Expressions.Tests.Optimizers;

[TestClass]
public class BlockOptimizerTests
{
    [TestMethod]
    public void StructuralSimplification_ShouldFlattenNestedBlocks()
    {
        // Arrange
        var referenceExpr = Expression.Lambda<Func<int>>(
            Expression.Block(
                Expression.Block(
                    Expression.Constant( 5 )
                )
            )
        );

        var optimizedExpr = new BlockOptimizer().Optimize( referenceExpr );

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
    public void StructuralSimplification_ShouldRemoveRedundantBlock()
    {
        // Arrange
        var referenceExpr = Expression.Lambda<Func<int>>(
            Expression.Block(
                Expression.Constant( 10 )
            )
        );

        var optimizedExpr = new BlockOptimizer().Optimize( referenceExpr );

        // Act
        var reference = referenceExpr.Compile();
        var result = reference();

        var optimized = optimizedExpr.Compile();
        var comparand = optimized();

        // Assert
        Assert.AreEqual( 10, result );
        Assert.AreEqual( 10, comparand );
    }
}
