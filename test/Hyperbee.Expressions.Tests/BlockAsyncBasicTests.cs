﻿using System.Linq.Expressions;
using System.Reflection;
using Hyperbee.Expressions.Tests.TestSupport;
using static System.Linq.Expressions.Expression;
using static Hyperbee.Expressions.ExpressionExtensions;

namespace Hyperbee.Expressions.Tests;

[TestClass]
public class BlockAsyncBasicTests
{
    [DataTestMethod]
    [DataRow( CompleterType.Immediate, CompilerType.Fast )]
    [DataRow( CompleterType.Immediate, CompilerType.System )]
    [DataRow( CompleterType.Immediate, CompilerType.Interpret )]
    [DataRow( CompleterType.Deferred, CompilerType.Fast )]
    [DataRow( CompleterType.Deferred, CompilerType.System )]
    [DataRow( CompleterType.Deferred, CompilerType.Interpret )]
    public async Task BlockAsync_ShouldAwaitSuccessfully_WithCompletedTask( CompleterType completer, CompilerType compiler )
    {
        // Arrange
        var block = BlockAsync(
            Await( AsyncHelper.Completer(
                Constant( completer ),
                Constant( 1 )
            ) )
        );

        var lambda = Lambda<Func<Task<int>>>( block );
        var compiledLambda = lambda.Compile( compiler );

        // Act
        var result = await compiledLambda();

        //Assert
        Assert.AreEqual( 1, result );
    }

    [DataTestMethod]
    [DataRow( CompleterType.Immediate, CompilerType.Fast )]
    [DataRow( CompleterType.Immediate, CompilerType.System )]
    [DataRow( CompleterType.Immediate, CompilerType.Interpret )]
    [DataRow( CompleterType.Deferred, CompilerType.Fast )]
    [DataRow( CompleterType.Deferred, CompilerType.System )]
    [DataRow( CompleterType.Deferred, CompilerType.Interpret )]
    public async Task BlockAsync_ShouldAwaitMultipleSuccessfully_WithCompletedTask( CompleterType completer, CompilerType compiler )
    {
        // Arrange
        var block = BlockAsync(
            Await( AsyncHelper.Completer(
                Constant( completer ),
                Constant( 1 )
            ) ),
            Await( AsyncHelper.Completer(
                Constant( completer ),
                Constant( 2 )
            ) )
        );

        var lambda = Lambda<Func<Task<int>>>( block );
        var compiledLambda = lambda.Compile( compiler );

        // Act
        var result = await compiledLambda();

        //Assert
        Assert.AreEqual( 2, result );
    }

