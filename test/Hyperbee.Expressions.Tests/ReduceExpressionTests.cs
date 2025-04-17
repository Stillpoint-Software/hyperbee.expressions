using System.Linq.Expressions;
using Hyperbee.Expressions.Tests.TestSupport;

using static System.Linq.Expressions.Expression;
using static Hyperbee.Expressions.Lab.ExpressionExtensions;

namespace Hyperbee.Expressions.Tests;

[TestClass]
public class ReduceExpressionTests
{
    [DataTestMethod]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Interpret )]
    public void ReduceExpressionTests_ShouldAddList( CompilerType compiler )
    {
        // Arrange
        var list = Constant( new List<int> { 1, 2, 3, 4 } );
        var seed = Constant( 0 );

        // 1 + 2 + 3 + 4 = 10
        var body = Reduce( list, seed, Add );

        // Act
        var lambda = Lambda<Func<int>>( body );
        var compiledLambda = lambda.Compile( compiler );

        var result = compiledLambda();

        // Assert
        Assert.AreEqual( 10, result );
    }

    [DataTestMethod]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Interpret )]
    public void ReduceExpressionTests_ShouldAddIndicesAndValues( CompilerType compiler )
    {
        // Arrange
        var list = Constant( new List<int> { 1, 2, 3, 4 } );
        var seed = Constant( 0 );

        // 0 + (0 + 1) + (1 + 2) + (2 + 3) + (3 + 4) = 16
        var body = Reduce( list, seed, ( acc, item, index ) => Add( acc, Add( index, item ) ) );

        // Act
        var lambda = Lambda<Func<int>>( body );
        var compiledLambda = lambda.Compile( compiler );

        var result = compiledLambda();

        // Assert
        Assert.AreEqual( 16, result );
    }

    [DataTestMethod]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Interpret )]
    public void ReduceExpressionTests_ShouldAddIndices( CompilerType compiler )
    {
        // Arrange
        var list = Constant( new List<int> { 1, 2, 3, 4 } );
        var seed = Constant( 0 );

        var body = Reduce( list, seed, ( acc, item, index, source ) =>
        {
            var size = Property( source, "Count" );

            // 0 +
            // ((0 * 4) + 1) +
            // ((1 * 4) + 2) +
            // ((2 * 4) + 3) +
            // ((3 * 4) + 4) = 34
            return Add( Add( acc, Multiply( index, size ) ), item );
        } );

        // Act
        var lambda = Lambda<Func<int>>( body );
        var compiledLambda = lambda.Compile( compiler );

        var result = compiledLambda();

        // Assert
        Assert.AreEqual( 34, result );
    }


    [DataTestMethod]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Interpret )]
    public void MapExpressionTests_ShouldAddIndices( CompilerType compiler )
    {
        var list = Constant( new List<double> { 1D, 2D, 3D, 4D } );

        var body = Map(
            list,
            ( item, index ) => Power( item, Convert( index, typeof( double ) ) )
        );

        // Act
        var lambda = Lambda<Func<List<double>>>( body );
        var compiledLambda = lambda.Compile( compiler );

        var result = compiledLambda();

        // Assert
        Assert.AreEqual( Math.Pow( 1, 0 ), result[0] );
        Assert.AreEqual( Math.Pow( 2, 1 ), result[1] );
        Assert.AreEqual( Math.Pow( 3, 2 ), result[2] );
        Assert.AreEqual( Math.Pow( 4, 3 ), result[3] );
    }
}
