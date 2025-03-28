using Hyperbee.Expressions.Tests.TestSupport;
using static System.Linq.Expressions.Expression;
using static Hyperbee.Expressions.ExpressionExtensions;

namespace Hyperbee.Expressions.Tests;

[TestClass]
public class WhileExpressionTests
{
    [DataTestMethod]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Interpret )]
    public void WhileExpression_ShouldBreak_WhenConditionMet( CompilerType compiler )
    {
        // Arrange
        var counter = Variable( typeof( int ), "counter" );

        var block = Block(
            [counter],
            Assign( counter, Constant( 0 ) ),
            While(
                LessThan( counter, Constant( 10 ) ),
                PostIncrementAssign( counter )
            ),
            counter
        );

        // Act
        var lambda = Lambda<Func<int>>( block );
        var compiledLambda = lambda.Compile( compiler );

        var result = compiledLambda();

        // Assert
        Assert.AreEqual( 10, result, "Loop should break when counter == 10." );
    }

    [DataTestMethod]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Interpret )]
    public void WhileExpression_ShouldBreak( CompilerType compiler )
    {
        // Arrange
        var counter = Variable( typeof( int ), "counter" );
        var counterInit = Assign( counter, Constant( 0 ) );

        var condition = LessThan( counter, Constant( 10 ) );

        var whileExpr = While( condition, ( breakLabel, _ ) =>
            IfThenElse(
                Equal( counter, Constant( 5 ) ),
                Break( breakLabel ),
                PostIncrementAssign( counter )
            )
        );

        var block = Block(
            [counter],
            counterInit,
            whileExpr,
            counter
        );

        // Act
        var lambda = Lambda<Func<int>>( block );
        var compiledLambda = lambda.Compile( compiler );

        var result = compiledLambda();

        // Assert
        Assert.AreEqual( 5, result, "Loop should break when counter == 5." );
    }

    [DataTestMethod]
    //[DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.System )]
    //[DataRow( CompilerType.Interpret )]
    public void WhileExpression_ShouldIterateOverCollection_WithYields( CompilerType compiler )
    {
        // Arrange
        var counter = Variable( typeof( int ), "counter" );

        var whileExpr = BlockEnumerable(
            [counter],
            While(
                LessThan( counter, Constant( 10 ) ),
                Block(
                    IfThenElse(
                        Equal( counter, Constant( 5 ) ),
                        YieldBreak(),
                        YieldReturn( counter )
                    ),
                    PostIncrementAssign( counter )
                )
            )
        );

        // Act
        var lambda = Lambda<Func<IEnumerable<int>>>( whileExpr );
        var compiledLambda = lambda.Compile( compiler );

        var results = compiledLambda().ToArray();

        // Assert:
        Assert.AreEqual( 5, results.Length );
        Assert.AreEqual( 0, results[0] );
        Assert.AreEqual( 1, results[1] );
        Assert.AreEqual( 2, results[2] );
        Assert.AreEqual( 3, results[3] );
        Assert.AreEqual( 4, results[4] );
    }
}