    [DataTestMethod]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Interpret )]
    public async Task BlockAsync_ShouldAwaitSuccessfully_WithDelayedTask( CompilerType compiler )
    {
        // Arrange
        var delayedTask = Task.Delay( 100 ).ContinueWith( _ => 5 );
        var block = BlockAsync( Await( Constant( delayedTask ) ) );
        var lambda = Lambda<Func<Task<int>>>( block );
        var compiledLambda = lambda.Compile( compiler );

        // Act
        var result = await compiledLambda();

        // Assert
        Assert.AreEqual( 5, result );
    }

    [DataTestMethod]
    [DataRow( CompleterType.Immediate, CompilerType.Fast )]
    [DataRow( CompleterType.Immediate, CompilerType.System )]
    [DataRow( CompleterType.Immediate, CompilerType.Interpret )]
    [DataRow( CompleterType.Deferred, CompilerType.Fast )]
    [DataRow( CompleterType.Deferred, CompilerType.System )]
    [DataRow( CompleterType.Deferred, CompilerType.Interpret )]
    public async Task BlockAsync_ShouldAwaitMultipleTasks_WithDifferentResults( CompleterType completer, CompilerType compiler )
    {
        // Arrange
        var block = BlockAsync(
            Await( AsyncHelper.Completer(
                Constant( completer ),
                Constant( 1 )
            ) ),
            Await( AsyncHelper.Completer(
                Constant( completer ),
                Constant( 2 )
            ) )
        );

        var lambda = Lambda<Func<Task<int>>>( block );
        var compiledLambda = lambda.Compile( compiler );

        // Act
        var result = await compiledLambda();

        // Assert
        Assert.AreEqual( 2, result ); // Last awaited result should be returned
    }

    [DataTestMethod]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Interpret )]
    [ExpectedException( typeof( InvalidOperationException ) )]
    public async Task BlockAsync_ShouldThrowException_WithFaultedTask( CompilerType compiler )
    {
        // Arrange
        var block = BlockAsync(
            Await( Constant( Task.FromException<int>( new InvalidOperationException( "Test Exception" ) ) ) )
        );

        var lambda = Lambda<Func<Task<int>>>( block );
        var compiledLambda = lambda.Compile( compiler );

        // Act & Assert
        await compiledLambda();
    }

    [DataTestMethod]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Interpret )]
    [ExpectedException( typeof( TaskCanceledException ) )]
    public async Task BlockAsync_ShouldHandleCanceledTask_WithCancellation( CompilerType compiler )
    {
        // Arrange
        var cancellationTokenSource = new CancellationTokenSource();
        await cancellationTokenSource.CancelAsync();

        var canceledTask = Task.FromCanceled<int>( cancellationTokenSource.Token );
        var block = BlockAsync( Await( Constant( canceledTask ) ) );
        var lambda = Lambda<Func<Task<int>>>( block );
        var compiledLambda = lambda.Compile( compiler );

        // Act & Assert
        await compiledLambda();
    }

    [DataTestMethod]
    [DataRow( CompleterType.Immediate, CompilerType.Fast )]
    [DataRow( CompleterType.Immediate, CompilerType.System )]
    [DataRow( CompleterType.Immediate, CompilerType.Interpret )]
    [DataRow( CompleterType.Deferred, CompilerType.Fast )]
    [DataRow( CompleterType.Deferred, CompilerType.System )]
    [DataRow( CompleterType.Deferred, CompilerType.Interpret )]
    public async Task BlockAsync_ShouldAwaitNestedBlockAsync_WithNestedTasks( CompleterType completer, CompilerType compiler )
    {
        // Arrange
        var innerBlock = BlockAsync(
            Await( AsyncHelper.Completer( Constant( completer ), Constant( 2 ) ) )
        );
        var block = BlockAsync(
            Await( AsyncHelper.Completer( Constant( completer ), Constant( 1 ) ) ),
            Await( innerBlock )
        );

        var lambda = Lambda<Func<Task<int>>>( block );
        var compiledLambda = lambda.Compile( compiler );

        // Act
        var result = await compiledLambda();

        // Assert
        Assert.AreEqual( 2, result );
    }

    [DataTestMethod]
    [DataRow( CompleterType.Immediate, CompilerType.Fast )]
    [DataRow( CompleterType.Immediate, CompilerType.System )]
    [DataRow( CompleterType.Immediate, CompilerType.Interpret )]
    [DataRow( CompleterType.Deferred, CompilerType.Fast )]
    [DataRow( CompleterType.Deferred, CompilerType.System )]
    [DataRow( CompleterType.Deferred, CompilerType.Interpret )]
    public async Task BlockAsync_ShouldAwaitNestedBlockAsync_WithNestedLambdas( CompleterType completer, CompilerType compiler )
    {
        // Arrange
        var innerBlock = Lambda<Func<Task<int>>>(
            BlockAsync(
                Await( AsyncHelper.Completer( Constant( completer ), Constant( 2 ) ) )
            )
        );

        var block = BlockAsync(
            Await( AsyncHelper.Completer( Constant( completer ), Constant( 1 ) ) ),
            Await( Invoke( innerBlock ) )
        );

        var lambda = Lambda<Func<Task<int>>>( block );
        var compiledLambda = lambda.Compile( compiler );

        // Act
        var result = await compiledLambda();

        // Assert
        Assert.AreEqual( 2, result );
    }

    [DataTestMethod]
    [DataRow( CompleterType.Immediate, CompilerType.Fast )]
    [DataRow( CompleterType.Immediate, CompilerType.System )]
    [DataRow( CompleterType.Immediate, CompilerType.Interpret )]
    [DataRow( CompleterType.Deferred, CompilerType.Fast )]
    [DataRow( CompleterType.Deferred, CompilerType.System )]
    [DataRow( CompleterType.Deferred, CompilerType.Interpret )]
    public async Task BlockAsync_ShouldShareVariablesBetweenBlocks_WithNestedAwaits( CompleterType completer, CompilerType compiler )
    {
        // Arrange
        var var1 = Variable( typeof( int ), "var1" );
        var var2 = Variable( typeof( int ), "var2" );

        // uses variables from both outer blocks
        var mostInnerBlock = Lambda<Func<Task>>(
            BlockAsync(
                Assign( var1, Add( var1, var2 ) ),
                Await( AsyncHelper.Completer(
                        Constant( completer )
                    ) )
            ) );

        var innerBlock = Lambda<Func<Task<int>>>(
            BlockAsync(
                [var2], // in scoped here and most inner block
                Assign( var2, Add( var1, Constant( 1 ) ) ),
                Await( Invoke( mostInnerBlock ) ),
                var1
            ) );

        var block = BlockAsync(
            [var1], // in scope for all blocks
            Assign( var1, Constant( 3 ) ),
            Await( Invoke( innerBlock ) )
        );

        var lambda = Lambda<Func<Task<int>>>( block );
        var compiledLambda = lambda.Compile( compiler );

        // Act
        var result = await compiledLambda();

        // Assert
        Assert.AreEqual( 7, result );
    }

    [DataTestMethod]
    [DataRow( CompleterType.Immediate, CompilerType.Fast )]
    [DataRow( CompleterType.Immediate, CompilerType.System )]
    [DataRow( CompleterType.Immediate, CompilerType.Interpret )]
    [DataRow( CompleterType.Deferred, CompilerType.Fast )]
    [DataRow( CompleterType.Deferred, CompilerType.System )]
    [DataRow( CompleterType.Deferred, CompilerType.Interpret )]
    public async Task BlockAsync_ShouldHandleSyncAndAsync_WithMixedOperations( CompleterType completer, CompilerType compiler )
    {
        // Arrange
        var block = BlockAsync(
            Constant( 10 ),
            Await( AsyncHelper.Completer( Constant( completer ), Constant( 20 ) ) )
        );
        var lambda = Lambda<Func<Task<int>>>( block );
        var compiledLambda = lambda.Compile( compiler );

        // Act
        var result = await compiledLambda();

        // Assert
        Assert.AreEqual( 20, result ); // The last operation is asynchronous and returns 20
    }

    [DataTestMethod]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Interpret )]
    public async Task BlockAsync_ShouldAwaitSuccessfully_WithTask( CompilerType compiler )
    {
        // Arrange
        var block = BlockAsync( Await( Constant( Task.CompletedTask, typeof( Task ) ) ) );
        var lambda = Lambda<Func<Task>>( block );
        var compiledLambda = lambda.Compile( compiler );

        // Act
        await compiledLambda(); // Should complete without exception

        // Assert
        Assert.IsTrue( true ); // If no exception, the test is successful
    }

    [DataTestMethod]
    [DataRow( CompleterType.Immediate, CompilerType.Fast )]
    [DataRow( CompleterType.Immediate, CompilerType.System )]
    [DataRow( CompleterType.Immediate, CompilerType.Interpret )]
    [DataRow( CompleterType.Deferred, CompilerType.Fast )]
    [DataRow( CompleterType.Deferred, CompilerType.System )]
    [DataRow( CompleterType.Deferred, CompilerType.Interpret )]
    public async Task BlockAsync_ShouldAwaitSuccessfully_WithReturnLabel( CompleterType completer, CompilerType compiler )
    {
        // Arrange
        var returnLabel = Label( typeof( int ) );
        var block = BlockAsync(
            IfThenElse(
                Constant( true ),
                Return( returnLabel, Await( AsyncHelper.Completer( Constant( completer ), Constant( 10 ) ) ) ),
                Return( returnLabel, Await( AsyncHelper.Completer( Constant( completer ), Constant( 20 ) ) ) )
            ),
            Label( returnLabel, Constant( 30 ) )
        );

        var lambda = Lambda<Func<Task<int>>>( block );
        var compiledLambda = lambda.Compile( compiler );

        // Act
        var result = await compiledLambda(); // Should complete without exception

        // Assert
        Assert.AreEqual( 10, result ); // The last operation is asynchronous and returns 10
    }

    [DataTestMethod]
    [DataRow( CompleterType.Immediate, CompilerType.Fast )]
    [DataRow( CompleterType.Immediate, CompilerType.System )]
    [DataRow( CompleterType.Immediate, CompilerType.Interpret )]
    [DataRow( CompleterType.Deferred, CompilerType.Fast )]
    [DataRow( CompleterType.Deferred, CompilerType.System )]
    [DataRow( CompleterType.Deferred, CompilerType.Interpret )]
    public async Task BlockAsync_ShouldPreserveVariableAssignment_BeforeAndAfterAwait( CompleterType completer, CompilerType compiler )
    {
        // Arrange
        var resultValue = Parameter( typeof( int ), "result" );

        var block = BlockAsync(
            [resultValue],
            Assign( resultValue, Constant( 5 ) ),
            Await( AsyncHelper.Completer(
                        Constant( completer )
                    ) ),
            Assign( resultValue, Add( resultValue, Constant( 10 ) ) ),
            resultValue // Return the result
        );

        var lambda = Lambda<Func<Task<int>>>( block );
        var compiledLambda = lambda.Compile( compiler );

        // Act
        var result = await compiledLambda();

        // Assert
        Assert.AreEqual( 15, result );
    }

    [DataTestMethod]
    [DataRow( CompleterType.Immediate, CompilerType.Fast )]
    [DataRow( CompleterType.Immediate, CompilerType.System )]
    [DataRow( CompleterType.Immediate, CompilerType.Interpret )]
    [DataRow( CompleterType.Deferred, CompilerType.Fast )]
    [DataRow( CompleterType.Deferred, CompilerType.System )]
    [DataRow( CompleterType.Deferred, CompilerType.Interpret )]
    public async Task BlockAsync_ShouldPreserveVariablesInNestedBlock_WithAwaits( CompleterType completer, CompilerType compiler )
    {
        // Arrange
        // NOTE: this test is also verifying the hoisting visitor and reuse of names
        var outerValue = Parameter( typeof( int ), "value" );
        var innerValue = Parameter( typeof( string ), "value" );
        var otherValue1 = Variable( typeof( int ), "value" );
        var otherValue2 = Variable( typeof( string ), "value" );
        var otherValue3 = Variable( typeof( string ), "value" );

        Expression<Func<string, int>> test = s => int.Parse( s );

        var innerBlock = Lambda<Func<string, Task<string>>>(
            BlockAsync(
                [otherValue1, otherValue2],
                Block(
                    Assign( otherValue1, Condition( Constant( false ), Constant( 100 ), Block( Constant( 150 ) ) ) ),
                    Assign( otherValue2, Constant( "200" ) ),
                    Await( AsyncHelper.Completer( Constant( completer ) ) ),
                    innerValue ),
                Condition( Constant( false ), Assign( innerValue, Constant( "200" ) ), Assign( otherValue2, Constant( "250" ) ) )
            ),
            parameters: [innerValue]
        );

        var block = BlockAsync(
            [otherValue3],
            Assign( otherValue3, Constant( "300" ) ),
            Assign( outerValue,
                Add( outerValue,
                    Invoke( test, Await( Invoke( innerBlock, otherValue3 ) ) ) ) ),
            outerValue
        );

        var lambda = Lambda<Func<int, Task<int>>>( block, outerValue );
        var compiledLambda = lambda.Compile( compiler );

        // Act
        var result = await compiledLambda( 5 );

        // Assert
        Assert.AreEqual( 255, result );
    }

    [DataTestMethod]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Interpret )]
    public async Task BlockAsync_ShouldPreserveVariables_WithNestedAwaits( CompilerType compiler )
    {
        Expression<Func<Task<int>>> initVariableAsync = () => Task.FromResult( 5 );
        Expression<Func<Task<bool>>> isTrueAsync = () => Task.FromResult( true );
        Expression<Func<int, int, Task<int>>> addAsync = ( a, b ) => Task.FromResult( a + b );

        var variable = Variable( typeof( int ), "variable" );

        var asyncBlock = BlockAsync(
            [variable],
            Assign(
                variable,
                Await( Invoke( initVariableAsync ) )
            ),
            IfThen(
                Await( Invoke( isTrueAsync ) ),
                Assign(
                    variable,
                    Await( Invoke( addAsync, variable, variable ) )
                )
            ),
            variable
        );

        var lambda = (Lambda<Func<Task<int>>>( asyncBlock ).Reduce() as Expression<Func<Task<int>>>)!;
        var compiledLambda = lambda.Compile( compiler );

        // Act
        var result = await compiledLambda();

        // Assert
        Assert.AreEqual( 10, result );
    }

    [DataTestMethod]
    [DataRow( CompleterType.Immediate, CompilerType.Fast )]
    [DataRow( CompleterType.Immediate, CompilerType.System )]
    [DataRow( CompleterType.Immediate, CompilerType.Interpret )]
    [DataRow( CompleterType.Deferred, CompilerType.Fast )]
    [DataRow( CompleterType.Deferred, CompilerType.System )]
    [DataRow( CompleterType.Deferred, CompilerType.Interpret )]
    public async Task BlockAsync_ShouldAllowLambdaParameters( CompleterType completer, CompilerType compiler )
    {
        var var1 = Variable( typeof( int ), "var1" );
        var param1 = Parameter( typeof( Func<Task<int>> ), "param1" );

        var asyncBlock = BlockAsync(
            Await(
                Invoke(
                    Lambda<Func<Func<Task<int>>, Task<int>>>(
                        BlockAsync(
                            [var1],
                            Assign( var1, Await( Invoke( param1 ) ) ),
                            var1
                        ),
                        parameters: [param1]
                    ),
                    Lambda<Func<Task<int>>>(
                        BlockAsync(
                            Await( AsyncHelper.Completer(
                                Constant( completer ),
                                Constant( 15 ) )
                            )
                        )
                    )
                )
            )
        );

        var lambda = Lambda<Func<Task<int>>>( asyncBlock );

        var compiledLambda = lambda.Compile( compiler );

        // Act
        var result = await compiledLambda();

        // Assert
        Assert.AreEqual( 15, result );
    }

    [DataTestMethod]
    [DataRow( CompleterType.Immediate, CompilerType.Fast )]
    [DataRow( CompleterType.Immediate, CompilerType.System )]
    [DataRow( CompleterType.Immediate, CompilerType.Interpret )]
    [DataRow( CompleterType.Deferred, CompilerType.Fast )]
    [DataRow( CompleterType.Deferred, CompilerType.System )]
    [DataRow( CompleterType.Deferred, CompilerType.Interpret )]
    public async Task BlockAsync_ShouldAllowParallelBlocks_WithTaskWhenAll( CompleterType completer, CompilerType compiler )
    {
        // Arrange
        var tracker = Variable( typeof( int[] ), "tracker" );
        var temp = Variable( typeof( int ), "temp" );

        var tasks = Variable( typeof( List<Task> ), "tasks" );
        var i = Variable( typeof( int ), "i" );

        var taskRun = Call(
            typeof( Task ).GetMethod( nameof( Task.Run ), [typeof( Func<Task> )] )!,
            Lambda<Func<Task>>(
                BlockAsync(
                    Await( AsyncHelper.Completer( Constant( completer ) ) ),
                    Assign( ArrayAccess( tracker, temp ), temp )
                )
            )
        );

        const int threadCount = 5;

        var initIncrement = Assign( i, Constant( 0 ) );
        var condition = LessThan( i, Constant( threadCount ) );
        var iteration = PostIncrementAssign( i );

        var block = BlockAsync(
            [tracker, tasks],
            Assign( tracker, NewArrayBounds( typeof( int ), Constant( threadCount ) ) ),
            Assign( tasks, New( typeof( List<Task> ).GetConstructors()[0] ) ),
            For( [i], initIncrement, condition, iteration,
                Block(
                    [temp],
                    Assign( temp, i ),
                    Call( tasks, typeof( List<Task> ).GetMethod( nameof( List<Task>.Add ) )!, taskRun )
                )
            ),
            Await( Call( typeof( Task ).GetMethod( nameof( Task.WhenAll ), [typeof( IEnumerable<Task> )] )!, tasks ) ),
            tracker
        );

        var lambda = Lambda<Func<Task<int[]>>>( block );
        var compiledLambda = lambda.Compile( compiler );

        // Act
        var result = await compiledLambda();

        // Assert
        Assert.AreEqual( threadCount, result.Length );
        for ( var tC = 0; tC < threadCount; tC++ )
        {
            Assert.AreEqual( tC, result[tC] );
        }
    }

    [DataTestMethod]
    [DataRow( CompleterType.Immediate, CompilerType.Fast )]
    [DataRow( CompleterType.Immediate, CompilerType.System )]
    [DataRow( CompleterType.Immediate, CompilerType.Interpret )]
    [DataRow( CompleterType.Deferred, CompilerType.Fast )]
    [DataRow( CompleterType.Deferred, CompilerType.System )]
    [DataRow( CompleterType.Deferred, CompilerType.Interpret )]
    public async Task BlockAsync_ShouldAllowNestedBlocks_WithoutAsync( CompleterType completer, CompilerType compiler )
    {
        // Arrange
        var innerVar = Variable( typeof( int ), "innerVar" );
        var middleVar = Variable( typeof( int ), "middleVar" );
        var outerVar = Variable( typeof( int ), "outerVar" );

        var blockAsync = BlockAsync(
            [outerVar],
            Block(
                [middleVar],
                Await(
                    BlockAsync(
                        [innerVar],
                        Assign( outerVar, Await( AsyncHelper.Completer(
                            Constant( completer ),
                            Constant( 3 )
                        ) ) ),
                        Assign( innerVar, Constant( 1 ) ),
                        Assign( middleVar, Constant( 2 ) )
                    )
                ),
                Assign( middleVar, Await( AsyncHelper.Completer(
                        Constant( completer ),
                        Constant( 4 )
                    ) ) ),
                Assign( outerVar, Add( outerVar, middleVar ) )
            ),
            Await(
                AsyncHelper.Completer( Constant( completer ) )
            ),
            outerVar
        );

        // Act
        var lambda = Lambda<Func<Task<int>>>( blockAsync );

        var compiledLambda = lambda.Compile( compiler );

        var result = await compiledLambda();

        // Assert
        Assert.AreEqual( 7, result );
    }

    [DataTestMethod]
    [DataRow( CompleterType.Immediate, CompilerType.Fast )]
    [DataRow( CompleterType.Immediate, CompilerType.System )]
    [DataRow( CompleterType.Immediate, CompilerType.Interpret )]
    [DataRow( CompleterType.Deferred, CompilerType.Fast )]
    [DataRow( CompleterType.Deferred, CompilerType.System )]
    [DataRow( CompleterType.Deferred, CompilerType.Interpret )]
    public async Task BlockAsync_ShouldAwaitSuccessfully_WithBlockConditional( CompleterType completer, CompilerType compiler )
    {
        // Arrange
        var block = BlockAsync(
            Block(
                Constant( 5 ),
                Condition( Constant( true ),
                    Block(
                        Await( AsyncHelper.Completer(
                            Constant( completer ),
                            Constant( 1 )
                        ) ),
                        Constant( 2 )
                    ),
                    Constant( 0 )
                )
            )
        );
        var lambda = Lambda<Func<Task<int>>>( block );
        var compiledLambda = lambda.Compile( compiler );

        // Act
        var result = await compiledLambda();

        // Assert
        Assert.AreEqual( 2, result );
    }

    public class TestContext
    {
        public int Id { get; set; }
    }

    private sealed class Disposable( Action dispose ) : IDisposable
    {
        public static readonly ConstructorInfo ConstructorInfo = typeof( Disposable ).GetConstructors()[0];

        private int _disposed;
        private Action Disposer { get; } = dispose;

        public void Dispose()
        {
            if ( Interlocked.CompareExchange( ref _disposed, 1, 0 ) == 0 )
                Disposer.Invoke();
        }
    }

    [DataTestMethod]
    [DataRow( CompleterType.Immediate, CompilerType.Fast )]
    [DataRow( CompleterType.Immediate, CompilerType.System )]
    [DataRow( CompleterType.Deferred, CompilerType.Fast )]
    [DataRow( CompleterType.Deferred, CompilerType.System )]
    public async Task BlockAsync_ShouldAwaitSuccessfully_WithNestedLambdaArgument( CompleterType completer, CompilerType compiler )
    {
        // Arrange
        var control = Constant( new TestContext() );
        var idVariable = Variable( typeof( int ), "originalId" );
        var idProperty = Property( control, "Id" );
        var exception = Variable( typeof( Exception ), "exception" );

        var disposableBlock = Block(
            [idVariable],
            Assign( idVariable, idProperty ),
            TryCatch(
                Block(
                    Assign( idProperty, Constant( 10 ) ),
                    New( Disposable.ConstructorInfo,
                        Lambda<Action>(
                            Block(
                                Assign( idProperty, idVariable )
                            )
                        ) )
                ),
                Catch(
                    exception,
                    Block(
                        [exception],
                        Assign( idProperty, idVariable ),
                        Throw( exception, typeof( Disposable ) )
                    )
                )
            )
        );

        // Act
        var lambda = Lambda<Func<Task<int>>>( BlockAsync(
             Using( disposableBlock,
                 Await( AsyncHelper.Completer(
                        Constant( completer ),
                        Constant( 3 )
                    ) ) )
             ) );

        var compiledLambda = lambda.Compile( compiler );

        var result = await compiledLambda();

        // Assert
        Assert.AreEqual( 3, result );
    }
}
