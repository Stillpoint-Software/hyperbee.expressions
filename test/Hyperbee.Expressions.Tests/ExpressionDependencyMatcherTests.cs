using Hyperbee.Expressions.Tests.TestSupport;
using Hyperbee.Expressions.Visitors;
using static System.Linq.Expressions.Expression;
using static Hyperbee.Expressions.ExpressionExtensions;

namespace Hyperbee.Expressions.Tests;

[TestClass]
public class ExpressionDependencyMatcherTests
{
    [TestMethod]
    public void ExpressionDependencyMatcher_ShouldFindAwaits()
    {
        // Arrange

        var awaitExpr1 = Await( AsyncHelper.Completable(
            Constant( false ),
            Constant( 1 )
        ) );

        var awaitExpr2 = Await( AsyncHelper.Completable(
            Constant( false ),
            Constant( 2 )
        ) );

        var blockAsyncExpr = BlockAsync(
            awaitExpr1,
            awaitExpr2
        );

        var rootExpr = Block( blockAsyncExpr );
        var counter = new ExpressionMatcher( expr => expr is AsyncBlockExpression || expr is AwaitExpression );

        // Act
        var rootExprCount = counter.MatchCount( rootExpr );
        var blockAsyncExprCount = counter.MatchCount( blockAsyncExpr );
        var awaitExpr1Count = counter.MatchCount( awaitExpr1 );
        var awaitExpr2Count = counter.MatchCount( awaitExpr2 );

        // Assert
        Assert.AreEqual( 3, rootExprCount );
        Assert.AreEqual( 3, blockAsyncExprCount );
        Assert.AreEqual( 1, awaitExpr1Count );
        Assert.AreEqual( 1, awaitExpr2Count );
    }
}
