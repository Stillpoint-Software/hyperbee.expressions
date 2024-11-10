using System.Linq.Expressions;
using Hyperbee.Expressions.Optimizers;

namespace Hyperbee.Expressions.Tests.Optimizers;

[TestClass]
public class InliningOptimizerTests
{
    [TestMethod]
    public void Inlining_ShouldInlineSimpleConstant()
    {
        // Before: .Add(.Constant(10), .Constant(5))
        // After:  .Constant(15)

        // Arrange
        var expression = Expression.Add(Expression.Constant(10), Expression.Constant(5));
        var optimizer = new InliningOptimizer();

        // Act
        var result = optimizer.Optimize(expression);
        var value = ((ConstantExpression)result).Value;

        // Assert
        Assert.AreEqual(15, value);
    }

    [TestMethod]
    public void Inlining_ShouldInlineLambdaExpression()
    {
        // Before: .Invoke((x) => x + 5, .Constant(3))
        // After:  .Constant(8)

        // Arrange
        var parameter = Expression.Parameter(typeof(int), "x");
        var lambda = Expression.Lambda(Expression.Add(parameter, Expression.Constant(5)), parameter);
        var invocation = Expression.Invoke(lambda, Expression.Constant(3));
        var optimizer = new InliningOptimizer();

        // Act
        var result = optimizer.Optimize(invocation);
        var value = ((ConstantExpression)result).Value;

        // Assert
        Assert.AreEqual(8, value);
    }

    [TestMethod]
    public void Inlining_ShouldShortCircuitBoolean()
    {
        // Before: .AndAlso(.Constant(true), .Constant(false))
        // After:  .Constant(false)

        // Arrange
        var expression = Expression.AndAlso(Expression.Constant(true), Expression.Constant(false));
        var optimizer = new InliningOptimizer();

        // Act
        var result = optimizer.Optimize(expression);
        var value = (bool)((ConstantExpression)result).Value!;

        // Assert
        Assert.IsFalse(value);
    }

    [TestMethod]
    public void Inlining_ShouldInlineConditionalExpression()
    {
        // Before: .Conditional(.Constant(true), .Constant("True"), .Constant("False"))
        // After:  .Constant("True")

        // Arrange
        var condition = Expression.Constant(true);
        var conditional = Expression.Condition(condition, Expression.Constant("True"), Expression.Constant("False"));
        var optimizer = new InliningOptimizer();

        // Act
        var result = optimizer.Optimize(conditional);
        var value = ((ConstantExpression)result).Value;

        // Assert
        Assert.AreEqual("True", value);
    }

    [TestMethod]
    public void Inlining_ShouldSimplifyNestedConditionalExpression()
    {
        // Before: .Conditional(.Constant(true), .Conditional(.Constant(false), .Constant("A"), .Constant("B")), .Constant("C"))
        // After:  .Constant("B")

        // Arrange
        var innerCondition = Expression.Condition(
            Expression.Constant(false),
            Expression.Constant("A"),
            Expression.Constant("B")
        );
        var outerCondition = Expression.Condition(
            Expression.Constant(true),
            innerCondition,
            Expression.Constant("C")
        );
        var optimizer = new InliningOptimizer();

        // Act
        var result = optimizer.Optimize(outerCondition);
        var value = ((ConstantExpression)result).Value;

        // Assert
        Assert.AreEqual("B", value);
    }
}
