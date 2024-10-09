using System.Linq.Expressions;
using System.Runtime.CompilerServices;

namespace Hyperbee.AsyncExpressions.Tests;

internal readonly struct LazyAwaiter<T> : INotifyCompletion
{
    private readonly Lazy<T> _lazy;

    public LazyAwaiter( Lazy<T> lazy ) => _lazy = lazy;

    public T GetResult() => _lazy.Value;

    public bool IsCompleted => true;

    public void OnCompleted( Action continuation ) { }
}

internal static class LazyAwaiterExtensions
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
        var lazyConstructor = typeof(Lazy<int>).GetConstructor( [typeof(Func<int>)] );
        var lazyExpression = Expression.New( lazyConstructor!, valueExpression );

        var awaitExpression = AsyncExpression.Await( lazyExpression, configureAwait: false );

        var lambda = Expression.Lambda<Func<int>>( awaitExpression );
        var compiledLambda = lambda.Compile();

        var result = compiledLambda(); 

        Assert.AreEqual( 42, result, "The result should be 42." );
    }
}
