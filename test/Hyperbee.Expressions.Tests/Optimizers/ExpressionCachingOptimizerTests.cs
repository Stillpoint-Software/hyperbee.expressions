using System.Linq.Expressions;
using Hyperbee.Expressions.Optimizers;
using Hyperbee.Expressions.Tests.TestSupport;

namespace Hyperbee.Expressions.Tests.Optimizers;

[TestClass]
public class ExpressionCachingOptimizerTests
{
    [TestMethod]
    public void ExpressionCaching_ShouldNotCacheSimpleExpressions()
    {
        // Arrange: A simple expression that doesn't need caching
        var simpleExpr = Expression.Add( Expression.Constant( 2 ), Expression.Constant( 3 ) );
        var referenceExpr = Expression.Lambda<Func<int>>(
            Expression.Add( simpleExpr, simpleExpr )
        );

        var optimizer = new ExpressionResultCachingOptimizer();
        var optimizedExpr = optimizer.Optimize( referenceExpr );

        // Act: Compile and run both expressions
        var reference = referenceExpr.Compile();
        var result = reference();

        var optimized = optimizedExpr.Compile();
        var comparand = optimized();

        // Assert: Ensure both versions return the same result and have similar structure
        Assert.AreEqual( result, comparand );
        Assert.IsTrue( optimizedExpr.GetDebugView().Contains( "2 + 3 + 2 + 3" ), "Simple expression should not be cached." );
    }

    [TestMethod]
    public void ExpressionCaching_ShouldCacheComplexExpressions()
    {
        // Arrange: A more complex expression that benefits from caching
        var complexExpr = Expression.Multiply( Expression.Constant( 5 ), Expression.Add( Expression.Constant( 3 ), Expression.Constant( 2 ) ) );
        var referenceExpr = Expression.Lambda<Func<int>>(
            Expression.Add( complexExpr, complexExpr )
        );

        var optimizer = new ExpressionResultCachingOptimizer();
        var optimizedExpr = optimizer.Optimize( referenceExpr );

        // Act: Compile and run both expressions
        var reference = referenceExpr.Compile();
        var result = reference();

        var optimized = optimizedExpr.Compile();
        var comparand = optimized();

        // Assert: Ensure both versions return the same result and check for caching
        Assert.AreEqual( result, comparand );
        Assert.IsTrue( optimizedExpr.GetDebugView().Contains( "Block" ), "Complex expression should be cached with a block." );
    }

    [TestMethod]
    public void ExpressionCaching_ShouldCacheRepeatedMethodCalls()
    {
        // Arrange: A lambda with a method call that is repeated, which should be cached
        Expression<Func<int>> referenceExpr = () => Math.Abs( -5 ) + Math.Abs( -5 );

        var optimizer = new ExpressionResultCachingOptimizer();
        var optimizedExpr = optimizer.Optimize( referenceExpr );

        // Act: Compile and run both expressions
        var reference = referenceExpr.Compile();
        var result = reference();

        var optimized = optimizedExpr.Compile();
        var comparand = optimized();

        // Assert: Ensure both versions return the same result and that caching occurred
        Assert.AreEqual( result, comparand );
        Assert.IsTrue( optimizedExpr.GetDebugView().Contains( "Block" ), "Method call should be cached with a block." );
    }
}
