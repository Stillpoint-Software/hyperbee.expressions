using Hyperbee.Expressions.Tests.TestSupport;
using static System.Linq.Expressions.Expression;
using static Hyperbee.Expressions.ExpressionExtensions;

namespace Hyperbee.Expressions.Tests;

// Lowering hoists local variables onto state-machine fields. A ParameterExpression is
// identified by instance, never by name: distinct instances may share a name, and a name
// may be null. These tests cover the cases where names collide or are absent.

[TestClass]
public class BlockYieldVariableScopeTests
{
    [TestMethod]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Interpret )]
    public void YieldBlock_ShouldNotConflateVariables_WithLambdaParameterOfSameName( CompilerType compiler )
    {
        // Arrange: an outer lambda parameter and a nested block local share the name "value"
        var input = Parameter( typeof( int ), "value" );
        var local = Variable( typeof( int ), "value" );

        var block = BlockEnumerable(
            Block(
                [local],
                Assign( local, Add( input, Constant( 5 ) ) ),
                YieldReturn( local )
            )
        );

        var lambda = Lambda<Func<int, IEnumerable<int>>>( block, input );
        var compiledLambda = lambda.Compile( compiler );

        // Act
        var result = compiledLambda( 10 ).ToArray();

        // Assert
        Assert.HasCount( 1, result );
        Assert.AreEqual( 15, result[0] );
    }

    [TestMethod]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Interpret )]
    public void YieldBlock_ShouldNotConflateVariables_InSiblingBlocksWithSameName( CompilerType compiler )
    {
        // Arrange: two distinct variables in sibling blocks share the name "v"
        var first = Variable( typeof( int ), "v" );
        var second = Variable( typeof( int ), "v" );

        var block = BlockEnumerable(
            Block(
                [first],
                Assign( first, Constant( 1 ) ),
                YieldReturn( first )
            ),
            Block(
                [second],
                Assign( second, Constant( 10 ) ),
                YieldReturn( second )
            )
        );

        var lambda = Lambda<Func<IEnumerable<int>>>( block );
        var compiledLambda = lambda.Compile( compiler );

        // Act
        var result = compiledLambda().ToArray();

        // Assert
        Assert.HasCount( 2, result );
        Assert.AreEqual( 1, result[0] );
        Assert.AreEqual( 10, result[1] );
    }

    [TestMethod]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Interpret )]
    public void YieldBlock_ShouldNotConflateVariables_WithoutNames( CompilerType compiler )
    {
        // Arrange: two unnamed variables in a nested block
        var first = Variable( typeof( int ) );
        var second = Variable( typeof( int ) );

        var block = BlockEnumerable(
            Block(
                [first, second],
                Assign( first, Constant( 1 ) ),
                Assign( second, Constant( 10 ) ),
                YieldReturn( Add( first, second ) )
            )
        );

        var lambda = Lambda<Func<IEnumerable<int>>>( block );
        var compiledLambda = lambda.Compile( compiler );

        // Act
        var result = compiledLambda().ToArray();

        // Assert
        Assert.HasCount( 1, result );
        Assert.AreEqual( 11, result[0] );
    }
}
