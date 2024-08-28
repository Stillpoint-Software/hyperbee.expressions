using System.Linq.Expressions;
using System.Reflection;

namespace Hyperbee.AsyncExpressions.Tests;

public enum ExpressionKind
{
    Lambda,
    Method
}

[TestClass]
public class AsyncExpressionUnitTests
{
    private static async Task<int> GetNumberAsync()
    {
        await Task.Delay(100);
        return 42;
    }

    private static async Task<int> AddTwoNumbersAsync(int a, int b)
    {
        await Task.Delay(10);
        return a + b;
    }

    private static async Task<int> AddThreeNumbersAsync(int a, int b, int c)
    {
        await Task.Delay(100);
        return a + b + c;
    }

    private static async Task<string> SayHelloAsync(int a)
    {
        await Task.Delay(10);
        return $"Hello {a}";
    }

    private static int IncrementValue(int a)
    {
        return a + 1;
    }

    private static async Task<int> ThrowExceptionAsync()
    {
        await Task.Delay(50);
        throw new InvalidOperationException("Simulated exception");
    }

    private static MethodInfo GetMethodInfo(string name)
    {
        return typeof(AsyncExpressionUnitTests).GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic)!;
    }

    private static AsyncExpression GetAsyncExpression(ExpressionKind kind, MethodInfo methodInfo, params Expression[] arguments)
    {
        switch (kind)
        {
            case ExpressionKind.Lambda:
                var (lambdaExpression, parameters) = GetLambdaExpression(methodInfo, arguments);
                return AsyncExpression.LambdaAsync(lambdaExpression, parameters);

            case ExpressionKind.Method:
                return AsyncExpression.CallAsync(methodInfo, arguments);

            default:
                throw new ArgumentOutOfRangeException(nameof(kind));
        }
    }


    private static (LambdaExpression Lambda, Expression[] Parameters) GetLambdaExpression(MethodInfo methodInfo, params Expression[] arguments)
    {
        if (methodInfo.GetParameters().Length != arguments.Length)
        {
            throw new ArgumentException("Number of arguments does not match the number of method parameters.");
        }

        var visitor = new ParametersVisitor();

        foreach (var argument in arguments)
        {
            visitor.Visit(argument);
        }

        var parameterExpressions = visitor.VisitedParameters.ToArray();

        var callExpression = Expression.Call(methodInfo, arguments);
        var lambdaExpression = Expression.Lambda(callExpression, parameterExpressions);

        return (lambdaExpression, parameterExpressions.Cast<Expression>().ToArray());
    }

    public class ParametersVisitor : ExpressionVisitor
    {
        public List<ParameterExpression> VisitedParameters { get; } = [];

        protected override Expression VisitParameter(ParameterExpression node)
        {
            if (!VisitedParameters.Contains(node))
            {
                VisitedParameters.Add(node);
            }

            return base.VisitParameter(node);
        }
    }

    [DataTestMethod]
    [DataRow(ExpressionKind.Lambda)]
    [DataRow(ExpressionKind.Method)]
    public void TestAsyncExpression_NoParameters(ExpressionKind kind)
    {
        var methodInfo = GetMethodInfo(nameof(GetNumberAsync));

        var asyncExpression = GetAsyncExpression(kind, methodInfo!);
        var awaitExpression = AsyncExpression.Await(asyncExpression, configureAwait: false);

        var lambda = Expression.Lambda<Func<int>>(awaitExpression);
        var compiledLambda = lambda.Compile();

        var result = compiledLambda(); // Directly get the unwrapped result
        Assert.AreEqual(42, result, "The result should be 42.");
    }

    [DataTestMethod]
    [DataRow(ExpressionKind.Lambda)]
    [DataRow(ExpressionKind.Method)]
    public void TestAsyncExpression_WithParameters(ExpressionKind kind)
    {
        var methodInfo = GetMethodInfo(nameof(AddThreeNumbersAsync));

        var paramExpr1 = Expression.Parameter(typeof(int), "a");
        var paramExpr2 = Expression.Parameter(typeof(int), "b");
        var paramExpr3 = Expression.Parameter(typeof(int), "c");

        var asyncExpression = GetAsyncExpression(kind, methodInfo!, paramExpr1, paramExpr2, paramExpr3);
        var awaitExpression = AsyncExpression.Await(asyncExpression, configureAwait: false);

        var lambda = Expression.Lambda<Func<int, int, int, int>>(awaitExpression, paramExpr1, paramExpr2, paramExpr3);
        var compiledLambda = lambda.Compile();

        var result = compiledLambda(10, 20, 12); // Directly get the unwrapped result
        Assert.AreEqual(42, result, "The result should be 42.");
    }

    [DataTestMethod]
    [DataRow(ExpressionKind.Lambda)]
    [DataRow(ExpressionKind.Method)]
    public void TestAsyncExpression_WithConstants(ExpressionKind kind)
    {
        var methodInfo = GetMethodInfo(nameof(AddThreeNumbersAsync));

        var paramExpr1 = Expression.Constant(10);
        var paramExpr2 = Expression.Constant(20);
        var paramExpr3 = Expression.Constant(12);

        var asyncExpression = GetAsyncExpression(kind, methodInfo!, paramExpr1, paramExpr2, paramExpr3);
        var awaitExpression = AsyncExpression.Await(asyncExpression, configureAwait: false);

        var lambda = Expression.Lambda<Func<int>>(awaitExpression);
        var compiledLambda = lambda.Compile();

        var result = compiledLambda(); // Directly get the unwrapped result
        Assert.AreEqual(42, result, "The result should be 42.");
    }

    [DataTestMethod]
    [DataRow(ExpressionKind.Lambda)]
    [DataRow(ExpressionKind.Method)]
    public void TestAsyncExpression_WithMethodParameters(ExpressionKind kind)
    {
        // var result0 = IncrementValue( 11 );  
        // var result1 = await AddThreeNumbersAsync( 10, 20, result0 );

        var incrementMethodInfo = GetMethodInfo(nameof(IncrementValue));
        var methodInfo = GetMethodInfo(nameof(AddThreeNumbersAsync));

        var paramExpr1 = Expression.Parameter(typeof(int), "a");
        var paramExpr2 = Expression.Parameter(typeof(int), "b");
        var paramExpr3 = Expression.Parameter(typeof(int), "c");

        var incrementValueCall = Expression.Call(incrementMethodInfo!, paramExpr3);

        var asyncExpression = GetAsyncExpression(kind, methodInfo!, paramExpr1, paramExpr2, incrementValueCall);
        var awaitExpression = AsyncExpression.Await(asyncExpression, configureAwait: false);

        var lambda = Expression.Lambda<Func<int, int, int, int>>(awaitExpression, paramExpr1, paramExpr2, paramExpr3);
        var compiledLambda = lambda.Compile();

        var result = compiledLambda(10, 20, 11); // Pass 10, 20, and 11 as parameters; IncrementValue will increment 11
        Assert.AreEqual(42, result, "The result should be 42.");
    }

    [DataTestMethod]
    [DataRow(ExpressionKind.Lambda)]
    [DataRow(ExpressionKind.Method)]
    public void TestAsyncExpression_AsParameter(ExpressionKind kind)
    {
        // var result = await SayHelloAsync( await AddTwoNumbersAsync( 10, 32 ) );

        var addTwoNumbersMethod = GetMethodInfo(nameof(AddTwoNumbersAsync));
        var sayHelloMethod = GetMethodInfo(nameof(SayHelloAsync));

        var paramA = Expression.Parameter(typeof(int), "a");
        var paramB = Expression.Parameter(typeof(int), "b");

        var asyncExpressionAdd = GetAsyncExpression(kind, addTwoNumbersMethod, paramA, paramB);
        var awaitExpressionAdd = AsyncExpression.Await(asyncExpressionAdd, configureAwait: false);

        var asyncExpressionSayHello = GetAsyncExpression(kind, sayHelloMethod, awaitExpressionAdd);
        var awaitExpressionSayHello = AsyncExpression.Await(asyncExpressionSayHello, configureAwait: false);

        var lambda = Expression.Lambda<Func<int, int, string>>(awaitExpressionSayHello, paramA, paramB);
        var compiledLambda = lambda.Compile();

        var result = compiledLambda(10, 32);

        Assert.AreEqual("Hello 42", result, "The result should be 'Hello 42'.");
    }

    [DataTestMethod]
    [DataRow(ExpressionKind.Lambda)]
    [DataRow(ExpressionKind.Method)]
    public void TestMultipleAsyncExpressions_SeparateAwaits(ExpressionKind kind)
    {
        var methodInfo1 = GetMethodInfo(nameof(GetNumberAsync));
        var methodInfo2 = GetMethodInfo(nameof(AddThreeNumbersAsync));

        var paramExpr1 = Expression.Parameter(typeof(int), "a");
        var paramExpr2 = Expression.Parameter(typeof(int), "b");
        var paramExpr3 = Expression.Parameter(typeof(int), "c");

        var asyncExpression1 = GetAsyncExpression(kind, methodInfo1!);
        var asyncExpression2 = GetAsyncExpression(kind, methodInfo2!, paramExpr1, paramExpr2, paramExpr3);

        var awaitExpression1 = AsyncExpression.Await(asyncExpression1, configureAwait: false);
        var awaitExpression2 = AsyncExpression.Await(asyncExpression2, configureAwait: false);

        var lambda1 = Expression.Lambda<Func<int>>(awaitExpression1);
        var lambda2 = Expression.Lambda<Func<int, int, int, int>>(awaitExpression2, paramExpr1, paramExpr2, paramExpr3);

        var compiledLambda1 = lambda1.Compile();
        var compiledLambda2 = lambda2.Compile();

        var result1 = compiledLambda1(); // Directly get the unwrapped result
        var result2 = compiledLambda2(10, 20, 12); // Directly get the unwrapped result

        Assert.AreEqual(42, result1, "The first result should be 42.");
        Assert.AreEqual(42, result2, "The second result should be 42.");
    }

    [DataTestMethod]
    [DataRow(ExpressionKind.Lambda)]
    [DataRow(ExpressionKind.Method)]
    public void TestChainedAwaitExpressions(ExpressionKind kind)
    {
        var addTwoNumbersMethod = GetMethodInfo(nameof(AddTwoNumbersAsync));
        var sayHelloMethod = GetMethodInfo(nameof(SayHelloAsync));

        // Create AsyncExpression and AwaitExpression for AddTwoNumbers
        var paramA = Expression.Parameter(typeof(int), "a");
        var paramB = Expression.Parameter(typeof(int), "b");

        var asyncExpressionAdd = GetAsyncExpression(kind, addTwoNumbersMethod!, paramA, paramB);
        var awaitExpressionAdd = AsyncExpression.Await(asyncExpressionAdd, configureAwait: false);

        var resultFromAdd = Expression.Variable(typeof(int), "resultFromAdd");

        // Create AsyncExpression and AwaitExpression for SayHello
        var asyncExpressionSayHello = GetAsyncExpression(kind, sayHelloMethod!, resultFromAdd);
        var awaitExpressionSayHello = AsyncExpression.Await(asyncExpressionSayHello, configureAwait: false);

        // Combine both expressions in a block
        var combinedExpression = Expression.Block(
            new[] { resultFromAdd }, // Declare the variable to hold the intermediate result
            Expression.Assign(resultFromAdd, awaitExpressionAdd), // Assign result of AddTwoNumbers to resultFromAdd
            awaitExpressionSayHello // Execute SayHello and return its result
        );

        // Compile the combined expression into a lambda and execute it
        var lambda = Expression.Lambda<Func<int, int, string>>(combinedExpression, paramA, paramB);
        var compiledLambda = lambda.Compile();

        var result = compiledLambda(32, 10); // Execute with parameters 32 and 10

        // Assert the result
        Assert.AreEqual("Hello 42", result, "The result should be 'Hello 42'.");
    }

    [DataTestMethod]
    [DataRow(ExpressionKind.Lambda)]
    [DataRow(ExpressionKind.Method)]
    public void TestAsyncExpression_ExceptionHandling(ExpressionKind kind)
    {
        var methodInfo = GetMethodInfo(nameof(ThrowExceptionAsync));

        var asyncExpression = GetAsyncExpression(kind, methodInfo!);
        var awaitExpression = AsyncExpression.Await(asyncExpression, configureAwait: false);

        var lambda = Expression.Lambda<Func<int>>(awaitExpression);
        var compiledLambda = lambda.Compile();

        try
        {
            _ = compiledLambda(); // Directly get the unwrapped result
            Assert.Fail("Expected exception was not thrown.");
        }
        catch (InvalidOperationException ex)
        {
            Assert.AreEqual("Simulated exception", ex.Message, "The exception message should match.");
        }
    }
}

