using Hyperbee.Expressions.Tests.TestSupport;
using static System.Linq.Expressions.Expression;

using static Hyperbee.Expressions.ExpressionExtensions;

namespace Hyperbee.Expressions.Tests;

[TestClass]
public class BlockYieldSwitchTests
{
    [DataTestMethod]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Interpret )]
    public void YieldBlock_ShouldYieldSuccessfully_WithYieldInSwitchValue( CompilerType compiler )
    {
        // Arrange: Yield in the switch value
        var switchValue = Block(
            YieldReturn( Constant( 1 ) ),
            Constant( 1 )
        );
        var block = BlockEnumerable(
            Switch(
                switchValue,
                Constant( 0 ),
                SwitchCase( Constant( 10 ), Constant( 1 ) ),
                SwitchCase( Constant( 20 ), Constant( 2 ) )
            )
        );
        var lambda = Lambda<Func<IEnumerable<int>>>( block );
        var compiledLambda = lambda.Compile( compiler );

        // Act
        var result = compiledLambda().ToArray();

        // Assert
        Assert.AreEqual( 1, result.Length );
        Assert.AreEqual( 1, result[0] );
    }

    [DataTestMethod]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Interpret )]
    public void YieldBlock_ShouldYieldSuccessfully_WithAwaitInDefaultBody( CompilerType compiler )
    {
        // Arrange: Default case contains an awaited task
        var switchValue = Constant( 3 ); // No case matches this value
        var block = BlockEnumerable(
            Switch(
                switchValue,
                YieldReturn( Constant( 99 ) ), // Default body
                SwitchCase( Constant( 10 ), Constant( 1 ) ),
                SwitchCase( Constant( 20 ), Constant( 2 ) )
            )
        );
        var lambda = Lambda<Func<IEnumerable<int>>>( block );
        var compiledLambda = lambda.Compile( compiler );

        // Act
        var result = compiledLambda().ToArray();

        // Assert
        Assert.AreEqual( 1, result.Length );
        Assert.AreEqual( 99, result[0] );
    }

    [DataTestMethod]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Interpret )]
    public void YieldBlock_ShouldYieldSuccessfully_WithAwaitInSwitchCaseBody( CompilerType compiler )
    {
        // Arrange: One of the case bodies contains an awaited task
        var switchValue = Constant( 1 );
        var block = BlockEnumerable(
            Switch(
                switchValue,
                Constant( 0 ),
                SwitchCase(
                    YieldReturn( Constant( 100 ) ),
                    Constant( 1 )
                ),
                SwitchCase( Constant( 200 ), Constant( 2 ) )
            )
        );
        var lambda = Lambda<Func<IEnumerable<int>>>( block );
        var compiledLambda = lambda.Compile( compiler );

        // Act
        var result = compiledLambda().ToArray();

        // Assert
        Assert.AreEqual( 1, result.Length );
        Assert.AreEqual( 100, result[0] );
    }

    [DataTestMethod]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Interpret )]
    public void YieldBlock_ShouldYieldSuccessfully_WithYieldInSwitchValueAndCaseBody( CompilerType compiler )
    {
        // Arrange: Yield both in switch value and case body
        var switchValue = Block(
            YieldReturn( Constant( 25 ) ),
            Constant( 2 )
        );
        var block = BlockEnumerable(
            Switch(
                switchValue,
                Constant( 0 ),
                SwitchCase(
                    YieldReturn( Constant( 50 ) ),
                    Constant( 2 )
                ),
                SwitchCase( Constant( 20 ), Constant( 3 ) )
            )
        );
        var lambda = Lambda<Func<IEnumerable<int>>>( block );
        var compiledLambda = lambda.Compile( compiler );

        // Act
        var result = compiledLambda().ToArray();

        // Assert
        Assert.AreEqual( 2, result.Length );
        Assert.AreEqual( 25, result[0] );
        Assert.AreEqual( 50, result[1] );
    }

    [DataTestMethod]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Interpret )]
    public void YieldBlock_ShouldYieldSuccessfully_WithNestedSwitchesAndYields( CompilerType compiler )
    {
        // Arrange: Outer and inner switch cases involve awaited tasks
        var switchValue = Constant( 1 );
        var nestedSwitch = Switch(
            Constant( 1 ),
            Constant( 0 ),
            SwitchCase(
                YieldReturn( Constant( 30 ) ),
                Constant( 1 )
            ),
            SwitchCase( YieldReturn( Constant( 50 ) ), Constant( 2 ) )
        );

        var block = BlockEnumerable(
            Switch(
                switchValue,
                nestedSwitch,
                SwitchCase( YieldReturn( Constant( 20 ) ), Constant( 2 ) )
            )
        );
        var lambda = Lambda<Func<IEnumerable<int>>>( block );
        var compiledLambda = lambda.Compile( compiler );

        // Act
        var result = compiledLambda().ToArray();

        // Assert
        Assert.AreEqual( 1, result.Length );
        Assert.AreEqual( 30, result[0] );
    }

    [DataTestMethod]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Interpret )]
    public void YieldBlock_ShouldYieldSuccessfully_WithYieldBeforeAndAfterSwitch( CompilerType compiler )
    {
        // Arrange: Awaiting tasks before and after the switch expression
        var block = BlockEnumerable(
            YieldReturn( Constant( 30 ) ),
            Switch(
                Constant( 1 ),
                Constant( 0 ),
                SwitchCase( Constant( 10 ), Constant( 1 ) ),
                SwitchCase( Constant( 20 ), Constant( 2 ) )
            ),
            YieldReturn( Constant( 15 ) )
        );
        var lambda = Lambda<Func<IEnumerable<int>>>( block );
        var compiledLambda = lambda.Compile( compiler );

        // Act
        var result = compiledLambda().ToArray();

        // Assert
        Assert.AreEqual( 2, result.Length );
        Assert.AreEqual( 30, result[0] );
        Assert.AreEqual( 15, result[1] );
    }

}
