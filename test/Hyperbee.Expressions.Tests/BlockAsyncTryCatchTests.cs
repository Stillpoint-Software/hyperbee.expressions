using Hyperbee.Expressions.Tests.TestSupport;
using static System.Linq.Expressions.Expression;
using static Hyperbee.Expressions.ExpressionExtensions;

namespace Hyperbee.Expressions.Tests;

[TestClass]
public class BlockAsyncTryCatchTests
{
    [DataTestMethod]
    [DataRow( CompleterType.Immediate, CompilerType.Fast )]
    [DataRow( CompleterType.Immediate, CompilerType.System )]
    [DataRow( CompleterType.Immediate, CompilerType.Interpret )]
    [DataRow( CompleterType.Deferred, CompilerType.Fast )]
    [DataRow( CompleterType.Deferred, CompilerType.System )]
    [DataRow( CompleterType.Deferred, CompilerType.Interpret )]
    public async Task AsyncBlock_ShouldAwaitSuccessfully_WithAwaitInTryBlock( CompleterType completer, CompilerType compiler )
    {
        // Arrange: Await in the try block
        var exceptionParam = Parameter( typeof( Exception ), "ex" );
        var block = BlockAsync(
            TryCatch(
                Await( AsyncHelper.Completer(
                    Constant( completer ),
                    Constant( 10 )
                ) ),
                Catch( exceptionParam, Constant( 0 ) )
            )
        );
        var lambda = Lambda<Func<Task<int>>>( block );
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
    public async Task AsyncBlock_ShouldCatchExceptionSuccessfully_WithAwaitInCatchBlock( CompleterType completer, CompilerType compiler )
    {
        // Arrange: Await in the catch block
        var exceptionParam = Parameter( typeof( Exception ), "ex" );
        var resultValue = Parameter( typeof( int ) );
        var block = BlockAsync(
            [resultValue],
            TryCatch(
                Block(
                    Throw( Constant( new Exception() ) ),
                    Constant( 1 )
                ),
                Catch(
                    exceptionParam,
                    Assign( resultValue, Await( AsyncHelper.Completer(
                        Constant( completer ),
                        Constant( 99 )
                    ) ) )
                )
            ),
            resultValue
        );
        var lambda = Lambda<Func<Task<int>>>( block );
        var compiledLambda = lambda.Compile( compiler );

        // Act
        var result = await compiledLambda();

        // Assert
        Assert.AreEqual( 99, result );

    }

    [DataTestMethod]
    [DataRow( CompleterType.Immediate, CompilerType.Fast )]
    [DataRow( CompleterType.Immediate, CompilerType.System )]
    [DataRow( CompleterType.Immediate, CompilerType.Interpret )]
    [DataRow( CompleterType.Deferred, CompilerType.Fast )]
    [DataRow( CompleterType.Deferred, CompilerType.System )]
    [DataRow( CompleterType.Deferred, CompilerType.Interpret )]
    public async Task AsyncBlock_ShouldHandleExceptionSuccessfully_WithTryCatchFinally( CompleterType completer, CompilerType compiler )
    {
        // Arrange: Await in both catch and finally blocks
        var exceptionParam = Parameter( typeof( Exception ), "ex" );
        var resultValue = Parameter( typeof( int ) );
        var block = BlockAsync(
            [resultValue],
            TryCatchFinally(
                Block(
                    Throw( Constant( new Exception() ) ),
                    Constant( 1 )
                ),
                Assign( resultValue,
                    Await( AsyncHelper.Completer(
                        Constant( completer ),
                        Constant( 50 )
                    ) ) ), // This Await will still be executed after the exception
                Catch( exceptionParam,
                    Assign( resultValue, Await( AsyncHelper.Completer(
                        Constant( completer ),
                        Constant( 30 )
                    ) ) )
                )
            ),
            resultValue
        );
        var lambda = Lambda<Func<Task<int>>>( block );
        var compiledLambda = lambda.Compile( compiler );

        // Act
        var result = await compiledLambda();

        // Assert
        Assert.AreEqual( 50, result ); // Catch block handles the exception and returns 30
    }

    [DataTestMethod]
    [DataRow( CompleterType.Immediate, CompilerType.Fast )]
    [DataRow( CompleterType.Immediate, CompilerType.System )]
    [DataRow( CompleterType.Immediate, CompilerType.Interpret )]
    [DataRow( CompleterType.Deferred, CompilerType.Fast )]
    [DataRow( CompleterType.Deferred, CompilerType.System )]
    [DataRow( CompleterType.Deferred, CompilerType.Interpret )]
    public async Task AsyncBlock_ShouldAwaitSuccessfully_WithAwaitInTryAndFinallyBlocks( CompleterType completer, CompilerType compiler )
    {
        // Arrange: Await in both try and finally blocks
        var resultValue = Parameter( typeof( int ) );
        var block = BlockAsync(
            [resultValue],
            TryFinally(
                Assign( resultValue, Await( AsyncHelper.Completer( Constant( completer ), Constant( 15 ) ) ) ), // Try block
                Assign( resultValue, Constant( 25 ) ) // Finally block
            ),
            resultValue
        );
        var lambda = Lambda<Func<Task<int>>>( block );
        var compiledLambda = lambda.Compile( compiler );

        // Act
        var result = await compiledLambda();

        // Assert
        Assert.AreEqual( 25, result ); // Catch block handles the exception and returns 30
    }

    [DataTestMethod]
    [DataRow( CompleterType.Immediate, CompilerType.Fast )]
    [DataRow( CompleterType.Immediate, CompilerType.System )]
    [DataRow( CompleterType.Immediate, CompilerType.Interpret )]
    [DataRow( CompleterType.Deferred, CompilerType.Fast )]
    [DataRow( CompleterType.Deferred, CompilerType.System )]
    [DataRow( CompleterType.Deferred, CompilerType.Interpret )]
    public async Task AsyncBlock_ShouldAwaitSuccessfully_WithAwaitInTryCatchAndFinallyBlocks( CompleterType completer, CompilerType compiler )
    {
        // Arrange: Await in Try, Catch, and Finally blocks
        var resultValue = Parameter( typeof( int ) );
        var exceptionParam = Parameter( typeof( Exception ), "ex" );
        var block = BlockAsync(
            [resultValue],
            TryCatchFinally(
                Assign( resultValue, Await( AsyncHelper.Completer( Constant( completer ), Constant( 10 ) ) ) ), // Try block
                Assign( resultValue, Await( AsyncHelper.Completer( Constant( completer ), Constant( 30 ) ) ) ), // Finally block
                Catch( exceptionParam,
                    Assign( resultValue, Await( AsyncHelper.Completer( Constant( completer ), Constant( 20 ) ) ) ) // Catch block
                )
            ),
            resultValue
        );
        var lambda = Lambda<Func<Task<int>>>( block );
        var compiledLambda = lambda.Compile( compiler );

        // Act
        var result = await compiledLambda();

        // Assert
        Assert.AreEqual( 30, result ); // Finally block should execute and return 30
    }


    [DataTestMethod]
    [DataRow( CompleterType.Immediate, CompilerType.Fast )]
    [DataRow( CompleterType.Immediate, CompilerType.System )]
    [DataRow( CompleterType.Immediate, CompilerType.Interpret )]
    [DataRow( CompleterType.Deferred, CompilerType.Fast )]
    [DataRow( CompleterType.Deferred, CompilerType.System )]
    [DataRow( CompleterType.Deferred, CompilerType.Interpret )]
    public async Task AsyncBlock_ShouldAwaitSuccessfully_WithAwaitAfterThrow( CompleterType completer, CompilerType compiler )
    {
        var resultValue = Parameter( typeof( int ) );
        var exceptionParam = Parameter( typeof( Exception ), "ex" );

        var block = BlockAsync(
            [resultValue],
            TryCatch(
                Block(
                    Assign( resultValue, Constant( 10 ) ),
                    Throw( Constant( new Exception( "Exception" ) ) ),

                    // pointless code
                    Await( AsyncHelper.Completer( Constant( completer ), Constant( 20 ) ) )
                ),
                Catch( exceptionParam, Assign( resultValue, Constant( 50 ) ) )
            ),
            resultValue
        );
        var lambda = Lambda<Func<Task<int>>>( block );
        var compiledLambda = lambda.Compile( compiler );

        // Act
        var result = await compiledLambda();

        // Assert
        Assert.AreEqual( 50, result ); // Outer catch handles the exception
    }

    [DataTestMethod]
    [DataRow( CompleterType.Immediate, CompilerType.Fast )]
    [DataRow( CompleterType.Immediate, CompilerType.System )]
    [DataRow( CompleterType.Immediate, CompilerType.Interpret )]
    [DataRow( CompleterType.Deferred, CompilerType.Fast )]
    [DataRow( CompleterType.Deferred, CompilerType.System )]
    [DataRow( CompleterType.Deferred, CompilerType.Interpret )]
    public async Task AsyncBlock_ShouldCatchMultipleExceptionsInNestedTryBlocks( CompleterType completer, CompilerType compiler )
    {
        // Arrange: Multiple exceptions in nested Try-Catch blocks
        var resultValue = Parameter( typeof( int ) );
        var outerExceptionParam = Parameter( typeof( Exception ), "outerEx" );
        var innerExceptionParam = Parameter( typeof( Exception ), "innerEx" );

        var block = BlockAsync(
            [resultValue],
            TryCatch(
                Block(
                    TryCatch(
                        Block(
                            Throw( Constant( new Exception( "Inner Exception" ) ) ),
                            Constant( 0 )
                        ),
                        Catch( innerExceptionParam, Assign( resultValue, Await( AsyncHelper.Completer( Constant( completer ), Constant( 20 ) ) ) ) )
                    ),
                    Throw( Constant( new Exception( "Outer Exception" ) ) ),
                    Constant( 0 )
                ),
                Catch( outerExceptionParam, Assign( resultValue, Constant( 50 ) ) )
            ),
            resultValue
        );
        var lambda = Lambda<Func<Task<int>>>( block );
        var compiledLambda = lambda.Compile( compiler );

        // Act
        var result = await compiledLambda();

        // Assert
        Assert.AreEqual( 50, result ); // Outer catch handles the exception
    }

    [DataTestMethod]
    [DataRow( CompleterType.Immediate, CompilerType.Fast )]
    [DataRow( CompleterType.Immediate, CompilerType.System )]
    [DataRow( CompleterType.Immediate, CompilerType.Interpret )]
    [DataRow( CompleterType.Deferred, CompilerType.Fast )]
    [DataRow( CompleterType.Deferred, CompilerType.System )]
    [DataRow( CompleterType.Deferred, CompilerType.Interpret )]
    public async Task AsyncBlock_ShouldAwaitSuccessfully_WithComplexNestedTryBlock( CompleterType completer, CompilerType compiler )
    {
        // Arrange: Await in the try block
        var resultValue = Parameter( typeof( int ) );
        var block = BlockAsync(
            [resultValue],
            Await( AsyncHelper.Completer( Constant( completer ), Constant( 0 ) ) ),
            TryCatch(
                Block(
                    Assign( resultValue, Await( AsyncHelper.Completer( Constant( completer ), Constant( 10 ) ) ) ),
                    TryCatch(
                        Block(
                            Assign( resultValue, Await( AsyncHelper.Completer( Constant( completer ), Constant( 20 ) ) ) ),
                            TryCatch(
                                Assign( resultValue, Await( AsyncHelper.Completer( Constant( completer ), Constant( 30 ) ) ) ),
                                Catch( typeof( Exception ), Assign( resultValue, Constant( 1 ) ) )
                            ) ),
                        Catch( typeof( Exception ), Assign( resultValue, Constant( 2 ) ) )
                    ),
                    Assign( resultValue, Await( AsyncHelper.Completer( Constant( completer ), Constant( 40 ) ) ) ),
                    TryCatch(
                        Block(
                            Assign( resultValue, Await( AsyncHelper.Completer( Constant( completer ), Constant( 50 ) ) ) ),
                            TryCatch(
                                Assign( resultValue, Await( AsyncHelper.Completer( Constant( completer ), Constant( 60 ) ) ) ),
                                Catch( typeof( Exception ), Assign( resultValue, Constant( 3 ) ) )
                            ) )
                        ,
                        Catch( typeof( Exception ), Assign( resultValue, Constant( 4 ) ) )

                    ) ),
                Catch( typeof( Exception ), Assign( resultValue, Constant( 6 ) ) )
            ),
            resultValue
        );
        var lambda = Lambda<Func<Task<int>>>( block );
        var compiledLambda = lambda.Compile( compiler );

        // Act
        var result = await compiledLambda();

        // Assert
        Assert.AreEqual( 60, result );
    }

    [DataTestMethod]
    [DataRow( CompleterType.Immediate, CompilerType.Fast )]
    [DataRow( CompleterType.Immediate, CompilerType.System )]
    [DataRow( CompleterType.Immediate, CompilerType.Interpret )]
    [DataRow( CompleterType.Deferred, CompilerType.Fast )]
    [DataRow( CompleterType.Deferred, CompilerType.System )]
    [DataRow( CompleterType.Deferred, CompilerType.Interpret )]
    public async Task AsyncBlock_ShouldAwaitSuccessfully_WithComplexNestedTryFinallyBlock( CompleterType completer, CompilerType compiler )
    {
        // Arrange: Await in the try block
        var resultValue = Parameter( typeof( int ) );
        var block = BlockAsync(
            [resultValue],
            Await( AsyncHelper.Completer( Constant( completer ), Constant( 0 ) ) ),
            TryCatchFinally(
                Block(
                    Assign( resultValue, Await( AsyncHelper.Completer(
                        Constant( completer ),
                        Constant( 10 )
                    ) ) ),
                    TryCatch(
                        Block(
                            Assign( resultValue, Await( AsyncHelper.Completer(
                                Constant( completer ),
                                Constant( 20 )
                            ) ) ),
                            TryCatch(
                                Assign( resultValue, Await( AsyncHelper.Completer(
                                    Constant( completer ),
                                    Constant( 30 )
                                ) ) ),
                                Catch( typeof( Exception ), Assign( resultValue, Constant( 1 ) ) )
                            ) ),
                        Catch( typeof( Exception ), Assign( resultValue, Constant( 2 ) ) )
                    ),
                    Assign( resultValue, Await( AsyncHelper.Completer(
                        Constant( completer ),
                        Constant( 40 )
                    ) ) ),
                    TryCatch(
                        Block(
                            Assign( resultValue, Await( AsyncHelper.Completer(
                                Constant( completer ),
                                Constant( 50 )
                            ) ) ),
                            TryCatch(
                                Assign( resultValue, Await( AsyncHelper.Completer(
                                    Constant( completer ),
                                    Constant( 60 )
                                ) ) ),
                                Catch( typeof( Exception ), Assign( resultValue, Constant( 3 ) ) )
                            ) )
                        ,
                        Catch( typeof( Exception ), Assign( resultValue, Constant( 4 ) ) )

                    ) ),
                TryCatch(
                    Assign( resultValue, Await( AsyncHelper.Completer(
                        Constant( completer ),
                        Constant( 40 )
                    ) ) ),  // Finally block should be result
                    Catch( typeof( Exception ), Assign( resultValue, Constant( 5 ) ) )
                ),
                Catch( typeof( Exception ), Assign( resultValue, Constant( 6 ) ) )
            ),
            resultValue
        );
        var lambda = Lambda<Func<Task<int>>>( block );
        var compiledLambda = lambda.Compile( compiler );

        // Act
        var result = await compiledLambda();

        // Assert
        Assert.AreEqual( 40, result );
    }

    [DataTestMethod]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Interpret )]
    public async Task AsyncBlock_ShouldAwaitSuccessfully_WithNestedTryCatchAndDelayedAwait( CompilerType compiler )
    {
        // Arrange: Nested TryCatch with delayed await tasks (non-completed)
        var resultValue = Parameter( typeof( int ) );

        var delayedTask1 = Task.Delay( 100 ).ContinueWith( _ => 10 );
        var delayedTask2 = Task.Delay( 200 ).ContinueWith( _ => 20 );
        var delayedTask3 = Task.Delay( 300 ).ContinueWith( _ => 30 );

        var block = BlockAsync(
            [resultValue],
            TryCatch(
                Block(
                    // Await the first delayed task in the outer try
                    Assign( resultValue, Await( Constant( delayedTask1 ) ) ),
                    TryCatch(
                        Block(
                            // Await the second delayed task in the inner try
                            Assign( resultValue, Await( Constant( delayedTask2 ) ) ),
                            TryCatch(
                                // Await the third delayed task in the innermost try
                                Assign( resultValue, Await( Constant( delayedTask3 ) ) ),
                                Catch( typeof( Exception ), Assign( resultValue, Constant( 99 ) ) )
                            )
                        ),
                        Catch( typeof( Exception ), Assign( resultValue, Constant( 50 ) ) )
                    )
                ),
                Catch( typeof( Exception ), Assign( resultValue, Constant( 25 ) ) )
            ),
            resultValue
        );
        var lambda = Lambda<Func<Task<int>>>( block );
        var compiledLambda = lambda.Compile( compiler );

        // Act
        var result = await compiledLambda();

        // Assert
        Assert.AreEqual( 30, result ); // Ensure the final delayed task completes and continues correctly
    }
}
