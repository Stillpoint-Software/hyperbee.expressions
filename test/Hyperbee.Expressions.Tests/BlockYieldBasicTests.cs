using Hyperbee.Expressions.Tests.TestSupport;
using static System.Linq.Expressions.Expression;
using static Hyperbee.Expressions.ExpressionExtensions;

namespace Hyperbee.Expressions.Tests;

[TestClass]
public class BlockYieldBasicTests
{
    [TestMethod]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Interpret )]
    public void BlockYield_ShouldYieldSuccessfully( CompilerType compiler )
    {
        // Arrange
        var block = BlockEnumerable(
            IfThenElse( Constant( true ),
                Block(
                    YieldReturn( Constant( 5 ) ),
                    YieldReturn( Constant( 6 ) ),
                    YieldReturn( Constant( 7 ) )
                ),
                YieldReturn( Constant( 10 ) ) ),
            YieldReturn( Constant( 15 ) )
        );

        var lambda = Lambda<Func<IEnumerable<int>>>( block );
        var compiledLambda = lambda.Compile( compiler );

        // Act
        var results = compiledLambda().ToArray();

        //Assert
        Assert.AreEqual( 5, results[0] );
        Assert.AreEqual( 6, results[1] );
        Assert.AreEqual( 7, results[2] );
        Assert.AreEqual( 15, results[3] );
    }

    /*
    ======================================================================
    Keep test as it's used in:
    https://github.com/dotnet/runtime/issues/114081
    ======================================================================

    [DataTestMethod]
    [DataRow( false )]
    [DataRow( true )]
    public void TryFault_ShouldRunSuccessfully_WithReturnLabel( bool interpret )
    {
        var x = Parameter( typeof( int ), "x" );
        var resultLabel = Label( typeof( int ), "exit" );

        // Arrange
        var block = Block(
            [x],
            TryFault(
                Block(
                    Assign( x, Constant( 1 ) ),
                    Return( resultLabel, x )
                ),
                Assign( x, Constant( 2 ) )
            ),
            Label( resultLabel, defaultValue: Constant( 3 ) )
        );

        var lambda = Lambda<Func<int>>( block );
        var compiledLambda = lambda.Compile( preferInterpretation: interpret );

        // Act
        var result = compiledLambda();

        //Assert
        Assert.AreEqual( 1, result );
    }

    [DataTestMethod]
    [DataRow( false )]
    [DataRow( true )]
    public void TryFault_ShouldRunSuccessfully_WithReturnLabel_Workaround( bool interpret )
    {
        var x = Parameter( typeof( int ), "x" );
        var resultLabel = Label( typeof( int ), "exit" );

        // Arrange
        var block = Block(
            [x],
            TryFinally(
                Block(
                    Assign( x, Constant( 1 ) ),
                    Return( resultLabel, x )
                ),
                Assign( x, Constant( 2 ) )
            ),
            Label( resultLabel, defaultValue: Constant( 3 ) )
        );

        var lambda = Lambda<Func<int>>( block );
        var compiledLambda = lambda.Compile( preferInterpretation: interpret );

        // Act
        var result = compiledLambda();

        //Assert
        Assert.AreEqual( 1, result );
    }
*/

}

