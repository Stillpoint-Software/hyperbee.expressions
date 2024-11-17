using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using Hyperbee.Expressions.Tests.TestSupport;
using static System.Linq.Expressions.Expression;
using static Hyperbee.Expressions.ExpressionExtensions;

namespace Hyperbee.Expressions.Tests;

public class LazyAwaiter<T> : ICriticalNotifyCompletion
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
        var lazyExpression = New( lazyConstructor!, valueExpression );

        var awaitExpression = Await( lazyExpression, configureAwait: false );

        var lambda = Lambda<Func<int>>( awaitExpression );
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
        var lazyConstructor = typeof( Lazy<int> ).GetConstructor( [typeof( Func<int> )] );
        var lazyExpression = New( lazyConstructor!, valueExpression );

        var block = BlockAsync(
            Await( lazyExpression, configureAwait: false )
        );

        var lambda = Lambda<Func<Task<int>>>( block );
        var compiledLambda = lambda.Compile();

        var result = await compiledLambda();

        Assert.AreEqual( 42, result, "The result should be 42." );
    }

    [DataTestMethod]
    [DataRow( true )] // Immediate completion
    [DataRow( false )] // Deferred completion
    public async Task TestCustomAwaiter_TaskLike( bool immediately ) //BF for ME review
    {
        // Arrange
        var resultValue = Parameter( typeof(int), "result" );

        var block = BlockAsync(
            [resultValue],
            Assign( resultValue, Constant( 5 ) ),
            Await(
                AsyncHelper.Completable(
                    Constant( immediately )
                )
            ),
            Assign( resultValue, Add( resultValue, Constant( 10 ) ) ),
            resultValue // Return the result
        );

        var lambda = Lambda<Func<Task<int>>>( block );
        var compiledLambda = lambda.Compile();

        // Act
        var result = await compiledLambda();

        // Assert
        Assert.AreEqual( 15, result ); // 5 + 10
    }

    [DataTestMethod]
    [DataRow( true )] // Immediate completion
    [DataRow( false )] // Deferred completion
    public async Task TestCustomAwaiter_TaskResultLike( bool immediately ) //BF for ME review
    {
        // Arrange
        var resultValue = Parameter( typeof(int), "result" );

        var block = BlockAsync(
            [resultValue],
            Assign( resultValue,
                Add(
                    Await(
                        AsyncHelper.Completable(
                            Constant( immediately ),
                            Constant( 37 )
                        )
                    ),
                    Constant( 5 )
                )
            ),
            resultValue
        );

        var lambda = Lambda<Func<Task<int>>>( block );
        var compiledLambda = lambda.Compile();

        // Act
        var result = await compiledLambda();

        // Assert
        Assert.AreEqual( 42, result );
    }
}
