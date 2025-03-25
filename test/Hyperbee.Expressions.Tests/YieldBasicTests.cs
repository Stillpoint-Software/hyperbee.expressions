using System.Linq.Expressions;
using System.Reflection;
using Hyperbee.Expressions.Tests.TestSupport;
using static System.Linq.Expressions.Expression;

using static Hyperbee.Expressions.CompilerServices.YieldSupport.ExpressionExtensions;


namespace Hyperbee.Expressions.Tests;

[TestClass]
public class YieldBasicTests
{
    [DataTestMethod]
    //[DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.System )]
    //[DataRow( CompilerType.Interpret )]
    public void BlockYield_ShouldYieldSuccessfully( CompilerType compiler )
    {
        // Arrange
        var block = BlockYield(
            IfThenElse( Constant( true ),
                Block(
                    YieldReturn( Constant( 5 ) ),
                    YieldReturn( Constant( 6 ) ),
                    YieldReturn( Constant( 7 ) )
                //YieldBreak()
                ),
                YieldReturn( Constant( 10 ) ) ),
            YieldReturn( Constant( 15 ) )//,
                                         //YieldBreak()
        );

        var lambda = Lambda<Func<IEnumerable<int>>>( block );
        var compiledLambda = lambda.Compile( compiler );

        // Act
        var result = compiledLambda();


        var results = result.ToArray();

        //Assert
        Assert.AreEqual( 5, results[0] );
        Assert.AreEqual( 6, results[1] );
        Assert.AreEqual( 7, results[2] );
        Assert.AreEqual( 15, results[3] );
    }

    [DataTestMethod]
    //[DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.System )]
    //[DataRow( CompilerType.Interpret )]
    public void BlockYield_ShouldYieldSuccessfully_WithParameters( CompilerType compiler )
    {
        // Arrange
        var param1 = Parameter( typeof( int ), "param1" );
        var block = BlockYield(
            IfThenElse( GreaterThan( param1, Constant( 10 ) ),
                YieldReturn( Constant( 5 ) ),
                YieldReturn( Constant( 10 ) ) ),
            YieldReturn( Constant( 15 ) )
        );

        var lambda = Lambda<Func<int, IEnumerable<int>>>( block, param1 );
        var compiledLambda = lambda.Compile( compiler );

        // Act
        var result = compiledLambda( 12 );

        var results = result.ToArray();

        //Assert
        Assert.AreEqual( 5, results[0] );
        Assert.AreEqual( 15, results[1] );
    }

}
