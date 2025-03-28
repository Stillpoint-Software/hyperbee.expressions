using Hyperbee.Expressions.Tests.TestSupport;
using static System.Linq.Expressions.Expression;
using static Hyperbee.Expressions.ExpressionExtensions;

namespace Hyperbee.Expressions.Tests;

[TestClass]
public class BlockYieldBasicTests
{
    [DataTestMethod]
    //[DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.System )]
    //[DataRow( CompilerType.Interpret )]
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

    [DataTestMethod]
    //[DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Interpret )]
    public void TryFault_ShouldRunSuccessfully( CompilerType compiler )
    {
        // Arrange
        var block = TryFault(
            Constant( true ),
            Constant( false )
        );

        var lambda = Lambda<Func<bool>>( block );
        var compiledLambda = lambda.Compile( compiler );

        // Act
        var result = compiledLambda();

        //Assert
        Assert.IsTrue( result );
    }

    [DataTestMethod]
    //[DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Interpret )]
    public void TryFault_ShouldRunSuccessfully_WithException( CompilerType compiler )
    {
        var x = Parameter( typeof( int ), "x" );

        // Arrange
        var block = Block(
            [x],
            TryCatch(
                TryFault( // fault is like a try finally, but for exceptions
                    Block(
                        Assign( x, Constant( 1 ) ),
                        Throw( New( typeof( Exception ) ), typeof( int ) )
                    ),
                    Assign( x, Constant( 2 ) )
                ),
                // catch so we can verify fault ran
                Catch( typeof( Exception ), Constant( -1 ) )
            ),
            x
        );

        var lambda = Lambda<Func<int>>( block );
        var compiledLambda = lambda.Compile( compiler );

        // Act
        var result = compiledLambda();

        //Assert
        Assert.AreEqual( 2, result );
    }
}
