using System.Linq.Expressions;
using System.Reflection;
using static System.Linq.Expressions.Expression;
using static Hyperbee.Expressions.ExpressionExtensions;

namespace Hyperbee.Expressions.Tests;

public enum ExpressionKind
{
    Lambda,
    Method
}

[TestClass]
public class AwaiterTests
{
    [TestMethod]
    public void GetAwaiterResult_NoParameters()
    {
        var methodInfo = GetMethodInfo( nameof( GetNumberAsync ) );

        var asyncExpression = Call( methodInfo );
        var awaitExpression = Await( asyncExpression, configureAwait: false );

        var lambda = Lambda<Func<int>>( awaitExpression );
        var compiledLambda = lambda.Compile();

        var result = compiledLambda();
        Assert.AreEqual( 42, result, "The result should be 42." );
    }

    [TestMethod]
    public void GetAwaiterResult_NoResults()
    {
        var methodInfo = GetMethodInfo( nameof( Delay ) );

        var asyncExpression = Call( methodInfo );
        var awaitExpression = Await( asyncExpression, configureAwait: false );

        var lambda = Lambda<Action>( awaitExpression );
        var compiledLambda = lambda.Compile();

        compiledLambda();
    }

    [TestMethod]
    public void GetAwaiterResult_WithParameters()
    {
        var methodInfo = GetMethodInfo( nameof( AddThreeNumbersAsync ) );

        var paramExpr1 = Parameter( typeof( int ), "a" );
        var paramExpr2 = Parameter( typeof( int ), "b" );
        var paramExpr3 = Parameter( typeof( int ), "c" );

        var asyncExpression = Call( methodInfo!, paramExpr1, paramExpr2, paramExpr3 );
        var awaitExpression = Await( asyncExpression, configureAwait: false );

        var lambda = Lambda<Func<int, int, int, int>>( awaitExpression, paramExpr1, paramExpr2, paramExpr3 );
        var compiledLambda = lambda.Compile();

        var result = compiledLambda( 10, 20, 12 );
        Assert.AreEqual( 42, result, "The result should be 42." );
    }

    [TestMethod]
    public void GetAwaiterResult_WithConstants()
    {
        var methodInfo = GetMethodInfo( nameof( AddThreeNumbersAsync ) );

        var paramExpr1 = Constant( 10 );
        var paramExpr2 = Constant( 20 );
        var paramExpr3 = Constant( 12 );

        var asyncExpression = Call( methodInfo!, paramExpr1, paramExpr2, paramExpr3 );
        var awaitExpression = Await( asyncExpression, configureAwait: false );

        var lambda = Lambda<Func<int>>( awaitExpression );
        var compiledLambda = lambda.Compile();

        var result = compiledLambda();
        Assert.AreEqual( 42, result, "The result should be 42." );
    }

    [TestMethod]
    public void GetAwaiterResult_WithAsyncParameter()
    {
        // var result = await SayHelloAsync( await AddTwoNumbersAsync( 10, 32 ) );

        var addTwoNumbersMethod = GetMethodInfo( nameof( AddTwoNumbersAsync ) );
        var sayHelloMethod = GetMethodInfo( nameof( SayHelloAsync ) );

        var paramA = Parameter( typeof( int ), "a" );
        var paramB = Parameter( typeof( int ), "b" );

        var asyncExpressionAdd = Call( addTwoNumbersMethod, paramA, paramB );
        var awaitExpressionAdd = Await( asyncExpressionAdd, configureAwait: false );

        var asyncExpressionSayHello = Call( sayHelloMethod, awaitExpressionAdd );
        var awaitExpressionSayHello = Await( asyncExpressionSayHello, configureAwait: false );

        var lambda = Lambda<Func<int, int, string>>( awaitExpressionSayHello, paramA, paramB );
        var compiledLambda = lambda.Compile();

        var result = compiledLambda( 10, 32 );

        Assert.AreEqual( "Hello 42", result, "The result should be 'Hello 42'." );
    }

    [TestMethod]
    public void GetAwaiterResult_WithMethodCallParameters()
    {
        // var result0 = IncrementValue( 11 );  
        // var result1 = await AddThreeNumbersAsync( 10, 20, result0 );

        var incrementMethodInfo = GetMethodInfo( nameof( IncrementValue ) );
        var methodInfo = GetMethodInfo( nameof( AddThreeNumbersAsync ) );

        var paramExpr1 = Parameter( typeof( int ), "a" );
        var paramExpr2 = Parameter( typeof( int ), "b" );
        var paramExpr3 = Parameter( typeof( int ), "c" );

        var incrementValueCall = Call( incrementMethodInfo!, paramExpr3 );

        var asyncExpression = Call( methodInfo!, paramExpr1, paramExpr2, incrementValueCall );
        var awaitExpression = Await( asyncExpression, configureAwait: false );

        var lambda = Lambda<Func<int, int, int, int>>( awaitExpression, paramExpr1, paramExpr2, paramExpr3 );
        var compiledLambda = lambda.Compile();

        var result = compiledLambda( 10, 20, 11 ); // Pass 10, 20, and 11 as parameters; IncrementValue will increment 11
        Assert.AreEqual( 42, result, "The result should be 42." );
    }

