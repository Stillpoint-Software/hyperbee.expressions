using Hyperbee.Expressions.Tests.TestSupport;
using static System.Linq.Expressions.Expression;

using static Hyperbee.Expressions.ExpressionExtensions;

namespace Hyperbee.Expressions.Tests;

[TestClass]
public class BlockYieldConditionalTests
{
    [DataTestMethod]
    //[DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.System )]
    //[DataRow( CompilerType.Interpret )]
    public void YieldBlock_ShouldYieldSuccessfully_WithIfThen( CompilerType compiler )
    {
        // Arrange
        var block = BlockYield(
            IfThen(
                Constant( true ),
                YieldReturn( Constant( 5 ) )
            )
        );

        var lambda = Lambda<Func<IEnumerable<int>>>( block );
        var compiledLambda = lambda.Compile( compiler );

        // Act
        var result = compiledLambda().First();

        // Assert
        Assert.AreEqual( 5, result );
    }

    [DataTestMethod]
    //[DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.System )]
    //[DataRow( CompilerType.Interpret )]
    public void YieldBlock_ShouldYieldSuccessfully_WithIfThenElse( CompilerType compiler )
    {
        // Arrange
        var block = BlockYield(
            IfThenElse(
                Constant( false ),
                YieldReturn( Constant( 1 ) ),
                YieldReturn( Constant( 5 ) )
            )
        );

        var lambda = Lambda<Func<IEnumerable<int>>>( block );
        var compiledLambda = lambda.Compile( compiler );

        // Act
        var result = compiledLambda().First();

        // Assert
        Assert.AreEqual( 5, result );
    }

    [DataTestMethod]
    //[DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.System )]
    //[DataRow( CompilerType.Interpret )]
    public void YieldBlock_ShouldYieldSuccessfully_WithIfThenElseBreaks( CompilerType compiler )
    {
        // Arrange
        var block = BlockYield(
            IfThenElse(
                Constant( true ),
                Block(
                    YieldReturn( Constant( 10 ) ),
                    YieldBreak(),
                    YieldReturn( Constant( 20 ) ) // Never reached
                ),
                YieldReturn( Constant( 5 ) )
            )
        );

        var lambda = Lambda<Func<IEnumerable<int>>>( block );
        var compiledLambda = lambda.Compile( compiler );

        // Act
        var result = compiledLambda().ToArray();

        // Assert
        Assert.AreEqual( 1, result.Length );
        Assert.AreEqual( 10, result[0] );
    }

    [DataTestMethod]
    //[DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.System )]
    //[DataRow( CompilerType.Interpret )]
    public void YieldBlock_ShouldYieldSuccessfully_WithIfThen_TrueBreak( CompilerType compiler )
    {
        // Arrange
        var block = BlockYield(
            IfThen(
                Constant( true ),
                YieldBreak()
            ),
            YieldReturn( Constant( 5 ) )
        );

        var lambda = Lambda<Func<IEnumerable<int>>>( block );
        var compiledLambda = lambda.Compile( compiler );

        // Act
        var result = compiledLambda().ToArray();

        // Assert
        Assert.AreEqual( 0, result.Length );
    }

    [DataTestMethod]
    //[DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.System )]
    //[DataRow( CompilerType.Interpret )]
    public void YieldBlock_ShouldYieldSuccessfully_WithIfThen_FalseBreak( CompilerType compiler )
    {
        // Arrange
        var block = BlockYield(
            IfThen(
                Constant( false ),
                YieldBreak()
            ),
            YieldReturn( Constant( 5 ) )
        );

        var lambda = Lambda<Func<IEnumerable<int>>>( block );
        var compiledLambda = lambda.Compile( compiler );

        // Act
        var result = compiledLambda().ToArray();

        // Assert
        Assert.AreEqual( 1, result.Length );
        Assert.AreEqual( 5, result[0] );
    }

    [DataTestMethod]
    //[DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.System )]
    //[DataRow( CompilerType.Interpret )]
    public void YieldBlock_ShouldYieldSuccessfully_WithConditionalAssignment( CompilerType compiler )
    {
        // Arrange: IfTrue branch contains a yield return
        var var = Variable( typeof( int ), "var" );

        var block = BlockYield(
            [var],
            Assign( var,
                Condition( Constant( true ),
                    Block(
                        YieldReturn( Constant( 2 ) ),
                        Constant( 1 )
                    ),
                    Constant( 0 )
                )
            ),
            YieldReturn( var )
        );
        var lambda = Lambda<Func<IEnumerable<int>>>( block );
        var compiledLambda = lambda.Compile( compiler );

        // Act
        var result = compiledLambda();

        // Assert
        Assert.AreEqual( 2, result.First() );
        Assert.AreEqual( 1, result.Skip( 1 ).First() );
    }

