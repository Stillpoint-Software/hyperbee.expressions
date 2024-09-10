using System.Linq.Expressions;
using System.Reflection;

namespace Hyperbee.AsyncExpressions.Tests;

[TestClass]
public class AsyncBlockTests
{
    public static bool AreEqual(int a, int b) => a == b;

    public static MethodInfo GetMethod(string name) => typeof(AsyncBlockTests).GetMethod(name);

    [TestMethod]
    public void TestAsyncBlock_VariableScopeWithMultipleAwaits()
    {
        // Arrange
        var varExpr = Expression.Variable(typeof(int), "x");
        var assignExpr1 = Expression.Assign(varExpr, Expression.Constant(10));
        
        // First await expression
        var awaitExpr1 = AsyncExpression.Await(Expression.Constant(Task.CompletedTask, typeof( Task ) ), false);

        // Increment variable after first await
        var assignExpr2 = Expression.Assign(varExpr, Expression.Increment(varExpr));

        // Second await expression
        var awaitExpr2 = AsyncExpression.Await(Expression.Constant(Task.CompletedTask, typeof( Task ) ), false);

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
        Assert.AreEqual(3, reducedExpression.Expressions.Count); 
        
        var lambda = Expression.Lambda<Action>(reducedExpression);
        var compiledLambda = lambda.Compile();

        compiledLambda(); 
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
        var awaitExpr3 = AsyncExpression.Await( Expression.Constant( Task.CompletedTask, typeof(Task) ), false );
        var expr4 = Expression.Constant( 4 );

        var asyncBlock = AsyncExpression.BlockAsync( expr1, expr2, awaitExpr3, expr4 );

        // Act
        var reducedExpression = asyncBlock.ConvertToAwaitableBlock( out _ );

        // Assert
        Assert.IsNotNull( reducedExpression );
        Assert.AreEqual(2, reducedExpression.Expressions.Count); 
    }

    [TestMethod]
    public async Task TestAsyncBlock_WithoutParameters_ReturnsResult()
    {
        // Arrange
        var expr1 = Expression.Constant( 1 );
        var expr2 = Expression.Constant( 2 );
        var awaitExpr3 = AsyncExpression.Await( Expression.Constant( Task.FromResult( 3 ) ), false  );
        var expr4 = Expression.Constant( 4 );
        var expr5 = Expression.Constant( 5 );
        var awaitExpr6 = AsyncExpression.Await( Expression.Constant( Task.CompletedTask, typeof(Task) ), false );
        var expr7 = Expression.Constant( 7 );

        var asyncBlock = AsyncExpression.BlockAsync( expr1, expr2, awaitExpr3, expr4, expr5, awaitExpr6, expr7 );

        // Act

        var lambda = Expression.Lambda<Func<Task<int>>>( asyncBlock );
        var compiledLambda = lambda.Compile();
        var result = await compiledLambda();

        // Assert
        Assert.AreEqual( 7, result ); 
    }

    [TestMethod]
    public async Task TestAsyncBlock_WithParameters_ReturnsResult()
    {
        // Arrange
        var param1 = Expression.Parameter( typeof( int ), "param1" );
        var var1 = Expression.Variable( typeof( int ), "var1" );
        var var2 = Expression.Variable( typeof( int ), "var2" );

        var exp1 = Expression.Assign( var1, Expression.Constant( 1 ) );
        var awaitExpr2 = AsyncExpression.Await( Expression.Constant( Task.FromResult( 3 ) ), false );
        var exp3 = Expression.Assign( var2, awaitExpr2 );
        var add = Expression.Add( var1, Expression.Add( var2, param1 ) );
        
        var asyncBlock = AsyncExpression.BlockAsync(
            [var1, var2],
            exp1, exp3, add
        );

        // Act

        var lambda = Expression.Lambda<Func<int, Task<int>>>( asyncBlock, param1 );
        var compiledLambda = lambda.Compile();
        var result = await compiledLambda(3);

        // Assert
        Assert.AreEqual( 7, result ); 
    }

