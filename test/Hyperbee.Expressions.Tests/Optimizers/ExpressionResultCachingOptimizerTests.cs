using System.Linq.Expressions;
using Hyperbee.Expressions.Optimizers;
using Hyperbee.Expressions.Tests.TestSupport;

namespace Hyperbee.Expressions.Tests.Optimizers;

[TestClass]
public class ExpressionResultCachingOptimizerTests
{
    public class TestClass
    {
        public string Method() => "Result";
    }

    [TestMethod]
    public void ExpressionCaching_ShouldNotCacheSimpleExpressions()
    {
        // Before: .Add(.Constant(2), .Constant(3)) + .Add(.Constant(2), .Constant(3))
        // After:  .Add(.Constant(2), .Constant(3)) + .Add(.Constant(2), .Constant(3))

        // Arrange
        var simpleExpr = Expression.Add(Expression.Constant(2), Expression.Constant(3));
        var referenceExpr = Expression.Lambda<Func<int>>(
            Expression.Add(simpleExpr, simpleExpr)
        );

        var optimizer = new ExpressionResultCachingOptimizer();

        // Act
        var optimizedExpr = optimizer.Optimize(referenceExpr);
        var reference = referenceExpr.Compile();
        var result = reference();
        var optimized = optimizedExpr.Compile();
        var comparand = optimized();

        // Assert
        Assert.AreEqual(result, comparand);
        Assert.IsTrue(optimizedExpr.GetDebugView().Contains("2 + 3 + 2 + 3"), "Simple expression should not be cached.");
    }

    [TestMethod]
    public void ExpressionCaching_ShouldCacheComplexExpressions()
    {
        // Before: .Add(.Multiply(.Constant(5), .Add(.Constant(3), .Constant(2))), .Multiply(.Constant(5), .Add(.Constant(3), .Constant(2))))
        // After:  .Block(.Assign(variable, .Multiply(.Constant(5), .Add(.Constant(3), .Constant(2)))), .Add(variable, variable))

        // Arrange
        var complexExpr = Expression.Multiply(Expression.Constant(5), Expression.Add(Expression.Constant(3), Expression.Constant(2)));
        var referenceExpr = Expression.Lambda<Func<int>>(
            Expression.Add(complexExpr, complexExpr)
        );

        var optimizer = new ExpressionResultCachingOptimizer();

        // Act
        var optimizedExpr = optimizer.Optimize(referenceExpr);
        var reference = referenceExpr.Compile();
        var result = reference();
        var optimized = optimizedExpr.Compile();
        var comparand = optimized();

        // Assert
        Assert.AreEqual(result, comparand);
        Assert.IsTrue(optimizedExpr.GetDebugView().Contains("Block"), "Complex expression should be cached with a block.");
    }

    [TestMethod]
    public void ExpressionCaching_ShouldCacheRepeatedMethodCalls()
    {
        // Before: .Add(.Call(Method), .Call(Method))
        // After:  .Block(.Assign(variable, .Call(Method)), .Add(variable, variable))

        // Arrange
        Expression<Func<int>> referenceExpr = () => Math.Abs(-5) + Math.Abs(-5);
        var optimizer = new ExpressionResultCachingOptimizer();

        // Act
        var optimizedExpr = optimizer.Optimize(referenceExpr);
        var reference = referenceExpr.Compile();
        var result = reference();
        var optimized = optimizedExpr.Compile();
        var comparand = optimized();

        // Assert
        Assert.AreEqual(result, comparand);
        Assert.IsTrue(optimizedExpr.GetDebugView().Contains("Block"), "Method call should be cached with a block.");
    }

    [TestMethod]
    public void ExpressionCaching_ShouldCacheRepeatedExpressions()
    {
        // Before: .Add(.Add(x, .Constant(5)), .Add(x, .Constant(5)))
        // After:  .Block(.Assign(variable, .Add(x, .Constant(5))), .Add(variable, variable))

        // Arrange
        var x = Expression.Parameter(typeof(int), "x");
        var repeatedExpr = Expression.Add(x, Expression.Constant(5));
        var complexExpression = Expression.Add(repeatedExpr, repeatedExpr);
        var optimizer = new ExpressionResultCachingOptimizer();

        // Act
        var result = optimizer.Optimize(complexExpression);

        // Assert
        Assert.IsInstanceOfType(result, typeof(BlockExpression));
        Assert.AreEqual(2, ((BlockExpression)result).Expressions.Count);
    }

    [TestMethod]
    public void ExpressionCaching_ShouldCacheCrossScopeExpression()
    {
        // Before: .Block(.Call(obj.Method), .Block(.Call(obj.Method)))
        // After:  .Block(.Assign(variable, .Call(obj.Method)), .Variable(variable), .Block(.Variable(variable)))

        // Arrange
        var methodCall = Expression.Call(Expression.Constant(new TestClass()), "Method", null);
        var innerBlock = Expression.Block(methodCall);
        var outerBlock = Expression.Block(methodCall, innerBlock);
        var optimizer = new ExpressionResultCachingOptimizer();

        // Act
        var result = optimizer.Optimize(outerBlock);

        // Assert
        Assert.IsInstanceOfType(result, typeof(BlockExpression));
    }

    [TestMethod]
    public void ExpressionCaching_ShouldCacheParameterizedSubexpression()
    {
        // Before: .Block(.Add(.Parameter(x), .Constant(5)), .Add(.Parameter(x), .Constant(5)))
        // After:  .Block(.Assign(variable, .Add(.Parameter(x), .Constant(5))), .Variable(variable), .Variable(variable))

        // Arrange
        var parameter = Expression.Parameter(typeof(int), "x");
        var addExpression = Expression.Add(parameter, Expression.Constant(5));
        var block = Expression.Block(addExpression, addExpression);
        var optimizer = new ExpressionResultCachingOptimizer();

        // Act
        var result = optimizer.Optimize(block);

        // Assert
        Assert.IsInstanceOfType(result, typeof(BlockExpression));
    }
}
