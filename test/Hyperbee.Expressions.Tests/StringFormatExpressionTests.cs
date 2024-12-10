using System.Linq.Expressions;

namespace Hyperbee.Expressions.Tests;

[TestClass]
public class StringFormatExpressionTests
{
    [TestMethod]
    public void StringFormatExpression_Should_Return_Format_When_No_Arguments()
    {
        // Arrange
        var format = Expression.Constant( "Hello, world!" );

        var formatExpr = ExpressionExtensions.StringFormat( format, [] );

        // Act
        var lambda = Expression.Lambda<Func<string>>( formatExpr ).Compile();
        var result = lambda();

        // Assert
        Assert.AreEqual( "Hello, world!", result, "Should return the format string when no arguments are provided." );
    }

    [TestMethod]
    public void StringFormatExpression_Should_Format_String_With_Arguments()
    {
        // Arrange
        var format = Expression.Constant( "Hello, {0}! You have {1} new messages." );
        var arg1 = Expression.Constant( "Alice" );
        var arg2 = Expression.Constant( 5 );

        var formatExpr = ExpressionExtensions.StringFormat( format, [arg1, arg2] );

        // Act
        var lambda = Expression.Lambda<Func<string>>( formatExpr ).Compile();
        var result = lambda();

        // Assert
        Assert.AreEqual( "Hello, Alice! You have 5 new messages.", result, "Should correctly format the string." );
    }

    [TestMethod]
    [ExpectedException( typeof( ArgumentException ) )]
    public void StringFormatExpression_Should_Throw_If_Format_Is_Not_String()
    {
        // Arrange
        var invalidFormat = Expression.Constant( 42 );

        // Act
        _ = ExpressionExtensions.StringFormat( invalidFormat, Expression.Constant( 10 ) );

        // Assert: Exception is expected
    }

    [TestMethod]
    public void StringFormatExpression_Should_Work_Within_Complex_Block()
    {
        // Arrange
        var format = Expression.Constant( "The sum of {0} and {1} is {2}" );
        var x = Expression.Parameter( typeof( int ), "x" );
        var y = Expression.Parameter( typeof( int ), "y" );
        var sum = Expression.Add( x, y );

        var formatExpr = ExpressionExtensions.StringFormat( format, [x, y, sum] );

        var block = Expression.Block( formatExpr );

        // Act
        var lambda = Expression.Lambda<Func<int, int, string>>( block, x, y ).Compile();
        var result = lambda( 10, 20 );

        // Assert
        Assert.AreEqual( "The sum of 10 and 20 is 30", result, "Should correctly format the string within a block." );
    }
}
