using System.Linq.Expressions;
using Hyperbee.Expressions.Optimizers;

namespace Hyperbee.Expressions.Tests.Optimizers;

[TestClass]
public class ExpressionReductionOptimizerTests
{
    [TestMethod]
    public void ExpressionSimplification_ShouldRemoveAddZero()
    {
        // Arrange
        var referenceExpr = Expression.Lambda<Func<int>>(
            Expression.Add( Expression.Constant( 5 ), Expression.Constant( 0 ) )
        );

        var optimizedExpr = new ExpressionReductionOptimizer().Optimize( referenceExpr );

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
    public void ExpressionSimplification_ShouldRemoveMultiplyByOne()
    {
        // Arrange
        var referenceExpr = Expression.Lambda<Func<int>>(
            Expression.Multiply( Expression.Constant( 7 ), Expression.Constant( 1 ) )
        );

        var optimizedExpr = new ExpressionReductionOptimizer().Optimize( referenceExpr );

        // Act
        var reference = referenceExpr.Compile();
        var result = reference();

        var optimized = optimizedExpr.Compile();
        var comparand = optimized();

        // Assert
        Assert.AreEqual( 7, result );
        Assert.AreEqual( 7, comparand );
    }
}
