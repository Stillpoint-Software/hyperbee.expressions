using System.Linq.Expressions;
using System.Reflection;

namespace Hyperbee.AsyncExpressions.Tests;

[TestClass]
public class AsyncBlockTests
{
    public static bool AreEqual(int a, int b) => a == b;
    public static MethodInfo GetMethod(string name) => typeof(AsyncBlockTests).GetMethod(name);

    [TestMethod]
    public async Task TestAsyncBlock_VariableScopeWithMultipleAwaits()
    {
        // Arrange
        var varExpr = Expression.Variable(typeof(int), "x");
        var assignExpr1 = Expression.Assign(varExpr, Expression.Constant(10));
        
        // First await expression
        var awaitExpr1 = AsyncExpression.Await(Expression.Constant(Task.CompletedTask, typeof( Task ) ));

        // Increment variable after first await
        var assignExpr2 = Expression.Assign(varExpr, Expression.Increment(varExpr));

        // Second await expression
        var awaitExpr2 = AsyncExpression.Await(Expression.Constant(Task.CompletedTask, typeof( Task ) ));

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

        var asyncBlock = AsyncExpression.BlockAsync( 
            [varExpr], 
            assignExpr1, awaitExpr1, assignExpr2, awaitExpr2, assertExpr1, finalAssignExpr, assertExpr2 );

        // Act
        var lambda = Expression.Lambda<Func<Task<bool>>>( asyncBlock ); //BF discuss with ME
        var compiledLambda = lambda.Compile();
        var result = await compiledLambda();

        // Assert
        Assert.IsTrue( result );
    }

    [TestMethod]
    [ExpectedException(typeof(InvalidOperationException))]
    public async Task TestAsyncBlock_WithoutAwait_ThrowsException()
    {
        // Arrange
        var varExpr = Expression.Variable(typeof(int), "x");
        var assignExpr1 = Expression.Assign(varExpr, Expression.Constant(10));
        var assignExpr2 = Expression.Assign(varExpr, Expression.Increment(varExpr));
        var assertExpr = Expression.Call(
            GetMethod(nameof(AreEqual)),
            Expression.Constant(11),
            varExpr);

        var asyncBlock = AsyncExpression.BlockAsync(
            [varExpr],
            assignExpr1, assignExpr2, assertExpr );

        // Act
        var lambda = Expression.Lambda<Func<Task<bool>>>( asyncBlock );
        var compiledLambda = lambda.Compile();
        var result = await compiledLambda();

        // Assert
        Assert.IsTrue( result );
    }

    [TestMethod]
    public async Task TestAsyncBlock_SimpleBlockSplitting()
    {
        // Arrange
        var expr1 = Expression.Constant( 1 );
        var expr2 = Expression.Constant( 2 );
        var awaitExpr3 = AsyncExpression.Await( Expression.Constant( Task.CompletedTask, typeof(Task) ) );
        var expr4 = Expression.Constant( 4 );

        var asyncBlock = AsyncExpression.BlockAsync( expr1, expr2, awaitExpr3, expr4 );

        // Act
        var lambda = Expression.Lambda<Func<Task<int>>>( asyncBlock );
        var compiledLambda = lambda.Compile();
        var result = await compiledLambda();

        // Assert
        Assert.AreEqual( 4, result );
    }

