using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using Hyperbee.Expressions.Tests.TestSupport;
using static System.Linq.Expressions.Expression;
using static Hyperbee.Expressions.ExpressionExtensions;

namespace Hyperbee.Expressions.Tests;

public class LazyAwaiter<T>( Lazy<T> lazy ) : ICriticalNotifyCompletion
{
    public T GetResult() => lazy.Value;
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

    [DataTestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    public async Task TestCustomAwaiter_AsyncBlock( CompilerType compiler )
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
        var compiledLambda = lambda.Compile( compiler );

        var result = await compiledLambda();

        Assert.AreEqual( 42, result, "The result should be 42." );
    }

    [DataTestMethod]
    [DataRow( CompleterType.Immediate, CompilerType.Fast )]
    [DataRow( CompleterType.Immediate, CompilerType.System )]
    [DataRow( CompleterType.Deferred, CompilerType.Fast )]
    [DataRow( CompleterType.Deferred, CompilerType.System )]
    public async Task TestCustomAwaiter_TaskLike( CompleterType completer, CompilerType compiler )
    {
        // Arrange
        var resultValue = Parameter( typeof( int ), "result" );

        var block = BlockAsync(
            [resultValue],
            Assign( resultValue, Constant( 5 ) ),
            Await(
                AsyncHelper.Completer(
                    Constant( completer )
                )
            ),
            Assign( resultValue, Add( resultValue, Constant( 37 ) ) ),
            resultValue
        );

        var lambda = Lambda<Func<Task<int>>>( block );
        var compiledLambda = lambda.Compile( compiler );

        // Act
        var result = await compiledLambda();

        // Assert
        Assert.AreEqual( 42, result, "The result should be 42." );
    }

    [DataTestMethod]
    [DataRow( CompleterType.Immediate, CompilerType.Fast )]
    [DataRow( CompleterType.Immediate, CompilerType.System )]
    [DataRow( CompleterType.Deferred, CompilerType.Fast )]
    [DataRow( CompleterType.Deferred, CompilerType.System )]
    public async Task TestCustomAwaiter_TaskResultLike( CompleterType completer, CompilerType compiler )
    {
        // Arrange
        var resultValue = Parameter( typeof( int ), "result" );

        var block = BlockAsync(
            [resultValue],
            Assign( resultValue,
                Add(
                    Await(
                        AsyncHelper.Completer(
                            Constant( completer ),
                            Constant( 37 )
                        )
                    ),
                    Constant( 5 )
                )
            ),
            resultValue
        );

        var lambda = Lambda<Func<Task<int>>>( block );
        var compiledLambda = lambda.Compile( compiler );

        // Act
        var result = await compiledLambda();

        // Assert
        Assert.AreEqual( 42, result, "The result should be 42." );
    }
}
