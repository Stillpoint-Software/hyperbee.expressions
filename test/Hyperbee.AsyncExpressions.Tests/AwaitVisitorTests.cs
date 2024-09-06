using System.Reflection;
using static System.Linq.Expressions.Expression;

namespace Hyperbee.AsyncExpressions.Tests;

[TestClass]
public class AwaitVisitorTests
{
    static int Test( int a, int b ) => a + b;

    [TestMethod]
    public void ShouldFindAwait_WhenUsingCall()
    {
        // Arrange
        var callExpr = Call(
            typeof(AwaitVisitorTests).GetMethod( nameof(Test), BindingFlags.Static | BindingFlags.NonPublic )!,
            AsyncExpression.Await( Constant( Task.FromResult( 1 ) ), false ),
            AsyncExpression.Await( Constant( Task.FromResult( 2 ) ), false ) );

        // Act
        var visitor = new AwaitVisitor();
        visitor.Visit( callExpr );

        // Assert
        Assert.AreEqual( 3, visitor.Expressions.Count );
    }

    [TestMethod]
    public void ShouldFindAwait_WhenUsingAssign()
    {
        // Arrange
        var varExpr = Variable( typeof(int), "x" );
        var assignExpr = Assign( varExpr,
            AsyncExpression.Await( Constant( Task.FromResult( 1 ) ), false ) );

        // Act
        var visitor = new AwaitVisitor();
        visitor.Visit( assignExpr );

        // Assert
        Assert.AreEqual( 2, visitor.Expressions.Count );
    }

    [TestMethod]
    public void ShouldFindAwait_WhenUsingConditions()
    {
        // Arrange
        var assignExpr = IfThen( AsyncExpression.Await( Constant( Task.FromResult( true ) ), false ),
            AsyncExpression.Await( Constant( Task.FromResult( 1 ) ), false ) );

        // Act
        var visitor = new AwaitVisitor();
        visitor.Visit( assignExpr );

        // Assert
        Assert.AreEqual( 2, visitor.Expressions.Count );
    }
}
