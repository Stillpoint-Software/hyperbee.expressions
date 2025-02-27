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
}