    [TestMethod]
    public void GetAwaiterResult_MultipleAsyncExpressions_SeparateAwaits()
    {
        var methodInfo1 = GetMethodInfo( nameof( GetNumberAsync ) );
        var methodInfo2 = GetMethodInfo( nameof( AddThreeNumbersAsync ) );

        var paramExpr1 = Parameter( typeof( int ), "a" );
        var paramExpr2 = Parameter( typeof( int ), "b" );
        var paramExpr3 = Parameter( typeof( int ), "c" );

        var asyncExpression1 = Call( methodInfo1 );
        var asyncExpression2 = Call( methodInfo2!, paramExpr1, paramExpr2, paramExpr3 );

        var awaitExpression1 = Await( asyncExpression1, configureAwait: false );
        var awaitExpression2 = Await( asyncExpression2, configureAwait: false );

        var lambda1 = Lambda<Func<int>>( awaitExpression1 );
        var lambda2 = Lambda<Func<int, int, int, int>>( awaitExpression2, paramExpr1, paramExpr2, paramExpr3 );

        var compiledLambda1 = lambda1.Compile();
        var compiledLambda2 = lambda2.Compile();

        var result1 = compiledLambda1();
        var result2 = compiledLambda2( 10, 20, 12 );

        Assert.AreEqual( 42, result1, "The first result should be 42." );
        Assert.AreEqual( 42, result2, "The second result should be 42." );
    }

    [TestMethod]
    public async Task GetAwaiterResult_ExternAwaitExpressions()
    {
        var addTwoNumbersMethod = GetMethodInfo( nameof( AddTwoNumbersAsync ) );

        // Create AsyncExpression for AddTwoNumbers
        var paramA = Parameter( typeof( int ), "a" );
        var paramB = Parameter( typeof( int ), "b" );

        var asyncAddTwoNumbers = Call( addTwoNumbersMethod!, paramA, paramB );
        var awaitAddTwoNumbers = Await( asyncAddTwoNumbers, configureAwait: false );

        var resultFromAdd = Variable( typeof( int ), "resultFromAdd" );

        // Create the "Hello " + resultFromAdd expression
        var helloStringExpression = Constant( "Hello " );
        var resultToStringExpression = Call( resultFromAdd, typeof( object ).GetMethod( "ToString", Type.EmptyTypes )! );
        var helloConcatExpression = Call(
            typeof( string ).GetMethod( "Concat", [typeof( string ), typeof( string )] )!,
            helloStringExpression,
            resultToStringExpression
        );

        // Wrap the concatenated string in Task.FromResult
        var taskFromResultMethod = typeof( Task ).GetMethod( "FromResult" )!.MakeGenericMethod( typeof( string ) );
        var taskWrappedExpression = Call( taskFromResultMethod, helloConcatExpression );

        // Combine the expressions in a block
        var combinedExpression = Block(
            [resultFromAdd],
            Assign( resultFromAdd, awaitAddTwoNumbers ),
            taskWrappedExpression
        );

        // Compile the nested expression into a lambda and execute it
        var lambda = Lambda<Func<int, int, Task<string>>>( combinedExpression, paramA, paramB );

        var asyncLambda = Invoke( lambda, paramA, paramB );
        var compiledLambda = Lambda<Func<int, int, Task<string>>>( asyncLambda, paramA, paramB ).Compile();

        var result = await compiledLambda( 32, 10 );

        // Assert the result
        Assert.AreEqual( "Hello 42", result, "The result should be 'Hello 42'." );
    }

    [TestMethod]
    public async Task GetAwaiterResult_MultipleAsyncExpressions_WithDeepNestingAsync()
    {
        var incrementExpression = ToExpression( Increment );

        var paramA = Parameter( typeof( Task<int> ), "a" );

        var l1 = Invoke( incrementExpression, paramA );
        var l2 = Invoke( incrementExpression, l1 );
        var l3 = Invoke( incrementExpression, l2 );

        var compiled = Lambda<Func<Task<int>, Task<int>>>( l3, paramA ).Compile();
        var expressionResult = await compiled( Task.FromResult( 2 ) );

        var runtimeResult = await Increment( Increment( Increment( Task.FromResult( 2 ) ) ) );

        Assert.AreEqual( runtimeResult, expressionResult );

        return;

        static Expression<Func<Task<int>, Task<int>>> ToExpression( Func<Task<int>, Task<int>> func ) => task => func( task );

        static async Task<int> Increment( Task<int> previousTask )
        {
            int previousResult = await previousTask;
            return previousResult + 1;
        }
    }

