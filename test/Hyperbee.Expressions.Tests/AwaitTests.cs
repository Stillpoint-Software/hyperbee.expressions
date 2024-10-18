using System.Linq.Expressions;
using System.Reflection;

namespace Hyperbee.Expressions.Tests;

public enum ExpressionKind
{
    Lambda,
    Method
}

[TestClass]
public class AwaitTests
{
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
        return typeof( AwaitTests ).GetMethod( name, BindingFlags.Static | BindingFlags.NonPublic )!;
    }

    private static Expression GetAsyncExpression( ExpressionKind kind, MethodInfo methodInfo, params Expression[] arguments )
    {
        switch ( kind )
        {
            case ExpressionKind.Lambda:
                var (lambdaExpression, lambdaArguments) = GetLambdaExpression( methodInfo, arguments );
                return Expression.Invoke( lambdaExpression, lambdaArguments );

            case ExpressionKind.Method:
                return Expression.Call( methodInfo, arguments );

            default:
                throw new ArgumentOutOfRangeException( nameof( kind ) );
        }
    }

    private static (LambdaExpression Lambda, Expression[] Arguments) GetLambdaExpression( MethodInfo methodInfo, params Expression[] arguments )
    {
        if ( methodInfo.GetParameters().Length != arguments.Length )
        {
            throw new ArgumentException( "Number of arguments does not match the number of method parameters." );
        }

        var parameterExpressions = arguments.OfType<ParameterExpression>().ToArray();
        var lambdaArguments = parameterExpressions.Cast<Expression>().ToArray();

        var callExpression = Expression.Call( methodInfo, arguments );
        var lambdaExpression = Expression.Lambda( callExpression, parameterExpressions );

        return (lambdaExpression, lambdaArguments);
    }

    [DataTestMethod]
    [DataRow( ExpressionKind.Lambda )]
    [DataRow( ExpressionKind.Method )]
    public void TestAsyncExpression_NoParameters( ExpressionKind kind )
    {
        var methodInfo = GetMethodInfo( nameof( GetNumberAsync ) );

        var asyncExpression = GetAsyncExpression( kind, methodInfo! );
        var awaitExpression = ExpressionExtensions.Await( asyncExpression, configureAwait: false );

        var lambda = Expression.Lambda<Func<int>>( awaitExpression );
        var compiledLambda = lambda.Compile();

        var result = compiledLambda();
        Assert.AreEqual( 42, result, "The result should be 42." );
    }

    [DataTestMethod]
    [DataRow( ExpressionKind.Lambda )]
    [DataRow( ExpressionKind.Method )]
    public void TestAsyncExpression_NoResults( ExpressionKind kind )
    {
        var methodInfo = GetMethodInfo( nameof( Delay ) );

        var asyncExpression = GetAsyncExpression( kind, methodInfo! );
        var awaitExpression = ExpressionExtensions.Await( asyncExpression, configureAwait: false );

        var lambda = Expression.Lambda<Action>( awaitExpression );
        var compiledLambda = lambda.Compile();

        compiledLambda();
    }

    [DataTestMethod]
    [DataRow( ExpressionKind.Lambda )]
    [DataRow( ExpressionKind.Method )]
    public void TestAsyncExpression_WithParameters( ExpressionKind kind )
    {
        var methodInfo = GetMethodInfo( nameof( AddThreeNumbersAsync ) );

        var paramExpr1 = Expression.Parameter( typeof( int ), "a" );
        var paramExpr2 = Expression.Parameter( typeof( int ), "b" );
        var paramExpr3 = Expression.Parameter( typeof( int ), "c" );

        var asyncExpression = GetAsyncExpression( kind, methodInfo!, paramExpr1, paramExpr2, paramExpr3 );
        var awaitExpression = ExpressionExtensions.Await( asyncExpression, configureAwait: false );

        var lambda = Expression.Lambda<Func<int, int, int, int>>( awaitExpression, paramExpr1, paramExpr2, paramExpr3 );
        var compiledLambda = lambda.Compile();

        var result = compiledLambda( 10, 20, 12 );
        Assert.AreEqual( 42, result, "The result should be 42." );
    }

    [DataTestMethod]
    [DataRow( ExpressionKind.Lambda )]
    [DataRow( ExpressionKind.Method )]
    public void TestAsyncExpression_WithConstants( ExpressionKind kind )
    {
        var methodInfo = GetMethodInfo( nameof( AddThreeNumbersAsync ) );

        var paramExpr1 = Expression.Constant( 10 );
        var paramExpr2 = Expression.Constant( 20 );
        var paramExpr3 = Expression.Constant( 12 );

        var asyncExpression = GetAsyncExpression( kind, methodInfo!, paramExpr1, paramExpr2, paramExpr3 );
        var awaitExpression = ExpressionExtensions.Await( asyncExpression, configureAwait: false );

        var lambda = Expression.Lambda<Func<int>>( awaitExpression );
        var compiledLambda = lambda.Compile();

        var result = compiledLambda();
        Assert.AreEqual( 42, result, "The result should be 42." );
    }

    [DataTestMethod]
    [DataRow( ExpressionKind.Lambda )]
    [DataRow( ExpressionKind.Method )]
    public void TestAsyncExpression_WithAsyncParameter( ExpressionKind kind )
    {
        // var result = await SayHelloAsync( await AddTwoNumbersAsync( 10, 32 ) );

        var addTwoNumbersMethod = GetMethodInfo( nameof( AddTwoNumbersAsync ) );
        var sayHelloMethod = GetMethodInfo( nameof( SayHelloAsync ) );

        var paramA = Expression.Parameter( typeof( int ), "a" );
        var paramB = Expression.Parameter( typeof( int ), "b" );

        var asyncExpressionAdd = GetAsyncExpression( kind, addTwoNumbersMethod, paramA, paramB );
        var awaitExpressionAdd = ExpressionExtensions.Await( asyncExpressionAdd, configureAwait: false );

        var asyncExpressionSayHello = GetAsyncExpression( kind, sayHelloMethod, awaitExpressionAdd );
        var awaitExpressionSayHello = ExpressionExtensions.Await( asyncExpressionSayHello, configureAwait: false );

        var lambda = Expression.Lambda<Func<int, int, string>>( awaitExpressionSayHello, paramA, paramB );
        var compiledLambda = lambda.Compile();

        var result = compiledLambda( 10, 32 );

        Assert.AreEqual( "Hello 42", result, "The result should be 'Hello 42'." );
    }

    [DataTestMethod]
    [DataRow( ExpressionKind.Lambda )]
    [DataRow( ExpressionKind.Method )]
    public void TestAsyncExpression_WithMethodCallParameters( ExpressionKind kind )
    {
        // var result0 = IncrementValue( 11 );  
        // var result1 = await AddThreeNumbersAsync( 10, 20, result0 );

        var incrementMethodInfo = GetMethodInfo( nameof( IncrementValue ) );
        var methodInfo = GetMethodInfo( nameof( AddThreeNumbersAsync ) );

        var paramExpr1 = Expression.Parameter( typeof( int ), "a" );
        var paramExpr2 = Expression.Parameter( typeof( int ), "b" );
        var paramExpr3 = Expression.Parameter( typeof( int ), "c" );

        var incrementValueCall = Expression.Call( incrementMethodInfo!, paramExpr3 );

        var asyncExpression = GetAsyncExpression( kind, methodInfo!, paramExpr1, paramExpr2, incrementValueCall );
        var awaitExpression = ExpressionExtensions.Await( asyncExpression, configureAwait: false );

        var lambda = Expression.Lambda<Func<int, int, int, int>>( awaitExpression, paramExpr1, paramExpr2, paramExpr3 );
        var compiledLambda = lambda.Compile();

        var result = compiledLambda( 10, 20, 11 ); // Pass 10, 20, and 11 as parameters; IncrementValue will increment 11
        Assert.AreEqual( 42, result, "The result should be 42." );
    }

    [DataTestMethod]
    [DataRow( ExpressionKind.Lambda )]
    [DataRow( ExpressionKind.Method )]
    public void TestMultipleAsyncExpressions_SeparateAwaits( ExpressionKind kind )
    {
        var methodInfo1 = GetMethodInfo( nameof( GetNumberAsync ) );
        var methodInfo2 = GetMethodInfo( nameof( AddThreeNumbersAsync ) );

        var paramExpr1 = Expression.Parameter( typeof( int ), "a" );
        var paramExpr2 = Expression.Parameter( typeof( int ), "b" );
        var paramExpr3 = Expression.Parameter( typeof( int ), "c" );

        var asyncExpression1 = GetAsyncExpression( kind, methodInfo1! );
        var asyncExpression2 = GetAsyncExpression( kind, methodInfo2!, paramExpr1, paramExpr2, paramExpr3 );

        var awaitExpression1 = ExpressionExtensions.Await( asyncExpression1, configureAwait: false );
        var awaitExpression2 = ExpressionExtensions.Await( asyncExpression2, configureAwait: false );

        var lambda1 = Expression.Lambda<Func<int>>( awaitExpression1 );
        var lambda2 = Expression.Lambda<Func<int, int, int, int>>( awaitExpression2, paramExpr1, paramExpr2, paramExpr3 );

        var compiledLambda1 = lambda1.Compile();
        var compiledLambda2 = lambda2.Compile();

        var result1 = compiledLambda1();
        var result2 = compiledLambda2( 10, 20, 12 );

        Assert.AreEqual( 42, result1, "The first result should be 42." );
        Assert.AreEqual( 42, result2, "The second result should be 42." );
    }

    [DataTestMethod]
    [DataRow( ExpressionKind.Lambda )]
    [DataRow( ExpressionKind.Method )]
    public async Task TestScopedAwaitExpressions( ExpressionKind kind )
    {
        var addTwoNumbersMethod = GetMethodInfo( nameof( AddTwoNumbersAsync ) );

        // Create AsyncExpression for AddTwoNumbers
        var paramA = Expression.Parameter( typeof( int ), "a" );
        var paramB = Expression.Parameter( typeof( int ), "b" );

        var asyncAddTwoNumbers = GetAsyncExpression( kind, addTwoNumbersMethod!, paramA, paramB );
        var awaitAddTwoNumbers = ExpressionExtensions.Await( asyncAddTwoNumbers, configureAwait: false );

        var resultFromAdd = Expression.Variable( typeof( int ), "resultFromAdd" );

        // Create the "Hello " + resultFromAdd expression
        var helloStringExpression = Expression.Constant( "Hello " );
        var resultToStringExpression = Expression.Call( resultFromAdd, typeof( object ).GetMethod( "ToString", Type.EmptyTypes )! );
        var helloConcatExpression = Expression.Call(
            typeof( string ).GetMethod( "Concat", [typeof( string ), typeof( string )] )!,
            helloStringExpression,
            resultToStringExpression
        );

        // Wrap the concatenated string in Task.FromResult
        var taskFromResultMethod = typeof( Task ).GetMethod( "FromResult" )!.MakeGenericMethod( typeof( string ) );
        var taskWrappedExpression = Expression.Call( taskFromResultMethod, helloConcatExpression );

        // Combine the expressions in a block
        var combinedExpression = Expression.Block(
            [resultFromAdd],
            Expression.Assign( resultFromAdd, awaitAddTwoNumbers ),
            taskWrappedExpression
        );

        // Compile the nested expression into a lambda and execute it
        var lambda = Expression.Lambda<Func<int, int, Task<string>>>( combinedExpression, paramA, paramB );

        var asyncLambda = Expression.Invoke( lambda, paramA, paramB );
        var compiledLambda = Expression.Lambda<Func<int, int, Task<string>>>( asyncLambda, paramA, paramB ).Compile();

        var result = await compiledLambda( 32, 10 );

        // Assert the result
        Assert.AreEqual( "Hello 42", result, "The result should be 'Hello 42'." );
    }

    [TestMethod]
    public async Task TestMultipleAsyncExpressions_WithDeepNestingAsync()
    {
        var incrementExpression = ToExpression( Increment );

        var paramA = Expression.Parameter( typeof( Task<int> ), "a" );

        var l1 = Expression.Invoke( incrementExpression, paramA );
        var l2 = Expression.Invoke( incrementExpression, l1 );
        var l3 = Expression.Invoke( incrementExpression, l2 );

        var compiled = Expression.Lambda<Func<Task<int>, Task<int>>>( l3, paramA ).Compile();
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
    public async Task TestMultipleAsyncExpressions_WithDeepNestingAsyncAwait()
    {
        var incrementExpression = ToExpression( Increment );

        var paramA = Expression.Parameter( typeof( int ), "a" );

        var l1 = ExpressionExtensions.Await( Expression.Invoke( incrementExpression, paramA ), configureAwait: false );
        var l2 = ExpressionExtensions.Await( Expression.Invoke( incrementExpression, l1 ), configureAwait: false );
        var l3 = Expression.Invoke( incrementExpression, l2 );

        var compiled = Expression.Lambda<Func<int, Task<int>>>( l3, paramA ).Compile();
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

    [DataTestMethod]
    [DataRow( ExpressionKind.Lambda )]
    [DataRow( ExpressionKind.Method )]
    public void TestChainedAwaitExpressions( ExpressionKind kind )
    {
        var addTwoNumbersMethod = GetMethodInfo( nameof( AddTwoNumbersAsync ) );
        var sayHelloMethod = GetMethodInfo( nameof( SayHelloAsync ) );

        // Create AsyncExpression and AwaitExpression for AddTwoNumbers
        var paramA = Expression.Parameter( typeof( int ), "a" );
        var paramB = Expression.Parameter( typeof( int ), "b" );

        var asyncAddTwoNumbers = GetAsyncExpression( kind, addTwoNumbersMethod!, paramA, paramB );
        var awaitAddTwoNumbers = ExpressionExtensions.Await( asyncAddTwoNumbers, configureAwait: false );

        var resultAddTwoNumbers = Expression.Variable( typeof( int ), "resultAddTwoNumbers" );

        // Create AsyncExpression and AwaitExpression for SayHello
        var asyncSayHello = GetAsyncExpression( kind, sayHelloMethod!, resultAddTwoNumbers );
        var awaitSayHello = ExpressionExtensions.Await( asyncSayHello, configureAwait: false );

        // Combine both expressions in a block
        var combinedExpression = Expression.Block(
            [resultAddTwoNumbers],
            Expression.Assign( resultAddTwoNumbers, awaitAddTwoNumbers ),
            awaitSayHello
        );

        // Compile and execute
        var lambda = Expression.Lambda<Func<int, int, string>>( combinedExpression, paramA, paramB );
        var compiledLambda = lambda.Compile();

        var result = compiledLambda( 32, 10 );

        Assert.AreEqual( "Hello 42", result, "The result should be 'Hello 42'." );
    }

    [DataTestMethod]
    [DataRow( ExpressionKind.Lambda )]
    [DataRow( ExpressionKind.Method )]
    public void TestAsyncExpression_ExceptionHandling( ExpressionKind kind )
    {
        var methodInfo = GetMethodInfo( nameof( ThrowExceptionAsync ) );

        var asyncThrowException = GetAsyncExpression( kind, methodInfo! );
        var awaitThrowException = ExpressionExtensions.Await( asyncThrowException, configureAwait: false );

        var lambda = Expression.Lambda<Func<int>>( awaitThrowException );
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
}
