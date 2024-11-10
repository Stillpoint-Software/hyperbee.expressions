using System.Linq.Expressions;
using Hyperbee.Expressions.Optimizers;

namespace Hyperbee.Expressions.Tests.Optimizers;

[TestClass]
public class ExpressionReductionOptimizerTests
{
    [TestMethod]
    public void Reduction_ShouldRemoveAddZero()
    {
        // Before: .Add(.Parameter(x), .Constant(0))
        // After:  .Parameter(x)

        // Arrange
        var expression = Expression.Add( Expression.Parameter( typeof( int ), "x" ), Expression.Constant( 0 ) );
        var optimizer = new ExpressionReductionOptimizer();

        // Act
        var result = optimizer.Optimize( expression );

        // Assert
        Assert.IsInstanceOfType( result, typeof( ParameterExpression ) );
    }

    [TestMethod]
    public void Reduction_ShouldRemoveMultiplyByOne()
    {
        // Before: .Multiply(.Parameter(x), .Constant(1))
        // After:  .Parameter(x)

        // Arrange
        var expression = Expression.Multiply( Expression.Parameter( typeof( int ), "x" ), Expression.Constant( 1 ) );
        var optimizer = new ExpressionReductionOptimizer();

        // Act
        var result = optimizer.Optimize( expression );

        // Assert
        Assert.IsInstanceOfType( result, typeof( ParameterExpression ) );
    }

    [TestMethod]
    public void Reduction_ShouldSimplifyLogicalIdentity()
    {
        // Before: .AndAlso(.Constant(true), .Parameter(x))
        // After:  .Parameter(x)

        // Arrange
        var andExpression = Expression.AndAlso( Expression.Constant( true ), Expression.Parameter( typeof( bool ), "x" ) );
        var optimizer = new ExpressionReductionOptimizer();

        // Act
        var result = optimizer.Optimize( andExpression );

        // Assert
        Assert.IsInstanceOfType( result, typeof( ParameterExpression ) );
    }

    [TestMethod]
    public void Reduction_ShouldFlattenNestedExpressions()
    {
        // Before: .Add(.Add(.Constant(1), .Constant(2)), .Constant(3))
        // After:  .Constant(6)

        // Arrange
        var nestedExpression = Expression.Add( Expression.Add( Expression.Constant( 1 ), Expression.Constant( 2 ) ), Expression.Constant( 3 ) );
        var optimizer = new ExpressionReductionOptimizer();

        // Act
        var result = optimizer.Optimize( nestedExpression );
        var constant = (ConstantExpression) result;
        var value = constant.Value;

        // Assert
        Assert.AreEqual( 6, value );
    }

    [TestMethod]
    public void Reduction_ShouldSimplifyMultiOperationReduction()
    {
        // Before: .Multiply(.Add(.Parameter(x), .Constant(0)), .Constant(1))
        // After:  .Parameter(x)

        // Arrange
        var parameter = Expression.Parameter( typeof( int ), "x" );
        var addZero = Expression.Add( parameter, Expression.Constant( 0 ) );
        var multiplyByOne = Expression.Multiply( addZero, Expression.Constant( 1 ) );
        var optimizer = new ExpressionReductionOptimizer();

        // Act
        var result = optimizer.Optimize( multiplyByOne );

        // Assert
        Assert.IsInstanceOfType( result, typeof( ParameterExpression ) );
    }

    [TestMethod]
    public void Reduction_ShouldSimplifyComplexBooleanReduction()
    {
        // Before: .AndAlso(.OrElse(.Constant(true), .Constant(false)), .Constant(true))
        // After:  .Constant(true)

        // Arrange
        var orExpression = Expression.OrElse( Expression.Constant( true ), Expression.Constant( false ) );
        var andExpression = Expression.AndAlso( orExpression, Expression.Constant( true ) );
        var optimizer = new ExpressionReductionOptimizer();

        // Act
        var result = optimizer.Optimize( andExpression );
        var value = (bool) ((ConstantExpression) result).Value!;

        // Assert
        Assert.IsTrue( value );
    }
}