    [TestMethod]
    public void TestAsyncBlock_SingleAwait()
    {
        // Arrange
        var varExpr = Expression.Variable(typeof(int), "x");
        var assignExpr = Expression.Assign(varExpr, Expression.Constant(5));
        var awaitExpr = AsyncExpression.Await(Expression.Constant(Task.CompletedTask, typeof(Task)), false);
        var assertExpr = Expression.Call(
            GetMethod(nameof(AreEqual)),
            Expression.Constant(5),
            varExpr);

        var asyncBlock = AsyncExpression.BlockAsync( assignExpr, awaitExpr, assertExpr );

        // Act
        var reducedExpression = asyncBlock.ConvertToAwaitableBlock( out _ );

        // Assert
        Assert.IsNotNull(reducedExpression);
        Assert.AreEqual(2, reducedExpression.Expressions.Count); 
        var lambda = Expression.Lambda<Action>(asyncBlock);
        var compiledLambda = lambda.Compile();

        compiledLambda(); 
    }

    [TestMethod]
    public void TestAsyncBlock_MultipleAwaitAndVariableUpdate()
    {
        // Arrange
        var varExpr = Expression.Variable(typeof(int), "x");
        var assignExpr1 = Expression.Assign(varExpr, Expression.Constant(1));
        var awaitExpr1 = AsyncExpression.Await(Expression.Constant(Task.CompletedTask, typeof( Task ) ), false);
        var assignExpr2 = Expression.Assign(varExpr, Expression.Add(varExpr, Expression.Constant(2)));
        var awaitExpr2 = AsyncExpression.Await(Expression.Constant(Task.CompletedTask, typeof( Task ) ), false);
        var assertExpr = Expression.Call(
            GetMethod(nameof(AreEqual)),
            Expression.Constant(3),
            varExpr);

        var asyncBlock = AsyncExpression.BlockAsync( assignExpr1, awaitExpr1, assignExpr2, awaitExpr2, assertExpr );

        // Act
        var reducedExpression = asyncBlock.ConvertToAwaitableBlock( out _ );

        // Assert
        Assert.IsNotNull(reducedExpression);
        Assert.AreEqual(3, reducedExpression.Expressions.Count); 
        var lambda = Expression.Lambda<Action>(reducedExpression);
        var compiledLambda = lambda.Compile();

        compiledLambda(); 
    }

    [TestMethod]
    public void TestAsyncBlock_NestedAsyncBlock()
    {
        // Arrange
        var varExpr = Expression.Variable( typeof(int), "x" );
        var assignExpr1 = Expression.Assign( varExpr, Expression.Constant( 1 ) );
        var awaitExpr1 = AsyncExpression.Await( Expression.Constant( Task.CompletedTask, typeof(Task) ), false );

        // Inner async block
        var innerAssign = Expression.Assign( varExpr, Expression.Add( varExpr, Expression.Constant( 2 ) ) );
        var innerAwait = AsyncExpression.Await( Expression.Constant( Task.CompletedTask, typeof(Task) ), false );
        var innerBlock = AsyncExpression.BlockAsync( innerAssign, innerAwait );

        var assertExpr = Expression.Call(
            GetMethod( nameof(AreEqual) ),
            Expression.Constant( 3 ),
            varExpr );

        var asyncBlock = AsyncExpression.BlockAsync( assignExpr1, awaitExpr1, innerBlock, assertExpr );

        // Act
        var reducedExpression = asyncBlock.ConvertToAwaitableBlock( out _ );

        // Assert
        Assert.IsNotNull( reducedExpression );
        Assert.AreEqual( 2, reducedExpression.Expressions.Count ); 
        var lambda = Expression.Lambda<Action>( reducedExpression );
        var compiledLambda = lambda.Compile();

        compiledLambda(); 
    }
}
