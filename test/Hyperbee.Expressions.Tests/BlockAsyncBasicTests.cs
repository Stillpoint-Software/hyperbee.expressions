using System.Linq.Expressions;
using System.Reflection;
using Hyperbee.Expressions.Tests.TestSupport;
using static System.Linq.Expressions.Expression;
using static Hyperbee.Expressions.ExpressionExtensions;

namespace Hyperbee.Expressions.Tests;

[TestClass]
public class BlockAsyncBasicTests
{
    [TestMethod]
    public async Task BlockAsync_ShouldAwaitSuccessfully_WithCompletedTask()
    {
        // Arrange
        var block = BlockAsync( Await( Constant( Task.FromResult( 1 ) ) ) );
        var lambda = Lambda<Func<Task<int>>>( block );
        var compiledLambda = lambda.Compile();

        // Act
        var result = await compiledLambda();

        //Assert
        Assert.AreEqual( 1, result );
    }

    [TestMethod]
    public async Task BlockAsync_ShouldAwaitSuccessfully_WithDelayedTask()
    {
        // Arrange
        var delayedTask = Task.Delay( 100 ).ContinueWith( _ => 5 );
        var block = BlockAsync( Await( Constant( delayedTask ) ) );
        var lambda = Lambda<Func<Task<int>>>( block );
        var compiledLambda = lambda.Compile();

        // Act
        var result = await compiledLambda();

        // Assert
        Assert.AreEqual( 5, result );
    }

    [TestMethod]
    public async Task BlockAsync_ShouldAwaitMultipleTasks_WithDifferentResults()
    {
        // Arrange
        var block = BlockAsync(
            Await( Constant( Task.FromResult( 1 ) ) ),
            Await( Constant( Task.FromResult( 2 ) ) )
        );
        var lambda = Lambda<Func<Task<int>>>( block );
        var compiledLambda = lambda.Compile();

        // Act
        var result = await compiledLambda();

        // Assert
        Assert.AreEqual( 2, result ); // Last awaited result should be returned
    }

    [TestMethod]
    [ExpectedException( typeof( InvalidOperationException ) )]
    public async Task BlockAsync_ShouldThrowException_WithFaultedTask()
    {
        // Arrange
        var block = BlockAsync(
            Await( Constant( Task.FromException<int>( new InvalidOperationException( "Test Exception" ) ) ) )
        );
        var lambda = Lambda<Func<Task<int>>>( block );
        var compiledLambda = lambda.Compile();

        // Act & Assert
        await compiledLambda();
    }

    [TestMethod]
    [ExpectedException( typeof( TaskCanceledException ) )]
    public async Task BlockAsync_ShouldHandleCanceledTask_WithCancellation()
    {
        // Arrange
        var cancellationTokenSource = new CancellationTokenSource();
        await cancellationTokenSource.CancelAsync();

        var canceledTask = Task.FromCanceled<int>( cancellationTokenSource.Token );
        var block = BlockAsync( Await( Constant( canceledTask ) ) );
        var lambda = Lambda<Func<Task<int>>>( block );
        var compiledLambda = lambda.Compile();

        // Act & Assert
        await compiledLambda();
    }

    [TestMethod]
    public async Task BlockAsync_ShouldAwaitNestedBlockAsync_WithNestedTasks()
    {
        // Arrange
        var innerBlock = BlockAsync(
            Await( Constant( Task.FromResult( 2 ) ) )
        );
        var block = BlockAsync(
            Await( Constant( Task.FromResult( 1 ) ) ),
            Await( innerBlock )
        );
        var lambda = Lambda<Func<Task<int>>>( block );
        var compiledLambda = lambda.Compile();

        // Act
        var result = await compiledLambda();

        // Assert
        Assert.AreEqual( 2, result );
    }

