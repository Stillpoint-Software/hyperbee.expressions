using System.Reflection;
using static System.Linq.Expressions.Expression;

namespace Hyperbee.AsyncExpressions.Tests;

[TestClass]
public class AwaitSplitVisitorTests
{
    static int Test( int a, int b ) => a + b;

    [TestMethod]
    public async Task ShouldFindAwait_WhenUsingCall()
    {
        // Arrange
        var methodInfo = GetType()
            .GetMethod( nameof(Test), BindingFlags.Static | BindingFlags.NonPublic )!;

        var callExpr = Call(
            methodInfo,
            AsyncExpression.Await( Constant( Task.FromResult( 1 ) ), false ),
            AsyncExpression.Await( Constant( Task.FromResult( 2 ) ), false ) );

        // Act
        var visitor = new AwaitSplitVisitor();
        var v = visitor.Visit( callExpr );

        // Assert
        Assert.AreEqual( 5, visitor.Expressions.Count );

        var lambda = Lambda<Func<Task<int>>>( AsyncExpression.BlockAsync( callExpr ) );
        var result = await lambda.Compile()();
        Assert.AreEqual( 3, result );
    }

    [TestMethod]
    public void ShouldFindAwait_WhenUsingAssign()
    {
        // Arrange
        var varExpr = Variable( typeof(int), "x" );
        var assignExpr = Assign( varExpr,
            AsyncExpression.Await( Constant( Task.FromResult( 1 ) ), false ) );

        // Act
        var visitor = new AwaitSplitVisitor();
        visitor.Visit( assignExpr );

        // Assert
        Assert.AreEqual( 2, visitor.Expressions.Count );
    }

    [TestMethod]
    public async Task ShouldFindAwait_WhenUsingConditions()
    {
        // Arrange
        var ifThenExpr = IfThen( AsyncExpression.Await( Constant( Task.FromResult( true ) ), false ),
            AsyncExpression.Await( Constant( Task.FromResult( 1 ) ), false ) );

        // Act
        var visitor = new AwaitSplitVisitor();
        visitor.Visit( ifThenExpr );

        // Assert
        Assert.AreEqual( 3, visitor.Expressions.Count );

        var lambda = Lambda<Func<Task>>( AsyncExpression.BlockAsync( ifThenExpr ) );
        await lambda.Compile()();
    }
}
