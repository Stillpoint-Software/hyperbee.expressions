using Hyperbee.Expressions.Tests.TestSupport;
using static System.Linq.Expressions.Expression;

namespace Hyperbee.Expressions.Tests;

[TestClass]
public class ConditionalExpressionTests
{
    [DataTestMethod]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Interpret )]
    public void ConditionalExpression_ShouldSucceed_WhenTrue( CompilerType compiler )
    {
        // Arrange
        var variable = Variable( typeof( int ), "variable" );

        var block = Block(
            [variable],
            Assign( variable, Constant( 0 ) ),
            IfThenElse(
                Constant( true ),
                Assign( variable, Constant( 10 ) ),
                Assign( variable, Constant( 3 ) )
            ),
            variable
        );

        // Act
        var lambda = Lambda<Func<int>>( block );
        var compiledLambda = lambda.Compile( compiler );

        var result = compiledLambda();

        // Assert
        Assert.AreEqual( 10, result );
    }
}