    [TestMethod]
    public async Task BlockAsync_ShouldAwaitNestedBlockAsync_WithNestedLambdas()
    {
        // Arrange
        var innerBlock = Lambda<Func<Task<int>>>(
            BlockAsync(
                Await( Constant( Task.FromResult( 2 ) ) )
            )
        );

        var block = BlockAsync(
            Await( Constant( Task.FromResult( 1 ) ) ),
            Await( Invoke( innerBlock ) )
        );

        var lambda = Lambda<Func<Task<int>>>( block );
        var compiledLambda = lambda.Compile();

        // Act
        var result = await compiledLambda();

        // Assert
        Assert.AreEqual( 2, result );
    }

    [TestMethod]
    public async Task BlockAsync_ShouldShareVariablesBetweenBlocks_WithNestedAwaits()
    {
        // Arrange
        var var1 = Variable( typeof( int ), "var1" );
        var var2 = Variable( typeof( int ), "var2" );

        // uses variables from both outer blocks
        var mostInnerBlock = Lambda<Func<Task>>(
            BlockAsync(
                Assign( var1, Add( var1, var2 ) ),
                Await( Constant( Task.Delay( 20 ) ) )
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
        var compiledLambda = lambda.Compile();

        // Act
        var result = await compiledLambda();

        // Assert
        Assert.AreEqual( 7, result );
    }

    [TestMethod]
    public async Task BlockAsync_ShouldHandleSyncAndAsync_WithMixedOperations()
    {
        // Arrange
        var block = BlockAsync(
            Constant( 10 ),
            Await( Constant( Task.FromResult( 20 ) ) )
        );
        var lambda = Lambda<Func<Task<int>>>( block );
        var compiledLambda = lambda.Compile();

        // Act
        var result = await compiledLambda();

        // Assert
        Assert.AreEqual( 20, result ); // The last operation is asynchronous and returns 20
    }

    [TestMethod]
    public async Task BlockAsync_ShouldAwaitSuccessfully_WithTask()
    {
        // Arrange
        var block = BlockAsync( Await( Constant( Task.CompletedTask, typeof( Task ) ) ) );
        var lambda = Lambda<Func<Task>>( block );
        var compiledLambda = lambda.Compile();

        // Act
        await compiledLambda(); // Should complete without exception

        // Assert
        Assert.IsTrue( true ); // If no exception, the test is successful
    }

    [TestMethod]
    public async Task BlockAsync_ShouldPreserveVariableAssignment_BeforeAndAfterAwait()
    {
        // Arrange
        var resultValue = Parameter( typeof( int ), "result" );

        var block = BlockAsync(
            [resultValue],
            Assign( resultValue, Constant( 5 ) ),
            Await( Constant( Task.Delay( 100 ) ) ),
            Assign( resultValue, Add( resultValue, Constant( 10 ) ) ),
            resultValue // Return the result
        );

        var lambda = Lambda<Func<Task<int>>>( block );
        var compiledLambda = lambda.Compile();

        // Act
        var result = await compiledLambda();

        // Assert
        Assert.AreEqual( 15, result );
    }

    [DataTestMethod]
    [DataRow( true )] // Immediate completion
    [DataRow( false )] // Deferred completion
    public async Task BlockAsync_ShouldPreserveVariableAssignment_BeforeAndAfterAwaitX( bool completeImmediately ) //BF for ME review
    {
        // Arrange
        var resultValue = Parameter( typeof(int), "result" );

        var block = BlockAsync(
            [resultValue],
            Assign( resultValue, Constant( 5 ) ),
            Await(
                AsyncHelper.Completable(
                    Constant( completeImmediately )
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
    public async Task BlockAsync_ShouldPreserveVariableAssignment_BeforeAndAfterAwaitR( bool completeImmediately ) //BF for ME review
    {
        // Arrange
        var resultValue = Parameter( typeof(int), "result" );

        var block = BlockAsync(
            [resultValue],
            Assign( resultValue, 
                Add( 
                    Await(
                        AsyncHelper.Completable(
                            Constant( completeImmediately ),
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

    [TestMethod]
    public async Task BlockAsync_ShouldPreserveVariablesInNestedBlock_WithAwaits()
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
                    Await( Constant( Task.Delay( 100 ) ) ),
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
        var compiledLambda = lambda.Compile();

        // Act
        var result = await compiledLambda( 5 );

        // Assert
        Assert.AreEqual( 255, result );
    }

    [TestMethod]
    public async Task BlockAsync_ShouldPreserveVariables_WithNestedAwaits()
    {
        Expression<Func<Task<int>>> initVariableAsync = () => Task.FromResult( 5 );
        Expression<Func<Task<bool>>> isTrueAsync = () => Task.FromResult( true );
        Expression<Func<int, int, Task<int>>> addAsync = ( a, b ) => Task.FromResult( a + b );

        var variable = Variable( typeof( int ), "variable" );

        var asyncBlock =
            BlockAsync(
                [variable],
                Assign( variable, Await( Invoke( initVariableAsync ) ) ),
                IfThen( Await( Invoke( isTrueAsync ) ),
                    Assign( variable,
                        Await( Invoke( addAsync, variable, variable ) ) ) ),
                variable );

        var lambda = (Lambda<Func<Task<int>>>( asyncBlock ).Reduce() as Expression<Func<Task<int>>>)!;
        var compiledLambda = lambda.Compile();

        // Act
        var result = await compiledLambda();

        // Assert
        Assert.AreEqual( 10, result );
    }

    [TestMethod]
    public async Task BlockAsync_ShouldAllowLambdaParameters()
    {
        var var1 = Variable( typeof( int ), "var1" );
        var param1 = Parameter( typeof( Func<Task<int>> ), "param1" );

        var parameterAsync = Lambda<Func<Task<int>>>(
            BlockAsync(
                Await( Constant( Task.FromResult( 15 ) ) )
            )
        );

        var innerLambda = Lambda<Func<Func<Task<int>>, Task<int>>>(
            BlockAsync(
                [var1],
                Assign( var1, Await( Invoke( param1 ) ) ),
                var1
            ),
            parameters: [param1]
        );

        var asyncBlock = BlockAsync(
            Await( Invoke( innerLambda, parameterAsync ) )
        );

        var lambda = Lambda<Func<Task<int>>>( asyncBlock );

        var compiledLambda = lambda.Compile();

        // Act
        var result = await compiledLambda();

        // Assert
        Assert.AreEqual( 15, result );
    }

    [TestMethod]
    public async Task BlockAsync_ShouldAllowNestedBlocks_WithoutAsync()
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
                        Assign( outerVar, Await( Constant( Task.FromResult( 3 ) ) ) ),
                        Assign( innerVar, Constant( 1 ) ),
                        Assign( middleVar, Constant( 2 ) )
                    )
                ),
                Assign( middleVar, Await( Constant( Task.FromResult( 4 ) ) ) )
            ),
            Await( Constant( Task.FromResult( 6 ) ) ),
            Add( outerVar, middleVar )
        );

        // Act
        var lambda = Lambda<Func<Task<int>>>( blockAsync );
        var compiledLambda = lambda.Compile();

        var result = await compiledLambda();

        // Assert
        Assert.AreEqual( 7, result );
    }

    [TestMethod]
    public async Task BlockAsync_ShouldAwaitSuccessfully_WithBlockConditional()
    {
        // Arrange
        var block = BlockAsync(
            Block(
                Constant( 5 ),
                Condition( Constant( true ),
                    Await( Constant( Task.FromResult( 1 ) ) ),
                    Constant( 0 )
                )
            )
        );
        var lambda = Lambda<Func<Task<int>>>( block );
        var compiledLambda = lambda.Compile();

        // Act
        var result = await compiledLambda();

        // Assert
        Assert.AreEqual( 1, result );
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

    [TestMethod]
    public async Task BlockAsync_ShouldAwaitSuccessfully_WithNestedLambdaArgument()
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
                 Await( Constant( Task.FromResult( 3 ) ) )
             ) ) );
        var compiledLambda = lambda.Compile();

        var result = await compiledLambda();

        // Assert
        Assert.AreEqual( 3, result );
    }
}
