using System.Linq.Expressions;
using System.Runtime.CompilerServices;

namespace Hyperbee.Expressions.Tests;

public readonly struct LazyAwaiter<T> : ICriticalNotifyCompletion //INotifyCompletion
{
    private readonly Lazy<T> _lazy;

    public LazyAwaiter( Lazy<T> lazy ) => _lazy = lazy;

    public T GetResult() => _lazy.Value;
    public bool IsCompleted => true;
    public void OnCompleted( Action continuation ) { }
    public void UnsafeOnCompleted( Action continuation ) { }
}

public static class LazyAwaiterExtensions
{
    public static LazyAwaiter<T> GetAwaiter<T>( this Lazy<T> lazy )
    {
        return new LazyAwaiter<T>( lazy );
    }
}

[TestClass]
public class CustomAwaiterTests
{
    [TestMethod]
    public void TestCustomAwaiter_Await()
    {
        // var lazy = new Lazy<int>( () => 42 );
        // var result = await lazy;

        Expression<Func<int>> valueExpression = () => 42;
        var lazyConstructor = typeof( Lazy<int> ).GetConstructor( [typeof( Func<int> )] );
        var lazyExpression = Expression.New( lazyConstructor!, valueExpression );

        var awaitExpression = ExpressionExtensions.Await( lazyExpression, configureAwait: false );

        var lambda = Expression.Lambda<Func<int>>( awaitExpression );
        var compiledLambda = lambda.Compile();

        var result = compiledLambda();

        Assert.AreEqual( 42, result, "The result should be 42." );
    }

    [TestMethod]
    public async Task TestCustomAwaiter_AsyncBlock()
    {
        // var lazy = new Lazy<int>( () => 42 );
        // var result = await lazy;

        Expression<Func<int>> valueExpression = () => 42;
        var lazyConstructor = typeof(Lazy<int>).GetConstructor( [typeof(Func<int>)] );
        var lazyExpression = Expression.New( lazyConstructor!, valueExpression );

        var block = ExpressionExtensions.BlockAsync(
            ExpressionExtensions.Await( lazyExpression, configureAwait: false )
        );

        var lambda = Expression.Lambda<Func<Task<int>>>( block );
        var compiledLambda = lambda.Compile();

        var result = await compiledLambda();

        Assert.AreEqual( 42, result, "The result should be 42." );
    }
}
