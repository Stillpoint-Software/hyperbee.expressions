using System.Linq.Expressions;
using Hyperbee.Expressions.Tests.TestSupport;

using static System.Linq.Expressions.Expression;
using static Hyperbee.Expressions.Lab.ExpressionExtensions;

namespace Hyperbee.Expressions.Tests;

[TestClass]
public class MapExpressionTests
{
    [TestMethod]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Interpret )]
    public void MapExpressionTests_ShouldDoubleValues( CompilerType compiler )
    {
        // Arrange
        var list = Constant( new List<int> { 1, 2, 3, 4 } );

        var body = Map(
            list,
            item => Multiply( item, Constant( 2 ) )
        );

        // Act
        var lambda = Lambda<Func<List<int>>>( body );
        var compiledLambda = lambda.Compile( compiler );

        var result = compiledLambda();

        // Assert
        Assert.AreEqual( 2, result[0] );
        Assert.AreEqual( 4, result[1] );
        Assert.AreEqual( 6, result[2] );
        Assert.AreEqual( 8, result[3] );
    }

    [TestMethod]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Interpret )]
    public void MapExpressionTests_ShouldAddIndexToValues( CompilerType compiler )
    {
        // Arrange
        var list = Constant( new List<int> { 10, 20, 30, 40 } );

        var body = Map( list, Add );

        // Act
        var lambda = Lambda<Func<List<int>>>( body );
        var compiledLambda = lambda.Compile( compiler );

        var result = compiledLambda();

        // Assert
        Assert.AreEqual( 10 + 0, result[0] );
        Assert.AreEqual( 20 + 1, result[1] );
        Assert.AreEqual( 30 + 2, result[2] );
        Assert.AreEqual( 40 + 3, result[3] );
    }

    [TestMethod]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Interpret )]
    public void MapExpressionTests_ShouldMultipleValuesWithCount( CompilerType compiler )
    {
        // Arrange
        var list = Constant( new List<int> { 1, 2, 3, 4 } );

        var body = Map(
            list,
            ( item, index, source ) => Add( Multiply( item, index ), Property( source, "Count" ) )
        );

        // Act
        var lambda = Lambda<Func<List<int>>>( body );
        var compiledLambda = lambda.Compile( compiler );

        var result = compiledLambda();

        // Assert
        Assert.AreEqual( 4, result[0] );
        Assert.AreEqual( 6, result[1] );
        Assert.AreEqual( 10, result[2] );
        Assert.AreEqual( 16, result[3] );
    }
}
