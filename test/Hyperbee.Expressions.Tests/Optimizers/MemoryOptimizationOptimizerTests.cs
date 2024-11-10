using System.Linq.Expressions;
using Hyperbee.Expressions.Optimizers;

namespace Hyperbee.Expressions.Tests.Optimizers;

[TestClass]
public class MemoryOptimizationOptimizerTests
{
    [TestMethod]
    public void MemoryOptimization_ShouldReduceRedundantAllocations()
    {
        // Before: .Block(.Assign(.Parameter(x), .Constant(1)), .Parameter(x))
        // After:  .Constant(1)

        // Arrange
        var param = Expression.Parameter(typeof(int), "x");
        var redundantAlloc = Expression.Block( [param], Expression.Assign(param, Expression.Constant(1)), param);
        var optimizer = new MemoryOptimizationOptimizer();

        // Act
        var result = optimizer.Optimize(redundantAlloc);
        var constant = (ConstantExpression)result;
        var value = constant.Value;

        // Assert
        Assert.AreEqual(1, value);
    }

    [TestMethod]
    public void MemoryOptimization_ShouldReuseTemporaryVariableInLoop()
    {
        // Before: .Loop(.Block(.Assign(.Parameter(temp), .Constant(42)), .Parameter(temp)))
        // After:  .Constant(42)

        // Arrange
        var tempVar = Expression.Parameter(typeof(int), "temp");
        var loop = Expression.Loop(Expression.Block( [tempVar], Expression.Assign(tempVar, Expression.Constant(42)), tempVar));
        var optimizer = new MemoryOptimizationOptimizer();

        // Act
        var result = optimizer.Optimize(loop);

        // Assert
        Assert.IsInstanceOfType(result, typeof(ConstantExpression));
    }

    [TestMethod]
    public void MemoryOptimization_ShouldReduceAllocationsInNestedBlocks()
    {
        // Before: .Block(.Block(.Assign(.Parameter(temp), .Constant(10)), .Parameter(temp)), .Parameter(temp))
        // After:  .Constant(10)

        // Arrange
        var tempVar = Expression.Parameter(typeof(int), "temp");
        var innerBlock = Expression.Block( [tempVar], Expression.Assign(tempVar, Expression.Constant(10)), tempVar);
        var outerBlock = Expression.Block(innerBlock, tempVar);
        var optimizer = new MemoryOptimizationOptimizer();

        // Act
        var result = optimizer.Optimize(outerBlock);

        // Assert
        Assert.IsInstanceOfType(result, typeof(ConstantExpression));
    }
}