    [TestMethod]
    public async Task TestAsyncBlock_WithoutParameters()
    {
        // Arrange
        var expr1 = Expression.Constant( 1 );
        var expr2 = Expression.Constant( 2 );
        var awaitExpr3 = AsyncExpression.Await( Expression.Constant( Task.FromResult( 3 ) )  );
        var expr4 = Expression.Constant( 4 );
        var expr5 = Expression.Constant( 5 );
        var awaitExpr6 = AsyncExpression.Await( Expression.Constant( Task.CompletedTask, typeof(Task) ) );
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
    public async Task TestAsyncBlock_WithParameters()
    {
        // Arrange
        var param1 = Expression.Parameter( typeof( int ), "param1" );
        var var1 = Expression.Variable( typeof( int ), "var1" );
        var var2 = Expression.Variable( typeof( int ), "var2" );

        var exp1 = Expression.Assign( var1, Expression.Constant( 1 ) );
        var awaitExpr2 = AsyncExpression.Await( Expression.Constant( Task.FromResult( 3 ) ) );
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
    public async Task TestAsyncBlock_SwitchWithParameters_VariableResult()
    {
        var param1 = Expression.Parameter( typeof( int ), "param1" );
        var var1 = Expression.Variable( typeof( int ), "var1" );

        var switchCase = Expression.Switch(
            param1,
            Expression.Constant( 0 ),
            [
                Expression.SwitchCase( 
                    Expression.Assign( var1, AsyncExpression.Await( Expression.Constant( Task.FromResult( 5 ) ) ) ), 
                    Expression.Constant( 1 ) ),
                Expression.SwitchCase( 
                    Expression.Assign( var1, AsyncExpression.Await( Expression.Constant( Task.FromResult( 7 ) ) ) ), 
                    Expression.Constant( 2 ) )
            ]
        );

        var asyncBlock = AsyncExpression.BlockAsync(
            [var1],
            switchCase, var1
        );

        // Act
        var lambda = Expression.Lambda<Func<int, Task<int>>>( asyncBlock, param1 );
        var compiledLambda = lambda.Compile();
        var switch1 = await compiledLambda( 1 );
        var switch2 = await compiledLambda( 2 );

        // Assert
        Assert.AreEqual( 5, switch1 );
        Assert.AreEqual( 7, switch2 );
    }

    [TestMethod]
    public async Task TestAsyncBlock_SwitchWithParameters_ReturnsResult()
    {
        var param1 = Expression.Parameter( typeof( int ), "param1" );
        var returnLabel = Expression.Label( typeof( int ), "returnTest" );

        var switchCase = Expression.Switch(
            param1,
            Expression.Return( returnLabel, AsyncExpression.Await( Expression.Constant( Task.FromResult( 2 ) ) ) ),
            [
                Expression.SwitchCase(
                    Expression.Return( returnLabel, AsyncExpression.Await( Expression.Constant( Task.FromResult( 5 ) ) ) ),
                    Expression.Constant( 1 ) ),
                Expression.SwitchCase(
                    Expression.Return( returnLabel, AsyncExpression.Await( Expression.Constant( Task.FromResult( 7 ) ) ) ),
                    Expression.Constant( 2 ) )
            ]
        );
        var returnExpr = Expression.Label( returnLabel, Expression.Constant( 9 ) );

        var asyncBlock = AsyncExpression.BlockAsync(
            [],
            switchCase, returnExpr
        );

        // Act
        var lambda = Expression.Lambda<Func<int, Task<int>>>( asyncBlock, param1 );
        var compiledLambda = lambda.Compile();
        var switch1 = await compiledLambda( 1 );
        var switch2 = await compiledLambda( 2 );

        // Assert
        Assert.AreEqual( 5, switch1 );
        Assert.AreEqual( 7, switch2 );
    }

    [TestMethod]
    public async Task TestAsyncBlock_ConditionalWithParameters_VariableResult()
    {
        // Arrange
        var param1 = Expression.Parameter( typeof( int ), "param1" );
        var param2 = Expression.Parameter( typeof( bool ), "param2" );
        var var1 = Expression.Variable( typeof( int ), "var1" );
        var var2 = Expression.Variable( typeof( int ), "var2" );

        var exp1 = Expression.Assign( var1, Expression.Constant( 1 ) );
        var awaitExpr2 = AsyncExpression.Await( Expression.Constant( Task.FromResult( 3 ) ) );
        var exp3 = Expression.Assign( var2, awaitExpr2 );
        var conditionalAdd = Expression.IfThenElse( param2,
            Expression.Assign( var2, Expression.Add( var2, param1 ) ),
            Expression.Assign( var2, Expression.Add( var1, Expression.Add( var2, param1 ) ) )
        );

        var asyncBlock = AsyncExpression.BlockAsync(
            [var1, var2],
            exp1, exp3, conditionalAdd, var2
        );

        // Act
        var lambda = Expression.Lambda<Func<int, bool, Task<int>>>( asyncBlock, param1, param2 );
        var compiledLambda = lambda.Compile();
        var resultTrue = await compiledLambda( 3, true );
        var resultFalse = await compiledLambda( 3, false );

        // Assert
        Assert.AreEqual( 6, resultTrue );
        Assert.AreEqual( 7, resultFalse );
    }

    [TestMethod]
    public async Task TestAsyncBlock_Conditional_AwaitTest()
    {
        // Arrange
        var var1 = Expression.Variable( typeof( int ), "var1" );
        var var2 = Expression.Variable( typeof( int ), "var2" );

        var exp1 = Expression.Assign( var1, Expression.Constant( 1 ) );
        var awaitConditionTest = AsyncExpression.Await( Expression.Constant( Task.FromResult( true ) ) );
        var conditionalAdd = Expression.IfThenElse( awaitConditionTest,
            Expression.Assign( var2, Expression.Add( var2, var1 ) ),
            Expression.Assign( var2, Expression.Add( var1, Expression.Add( var2, var2 ) ) )
        );

        var asyncBlock = AsyncExpression.BlockAsync(
            [var1, var2],
            exp1, conditionalAdd, var2
        );

        // Act
        var lambda = Expression.Lambda<Func<Task<int>>>( asyncBlock );
        var compiledLambda = lambda.Compile();
        var result = await compiledLambda(  );

        // Assert
        Assert.AreEqual( 1, result );
    }

    [TestMethod]
    public async Task TestAsyncBlock_ConditionalWithParameters_ReturnsResult()
    {
        // Arrange
        var param1 = Expression.Parameter( typeof( int ), "param1" );
        var param2 = Expression.Parameter( typeof( bool ), "param2" );
        var var1 = Expression.Variable( typeof( int ), "var1" );
        var var2 = Expression.Variable( typeof( int ), "var2" );
        var returnLabel = Expression.Label( typeof( int ), "returnTest" );

        var exp1 = Expression.Assign( var1, Expression.Constant( 1 ) );
        var awaitExpr2 = AsyncExpression.Await( Expression.Constant( Task.FromResult( 3 ) ) );
        var exp3 = Expression.Assign( var2, awaitExpr2 );
        var conditionalAdd = Expression.IfThenElse( param2,
            Expression.Return( returnLabel, Expression.Add( var2, param1 ) ),
            Expression.Return( returnLabel, Expression.Add( var1, Expression.Add( var2, param1 ) ) )
        );
        var returnExpr = Expression.Label( returnLabel, Expression.Constant( 9 ) );

        var asyncBlock = AsyncExpression.BlockAsync(
            [var1, var2],
            exp1, exp3, conditionalAdd, returnExpr
        );

        // Act
        var lambda = Expression.Lambda<Func<int, bool, Task<int>>>( asyncBlock, param1, param2 );
        var compiledLambda = lambda.Compile();
        var resultTrue = await compiledLambda( 3, true );
        var resultFalse = await compiledLambda( 3, false );

        // Assert
        Assert.AreEqual( 6, resultTrue );
        Assert.AreEqual( 7, resultFalse );
    }

    [TestMethod]
    public async Task TestAsyncBlock_SingleAwait()
    {
        // Arrange
        var varExpr = Expression.Variable( typeof( int ), "x" );
        var assignExpr = Expression.Assign( varExpr, Expression.Constant( 5 ) );
        var awaitExpr = AsyncExpression.Await( Expression.Constant( Task.CompletedTask, typeof( Task ) ) );
        var assertExpr = Expression.Call(
            GetMethod( nameof( AreEqual ) ),
            Expression.Constant( 5 ),
            varExpr );

        var asyncBlock = AsyncExpression.BlockAsync(
            [varExpr],
            assignExpr, awaitExpr, assertExpr );

        // Act
        var lambda = Expression.Lambda<Func<Task<bool>>>( asyncBlock );
        var compiledLambda = lambda.Compile();
        var result = await compiledLambda();

        // Assert
        Assert.IsTrue( result );
    }

    [TestMethod]
    public async Task TestAsyncBlock_MultipleAwaitAndVariableUpdate()
    {
        // Arrange
        var varExpr = Expression.Variable(typeof(int), "x");
        var assignExpr1 = Expression.Assign(varExpr, Expression.Constant(1));
        var awaitExpr1 = AsyncExpression.Await(Expression.Constant(Task.CompletedTask, typeof( Task ) ));
        var assignExpr2 = Expression.Assign(varExpr, Expression.Add(varExpr, Expression.Constant(2)));
        var awaitExpr2 = AsyncExpression.Await(Expression.Constant(Task.CompletedTask, typeof( Task ) ));
        var assertExpr = Expression.Call(
            GetMethod(nameof(AreEqual)),
            Expression.Constant(3),
            varExpr);

        var asyncBlock = AsyncExpression.BlockAsync(
            [varExpr],
            assignExpr1, awaitExpr1, assignExpr2, awaitExpr2, assertExpr );

        // Act
        var lambda = Expression.Lambda<Func<Task<bool>>>( asyncBlock );
        var compiledLambda = lambda.Compile();
        var result = await compiledLambda();

        // Assert
        Assert.IsTrue( result );
    }

    [TestMethod]
    public async Task TestAsyncBlock_NestedAsyncBlock()
    {
        // Arrange
        var varExpr = Expression.Variable( typeof(int), "x" );
        var assignExpr1 = Expression.Assign( varExpr, Expression.Constant( 1 ) );
        var awaitExpr1 = AsyncExpression.Await( Expression.Constant( Task.CompletedTask, typeof(Task) ) );

        // Inner async block
        var innerAssign = Expression.Assign( varExpr, Expression.Add( varExpr, Expression.Constant( 2 ) ) );
        var innerAwait = AsyncExpression.Await( Expression.Constant( Task.CompletedTask, typeof(Task) ) );
        var innerBlock = AsyncExpression.Await( 
            AsyncExpression.BlockAsync( innerAssign, innerAwait, varExpr ) );

        var assertExpr = Expression.Call(
            GetMethod( nameof(AreEqual) ),
            Expression.Constant( 3 ),
            varExpr );

        var asyncBlock = AsyncExpression.BlockAsync(
            [varExpr],
            assignExpr1, awaitExpr1, innerBlock, assertExpr );

        // Act
        var lambda = Expression.Lambda<Func<Task<bool>>>( asyncBlock );
        var compiledLambda = lambda.Compile();
        var result = await compiledLambda();

        // Assert
        Assert.IsTrue( result );
    }

    [TestMethod]
    public async Task TestAsyncBlock_LoopAsyncBlock()
    {
        // Arrange
        var breakLabel = Expression.Label( typeof( int ), "breakLoop" );
        var continueLabel = Expression.Label( "continueLoop" );
        var var1 = Expression.Variable( typeof(int), "x" );

        var asyncLoopBlock = AsyncExpression.BlockAsync(
            [var1],
            Expression.Assign( var1, Expression.Constant( 0 ) ),
            Expression.Loop(
                Expression.Block(
                    Expression.Assign( var1, Expression.Add( var1, Expression.Constant( 1 ) ) ),
                    AsyncExpression.Await( Expression.Constant( Task.CompletedTask, typeof( Task ) ) ),
                    Expression.IfThenElse( Expression.GreaterThanOrEqual( var1, Expression.Constant( 5 ) ),
                        Expression.Break( breakLabel, var1 ),
                        Expression.Continue( continueLabel ) )
                ),
                breakLabel,
                continueLabel
            ),
            var1  // TODO: Should this be required?
        );

        // Act
        var lambda = Expression.Lambda<Func<Task<int>>>( asyncLoopBlock );
        var compiledLambda = lambda.Compile();
        var result = await compiledLambda();

        // Assert
        Assert.AreEqual( 5, result );
    }
}
