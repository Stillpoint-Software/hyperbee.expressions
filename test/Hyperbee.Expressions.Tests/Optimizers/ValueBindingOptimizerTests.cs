using System.Linq.Expressions;
using Hyperbee.Expressions.Optimizers;

namespace Hyperbee.Expressions.Tests.Optimizers;

[TestClass]
public class ValueBindingOptimizerTests
{
    public class Container
    {
        public NestedClass Nested { get; } = new();
    }

    public class NestedClass
    {
        public string Value => "ExpectedValue";
    }

    [TestMethod]
    public void ValueBinding_ShouldFoldConstants()
    {
        // Before: .Add(.Constant(2), .Constant(3))
        // After:  .Constant(5)

        // Arrange
        var expression = Expression.Add( Expression.Constant( 2 ), Expression.Constant( 3 ) );
        var optimizer = new ValueBindingOptimizer();

        // Act
        var result = optimizer.Optimize( expression );
        var value = ((ConstantExpression) result).Value;

        // Assert
        Assert.AreEqual( 5, value );
    }

    [TestMethod]
    public void ValueBinding_ShouldInlineSingleUseVariable()
    {
        // Before: .Block(.Assign(.Parameter(x), .Constant(10)), .Parameter(x))
        // After:  .Constant(10)

        // Arrange
        var parameter = Expression.Parameter( typeof( int ), "x" );
        var block = Expression.Block( [parameter], Expression.Assign( parameter, Expression.Constant( 10 ) ), parameter );
        var optimizer = new ValueBindingOptimizer();

        // Act
        var result = optimizer.Optimize( block );
        var value = ((ConstantExpression) result).Value;

        // Assert
        Assert.AreEqual( 10, value );
    }

    [TestMethod]
    public void ValueBinding_ShouldSimplifyConstantMemberAccess()
    {
        // Before: .Property(.Constant(new DateTime(2024, 1, 1)), "Year")
        // After:  .Constant(2024)

        // Arrange
        var date = Expression.Constant( new DateTime( 2024, 1, 1 ) );
        var memberAccess = Expression.Property( date, "Year" );
        var optimizer = new ValueBindingOptimizer();

        // Act
        var result = optimizer.Optimize( memberAccess );
        var value = ((ConstantExpression) result).Value;

        // Assert
        Assert.AreEqual( 2024, value );
    }

    [TestMethod]
    public void ValueBinding_ShouldInlineVariableInNestedScope()
    {
        // Before: .Block(.Assign(.Parameter(x), .Constant(10)), .Block(.Parameter(x)))
        // After:  .Constant(10)

        // Arrange
        var parameter = Expression.Parameter( typeof( int ), "x" );
        var nestedBlock = Expression.Block( parameter );
        var outerBlock = Expression.Block( [parameter], Expression.Assign( parameter, Expression.Constant( 10 ) ), nestedBlock );
        var optimizer = new ValueBindingOptimizer();

        // Act
        var result = optimizer.Optimize( outerBlock );
        var value = ((ConstantExpression) result).Value;

        // Assert
        Assert.AreEqual( 10, value );
    }

    [TestMethod]
    public void ValueBinding_ShouldSimplifyComplexMemberAccess()
    {
        // Before: .Property(.Property(.Constant(new Container()), "Nested"), "Value")
        // After:  .Constant("ExpectedValue")

        // Arrange
        var container = Expression.Constant( new Container() );
        var nestedMember = Expression.Property( container, nameof( Container.Nested ) );
        var memberAccess = Expression.Property( nestedMember, nameof( Container.Nested.Value ) );
        var optimizer = new ValueBindingOptimizer();

        // Act
        var result = optimizer.Optimize( memberAccess );
        var value = ((ConstantExpression) result).Value;

        // Assert
        Assert.AreEqual( "ExpectedValue", value );
    }

    [TestMethod]
    public void ValueBinding_ShouldReduceRedundantAllocations()
    {
        // Before: .Block(.Assign(.Parameter(x), .Constant(1)), .Parameter(x))
        // After:  .Constant(1)

        // Arrange
        var param = Expression.Parameter( typeof( int ), "x" );
        var redundantAlloc = Expression.Block( [param], Expression.Assign( param, Expression.Constant( 1 ) ), param );
        var optimizer = new ValueBindingOptimizer();

        // Act
        var result = optimizer.Optimize( redundantAlloc );
        var constant = (ConstantExpression) result;
        var value = constant.Value;

        // Assert
        Assert.AreEqual( 1, value );
    }

    [TestMethod]
    public void ValueBinding_ShouldReuseTemporaryVariableInLoop()
    {
        // Before: .Loop(.Block(.Assign(.Parameter(temp), .Constant(42)), .Parameter(temp)))
        // After:  .Constant(42) 

        // Arrange
        var tempVar = Expression.Parameter( typeof( int ), "temp" );
        var loop = Expression.Loop( Expression.Block( [tempVar], Expression.Assign( tempVar, Expression.Constant( 42 ) ), tempVar ) );
        var optimizer = new ValueBindingOptimizer();

        // Act
        var result = optimizer.Optimize( loop );

        // Assert
        var resultValue = (result as ConstantExpression)?.Value;

        Assert.IsInstanceOfType( result, typeof( ConstantExpression ) );
        Assert.AreEqual( 42, resultValue, "Expected the loop body constant to have value 42." );
    }

    [TestMethod]
    public void ValueBinding_ShouldReduceAllocationsInNestedBlocks()
    {
        // Before: .Block(.Block(.Assign(.Parameter(temp), .Constant(10)), .Parameter(temp)), .Parameter(temp))
        // After:  .Constant(10)

        // Arrange
        var tempVar = Expression.Parameter( typeof( int ), "temp" );
        var innerBlock = Expression.Block( [tempVar], Expression.Assign( tempVar, Expression.Constant( 10 ) ), tempVar );
        var outerBlock = Expression.Block( innerBlock, tempVar );
        var optimizer = new ValueBindingOptimizer();

        // Act
        var result = optimizer.Optimize( outerBlock );

        // Assert
        Assert.IsInstanceOfType( result, typeof( ConstantExpression ) );
    }
}