    [DataTestMethod]
    //[DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.System )]
    //[DataRow( CompilerType.Interpret )]
    public void YieldBlock_ShouldYieldSuccessfully_WithConditionalInTest( CompilerType compiler )
    {
        // Arrange: Test depends on yielding value first
        var test = Block(
            YieldReturn( Constant( 5 ) ),
            Constant( true )
        );

        var block = BlockYield(
            Condition( test, YieldReturn( Constant( 10 ) ), YieldReturn( Constant( 15 ) ) )
        );

        var lambda = Lambda<Func<IEnumerable<int>>>( block );
        var compiledLambda = lambda.Compile( compiler );

        // Act
        var result = compiledLambda();

        // Assert
        Assert.AreEqual( 5, result.First() );
        Assert.AreEqual( 10, result.Skip( 1 ).First() );
    }

    [DataTestMethod]
    //[DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.System )]
    //[DataRow( CompilerType.Interpret )]
    public void YieldBlock_ShouldYieldSuccessfully_WithAwaitBeforeAndAfterConditional( CompilerType compiler )
    {
        // Arrange: yield before and after a conditional expression
        var block = BlockYield(
            YieldReturn( Constant( 10 ) ),
            Condition( Constant( true ), Constant( 15 ), Constant( 0 ) ),
            YieldReturn( Constant( 20 ) )
        );
        var lambda = Lambda<Func<IEnumerable<int>>>( block );
        var compiledLambda = lambda.Compile( compiler );

        // Act
        var result = compiledLambda().ToArray();

        // Assert
        Assert.AreEqual( 10, result[0] );
        Assert.AreEqual( 20, result[1] );
    }

    [DataTestMethod]
    //[DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.System )]
    //[DataRow( CompilerType.Interpret )]
    public void YieldBlock_ShouldYieldSuccessfully_WithFalseCondition( CompilerType compiler )
    {
        // Arrange: False condition should lead to the false branch being executed
        var condition = Constant( false );
        var block = BlockYield(
            Condition( condition,
                Constant( 10 ),
                YieldReturn( Constant( 20 ) )
            )
        );
        var lambda = Lambda<Func<IEnumerable<int>>>( block );
        var compiledLambda = lambda.Compile( compiler );

        // Act
        var result = compiledLambda();

        // Assert
        Assert.AreEqual( 20, result.First() ); // False branch should be awaited and return 20
    }

    [DataTestMethod]
    //[DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.System )]
    //[DataRow( CompilerType.Interpret )]
    public void YieldBlock_ShouldYieldSuccessfully_WithComplexConditionalLogic( CompilerType compiler )
    {
        // Arrange: Two conditionals where both branches return yield values
        var block = BlockYield(
            Condition( Constant( true ),
                YieldReturn( Constant( 10 ) ),
                YieldReturn( Constant( 20 ) )
            ),
            Condition( Constant( false ),
                YieldReturn( Constant( 30 ) ),
                YieldReturn( Constant( 40 ) )
            )
        );

        var lambda = Lambda<Func<IEnumerable<int>>>( block );
        var compiledLambda = lambda.Compile( compiler );

        // Act
        var result = compiledLambda().ToArray();

        // Assert
        Assert.AreEqual( 10, result[0] );
        Assert.AreEqual( 40, result[1] );
    }

    [DataTestMethod]
    //[DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.System )]
    //[DataRow( CompilerType.Interpret )]
    public void BlockYield_ShouldYieldSuccessfully_WithParameters( CompilerType compiler )
    {
        // Arrange
        var param1 = Parameter( typeof( int ), "param1" );
        var param2 = Parameter( typeof( int ), "param2" );

        var block = BlockYield(
            IfThenElse( GreaterThan( param1, Constant( 10 ) ),
                IfThenElse( GreaterThan( param2, Constant( 10 ) ),
                    YieldReturn( Constant( 5 ) ),
                    YieldReturn( Constant( 10 ) ) ),
                YieldReturn( Constant( 15 ) ) ),
            YieldReturn( Constant( 20 ) )
        );

        var lambda = Lambda<Func<int, int, IEnumerable<int>>>( block, param1, param2 );
        var compiledLambda = lambda.Compile( compiler );

        // Act
        var resultTrueTrue = compiledLambda( 12, 15 ).ToArray();
        var resultFalseTrue = compiledLambda( 9, 15 ).ToArray();
        var resultTrueFalse = compiledLambda( 12, 8 ).ToArray();
        var resultFalseFalse = compiledLambda( 9, 8 ).ToArray();

        //Assert
        Assert.AreEqual( 5, resultTrueTrue[0] );
        Assert.AreEqual( 20, resultTrueTrue[1] );

        Assert.AreEqual( 15, resultFalseTrue[0] );
        Assert.AreEqual( 20, resultFalseTrue[1] );

        Assert.AreEqual( 10, resultTrueFalse[0] );
        Assert.AreEqual( 20, resultTrueFalse[1] );

        Assert.AreEqual( 15, resultFalseFalse[0] );
        Assert.AreEqual( 20, resultFalseFalse[1] );
    }
}