    [TestMethod]
    public async Task GetAwaiterResult_MultipleAsyncExpressions_WithDeepNestingAsyncAwait()
    {
        var incrementExpression = ToExpression( Increment );

        var paramA = Parameter( typeof( int ), "a" );

        var l1 = Await( Invoke( incrementExpression, paramA ), configureAwait: false );
        var l2 = Await( Invoke( incrementExpression, l1 ), configureAwait: false );
        var l3 = Invoke( incrementExpression, l2 );

        var compiled = Lambda<Func<int, Task<int>>>( l3, paramA ).Compile();
        var expressionResult = await compiled( 2 );

        var runtimeResult = await Increment( await Increment( await Increment( 2 ) ) );

        Assert.AreEqual( runtimeResult, expressionResult );

        return;

        static Expression<Func<int, Task<int>>> ToExpression( Func<int, Task<int>> func ) => task => func( task );

        static async Task<int> Increment( int previous )
        {
            await Task.Delay( 10 );
            return previous + 1;
        }
    }

    [TestMethod]
    public void GetAwaiterResult_ChainedAwaitExpressions()
    {
        var addTwoNumbersMethod = GetMethodInfo( nameof( AddTwoNumbersAsync ) );
        var sayHelloMethod = GetMethodInfo( nameof( SayHelloAsync ) );

        // Create AsyncExpression and AwaitExpression for AddTwoNumbers
        var paramA = Parameter( typeof( int ), "a" );
        var paramB = Parameter( typeof( int ), "b" );

        var asyncAddTwoNumbers = Call( addTwoNumbersMethod!, paramA, paramB );
        var awaitAddTwoNumbers = Await( asyncAddTwoNumbers, configureAwait: false );

        var resultAddTwoNumbers = Variable( typeof( int ), "resultAddTwoNumbers" );

        // Create AsyncExpression and AwaitExpression for SayHello
        var asyncSayHello = Call( sayHelloMethod!, resultAddTwoNumbers );
        var awaitSayHello = Await( asyncSayHello, configureAwait: false );

        // Combine both expressions in a block
        var combinedExpression = Block(
            [resultAddTwoNumbers],
            Assign( resultAddTwoNumbers, awaitAddTwoNumbers ),
            awaitSayHello
        );

        // Compile and execute
        var lambda = Lambda<Func<int, int, string>>( combinedExpression, paramA, paramB );
        var compiledLambda = lambda.Compile();

        var result = compiledLambda( 32, 10 );

        Assert.AreEqual( "Hello 42", result, "The result should be 'Hello 42'." );
    }

    [TestMethod]
    public void GetAwaiterResult_ExceptionHandling()
    {
        var methodInfo = GetMethodInfo( nameof( ThrowExceptionAsync ) );

        var asyncThrowException = Call( methodInfo );
        var awaitThrowException = Await( asyncThrowException, configureAwait: false );

        var lambda = Lambda<Func<int>>( awaitThrowException );
        var compiledLambda = lambda.Compile();

        try
        {
            _ = compiledLambda();
            Assert.Fail( "An exception was not thrown." );
        }
        catch ( InvalidOperationException ex )
        {
            Assert.AreEqual( "Simulated exception.", ex.Message, "The exception message should match." );
        }
        catch ( Exception ex )
        {
            Assert.Fail( $"Unexpected exception of type {ex.GetType()} was thrown." );
        }
    }

    // Helpers

    private static async Task Delay()
    {
        await Task.Delay( 10 );
    }

    private static async Task<int> GetNumberAsync()
    {
        await Task.Delay( 10 );
        return 42;
    }

    private static async Task<int> AddTwoNumbersAsync( int a, int b )
    {
        await Task.Delay( 10 );
        return a + b;
    }

    private static async Task<int> AddThreeNumbersAsync( int a, int b, int c )
    {
        await Task.Delay( 10 );
        return a + b + c;
    }

    private static async Task<string> SayHelloAsync( int a )
    {
        await Task.Delay( 10 );
        return $"Hello {a}";
    }

    private static int IncrementValue( int a )
    {
        return a + 1;
    }

    private static async Task<int> ThrowExceptionAsync()
    {
        await Task.Delay( 10 );
        throw new InvalidOperationException( "Simulated exception." );
    }

    private static MethodInfo GetMethodInfo( string name )
    {
        return typeof(AwaiterTests).GetMethod( name, BindingFlags.Static | BindingFlags.NonPublic )!;
    }
}
