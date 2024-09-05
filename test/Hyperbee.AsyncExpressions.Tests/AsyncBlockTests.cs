using System.Linq.Expressions;
using System.Reflection;

namespace Hyperbee.AsyncExpressions.Tests;

[TestClass]
public class AsyncBlockTests
{
    // Helper method to replace Assert.AreEqual for int comparisons
    public static bool AreEqual(int a, int b) => a == b;

    // Helper method to retrieve MethodInfo for test methods
    public static MethodInfo GetMethod(string name) => typeof(AsyncBlockTests).GetMethod(name);

    [TestMethod]
    public void TestAsyncBlock_VariableScopeWithMultipleAwaits()
    {
        // Arrange
        var varExpr = Expression.Variable(typeof(int), "x");
        var assignExpr1 = Expression.Assign(varExpr, Expression.Constant(10));
        
        // First await expression
        var awaitExpr1 = AsyncExpression.Await(Expression.Constant(Task.CompletedTask), false);

        // Increment variable after first await
        var assignExpr2 = Expression.Assign(varExpr, Expression.Increment(varExpr));

        // Second await expression
        var awaitExpr2 = AsyncExpression.Await(Expression.Constant(Task.CompletedTask), false);

        // Assert to check if variable maintains scope and has the expected value
        var assertExpr1 = Expression.Call(
            GetMethod(nameof(AreEqual)),
            Expression.Constant(11),
            varExpr);

        // Trailing expression after the last await
        var finalAssignExpr = Expression.Assign(varExpr, Expression.Add(varExpr, Expression.Constant(1)));
        var assertExpr2 = Expression.Call(
            GetMethod(nameof(AreEqual)),
            Expression.Constant(12),
            varExpr);

        var asyncBlock = AsyncExpression.BlockAsync( assignExpr1, awaitExpr1, assignExpr2, awaitExpr2, assertExpr1, finalAssignExpr, assertExpr2 );

        // Act
        var reducedExpression = asyncBlock.Reduce() as BlockExpression;

        // Assert
        Assert.IsNotNull(reducedExpression);
        Assert.AreEqual(3, reducedExpression.Expressions.Count); // Should result in three sub-blocks
        var lambda = Expression.Lambda<Action>(reducedExpression);
        var compiledLambda = lambda.Compile();

        compiledLambda(); // Should execute without assertion failures
    }

    [TestMethod]
    [ExpectedException(typeof(InvalidOperationException))]
    public void TestAsyncBlock_WithoutAwait_ThrowsException()
    {
        // Arrange
        var varExpr = Expression.Variable(typeof(int), "x");
        var assignExpr1 = Expression.Assign(varExpr, Expression.Constant(10));
        var assignExpr2 = Expression.Assign(varExpr, Expression.Increment(varExpr));
        var assertExpr = Expression.Call(
            GetMethod(nameof(AreEqual)),
            Expression.Constant(11),
            varExpr);

        var asyncBlock = AsyncExpression.BlockAsync( assignExpr1, assignExpr2, assertExpr );

        // Act
        asyncBlock.Reduce();
    }

    [TestMethod]
    public void TestAsyncBlock_SimpleBlockSplitting()
    {
        // Arrange
        var expr1 = Expression.Constant( 1 );
        var expr2 = Expression.Constant( 2 );
        var awaitExpr3 = AsyncExpression.Await( Expression.Constant( Task.CompletedTask ), false );
        var expr4 = Expression.Constant( 4 );

        var asyncBlock = AsyncExpression.BlockAsync( expr1, expr2, awaitExpr3, expr4 );

        // Act
        var reducedExpression = asyncBlock.Reduce() as BlockExpression;

        // Assert
        Assert.IsNotNull( reducedExpression );
        Assert.AreEqual(2, reducedExpression.Expressions.Count); // Should result in two sub-blocks
    }

    [TestMethod]
    public async Task TestAsyncBlock_StartStateMachine()
    {
        // Arrange
        var expr1 = Expression.Constant( 1 );
        var expr2 = Expression.Constant( 2 );
        var awaitExpr3 = AsyncExpression.Awaitable( Expression.Constant( Task.FromResult( 3 ) ) );
        var expr4 = Expression.Constant( 4 );
        var expr5 = Expression.Constant( 5 );
        var awaitExpr6 = AsyncExpression.Awaitable( Expression.Constant( Task.CompletedTask, typeof(Task) ) );
        var expr7 = Expression.Constant( 7 );

        var asyncBlock = AsyncExpression.BlockAsync( expr1, expr2, awaitExpr3, expr4, expr5, awaitExpr6, expr7 );

        // Act
        var body = asyncBlock.StartStateMachine();

        var lambda = Expression.Lambda<Func<Task<int>>>( body );
        var compiledLambda = lambda.Compile();
        var result = await compiledLambda();

        // Assert
        Assert.AreEqual( 7, result ); // Should return last expression value
    }

    [TestMethod]
    public void TestAsyncBlock_SingleAwait()
    {
        // Arrange
        var varExpr = Expression.Variable(typeof(int), "x");
        var assignExpr = Expression.Assign(varExpr, Expression.Constant(5));
        var awaitExpr = AsyncExpression.Await(Expression.Constant(Task.CompletedTask), false);
        var assertExpr = Expression.Call(
            GetMethod(nameof(AreEqual)),
            Expression.Constant(5),
            varExpr);

        var asyncBlock = AsyncExpression.BlockAsync( assignExpr, awaitExpr, assertExpr );

        // Act
        var reducedExpression = asyncBlock.Reduce() as BlockExpression;

        // Assert
        Assert.IsNotNull(reducedExpression);
        Assert.AreEqual(2, reducedExpression.Expressions.Count); // Should result in two sub-blocks
        var lambda = Expression.Lambda<Action>(reducedExpression);
        var compiledLambda = lambda.Compile();

        compiledLambda(); // Should execute without assertion failures
    }

    [TestMethod]
    public void TestAsyncBlock_MultipleAwaitAndVariableUpdate()
    {
        // Arrange
        var varExpr = Expression.Variable(typeof(int), "x");
        var assignExpr1 = Expression.Assign(varExpr, Expression.Constant(1));
        var awaitExpr1 = AsyncExpression.Await(Expression.Constant(Task.CompletedTask), false);
        var assignExpr2 = Expression.Assign(varExpr, Expression.Add(varExpr, Expression.Constant(2)));
        var awaitExpr2 = AsyncExpression.Await(Expression.Constant(Task.CompletedTask), false);
        var assertExpr = Expression.Call(
            GetMethod(nameof(AreEqual)),
            Expression.Constant(3),
            varExpr);

        var asyncBlock = AsyncExpression.BlockAsync( assignExpr1, awaitExpr1, assignExpr2, awaitExpr2, assertExpr );

        // Act
        var reducedExpression = asyncBlock.Reduce() as BlockExpression;

        // Assert
        Assert.IsNotNull(reducedExpression);
        Assert.AreEqual(3, reducedExpression.Expressions.Count); // Should result in three sub-blocks
        var lambda = Expression.Lambda<Action>(reducedExpression);
        var compiledLambda = lambda.Compile();

        compiledLambda(); // Should execute without assertion failures
    }

    [TestMethod]
    public void TestAsyncBlock_NestedAsyncBlock()
    {
        // Arrange
        var varExpr = Expression.Variable(typeof(int), "x");
        var assignExpr1 = Expression.Assign(varExpr, Expression.Constant(1));
        var awaitExpr1 = AsyncExpression.Await(Expression.Constant(Task.CompletedTask), false);

        // Inner async block
        var innerAssign = Expression.Assign(varExpr, Expression.Add(varExpr, Expression.Constant(2)));
        var innerAwait = AsyncExpression.Await(Expression.Constant(Task.CompletedTask), false);
        var innerBlock = AsyncExpression.BlockAsync( innerAssign, innerAwait );

        var assertExpr = Expression.Call(
            GetMethod(nameof(AreEqual)),
            Expression.Constant(3),
            varExpr);

        var asyncBlock = AsyncExpression.BlockAsync( assignExpr1, awaitExpr1, innerBlock, assertExpr );

        // Act
        var reducedExpression = asyncBlock.Reduce() as BlockExpression;

        // Assert
        Assert.IsNotNull(reducedExpression);
        Assert.AreEqual(2, reducedExpression.Expressions.Count); // Should result in three sub-blocks (outer block and inner async treated as a single block)
        var lambda = Expression.Lambda<Action>(reducedExpression);
        var compiledLambda = lambda.Compile();

        compiledLambda(); // Should execute without assertion failures
    }
}
