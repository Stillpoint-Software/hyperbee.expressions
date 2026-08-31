using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using Hyperbee.Expressions.Tests.TestSupport;
using static System.Linq.Expressions.Expression;
using static Hyperbee.Expressions.ExpressionExtensions;

namespace Hyperbee.Expressions.Tests;

[TestClass]
public class BlockAsyncTryCatchTests
{
    private static Expression MarkRan( StrongBox<bool> box ) =>
        Assign( Field( Constant( box ), nameof( StrongBox<bool>.Value ) ), Constant( true ) );

    [TestMethod]
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

    [TestMethod]
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

    [TestMethod]
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

    [TestMethod]
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

    [TestMethod]
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

    [TestMethod]
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

    [TestMethod]
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

    [TestMethod]
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

    [TestMethod]
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

    [TestMethod]
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

    [TestMethod]
    [DataRow( CompleterType.Immediate, CompilerType.Fast )]
    [DataRow( CompleterType.Immediate, CompilerType.System )]
    [DataRow( CompleterType.Immediate, CompilerType.Interpret )]
    [DataRow( CompleterType.Deferred, CompilerType.Fast )]
    [DataRow( CompleterType.Deferred, CompilerType.System )]
    [DataRow( CompleterType.Deferred, CompilerType.Interpret )]
    public async Task AsyncBlock_ShouldReturnCorrectValue_WithReturnLabelInsideTryCatch( CompleterType completer, CompilerType compiler )
    {
        // NOTE: This test exercises Return labels inside TryCatch blocks. During async lowering,
        // Return expressions get transformed to include assignment of a result variable before the goto,
        // creating patterns like Return(label, Assign(_result, value)).
        //
        // FEC has documented error 1007 (NotSupported_Try_GotoReturnToTheFollowupLabel) for Return gotos
        // from TryCatch, but the detection is incomplete - it misses compound expressions containing assignments.
        // When FEC is fixed to detect these patterns, it should return null, allowing ExpressionCompilerExtensions
        // to fallback to System compiler.
        //
        // Known issue: https://github.com/dadhi/FastExpressionCompiler/issues/495
        // When FEC issue 495 is fixed, this test should pass for CompilerType.Fast as well.

        if ( compiler == CompilerType.Fast )
        {
            // Skip this test for Fast compiler until FEC issue 495 is resolved
            Assert.Inconclusive( "Skipping test for Fast compiler due to known issue with Return labels in TryCatch blocks." );
            return;
        }

        // Arrange
        var expected = new object();
        var variable = Variable( typeof( object ) );
        var label = Label( typeof( object ), "return" );

        var block = BlockAsync(
            [variable],
            TryCatch(
                Block(
                    typeof( void ),
                    Assign(
                        variable,
                        Await( AsyncHelper.Completer(
                            Constant( completer ),
                            Constant( expected, typeof( object ) )
                        ) ) ),
                    IfThen(
                        NotEqual(
                            variable,
                            Constant( null, typeof( object ) ) ),
                        Return(
                            label,
                            variable,
                            typeof( object ) ) ),
                    Return(
                        label,
                        Constant(
                            new object(),
                            typeof( object ) ),
                        typeof( object ) ),
                    Label(
                        label,
                        Constant(
                            new object(),
                            typeof( object ) ) )
                ),
                Catch( typeof( Exception ), Empty() )
            ),
            variable
        );

        var lambda = Lambda<Func<Task<object>>>( block );
        var compiledLambda = lambda.Compile( compiler );

        // Act
        var result = await compiledLambda();

        // Assert - should return 'expected', not null
        Assert.AreSame( expected, result );
    }

    // Catch variables
    //
    // Lowering moves a catch handler body into its own state, outside of the generated
    // try. The catch variable must be hoisted so the handler can still reference it.

    [TestMethod]
    [DataRow( CompleterType.Immediate, CompilerType.Fast )]
    [DataRow( CompleterType.Immediate, CompilerType.System )]
    [DataRow( CompleterType.Immediate, CompilerType.Interpret )]
    [DataRow( CompleterType.Deferred, CompilerType.Fast )]
    [DataRow( CompleterType.Deferred, CompilerType.System )]
    [DataRow( CompleterType.Deferred, CompilerType.Interpret )]
    public async Task AsyncBlock_ShouldBindCatchVariable_WithAwaitInTryBlock( CompleterType completer, CompilerType compiler )
    {
        // Arrange: the handler reads the catch variable
        var exceptionParam = Parameter( typeof( Exception ), "ex" );

        var block = BlockAsync(
            TryCatch(
                Block(
                    typeof( string ),
                    Await( AsyncHelper.Completer( Constant( completer ), Constant( 1 ) ) ),
                    Throw( Constant( new InvalidOperationException( "Boom" ) ) ),
                    Constant( "not-thrown" )
                ),
                Catch( exceptionParam, Property( exceptionParam, nameof( Exception.Message ) ) )
            )
        );

        var lambda = Lambda<Func<Task<string>>>( block );
        var compiledLambda = lambda.Compile( compiler );

        // Act
        var result = await compiledLambda();

        // Assert
        Assert.AreEqual( "Boom", result );
    }

    [TestMethod]
    [DataRow( CompleterType.Immediate, CompilerType.Fast )]
    [DataRow( CompleterType.Immediate, CompilerType.System )]
    [DataRow( CompleterType.Immediate, CompilerType.Interpret )]
    [DataRow( CompleterType.Deferred, CompilerType.Fast )]
    [DataRow( CompleterType.Deferred, CompilerType.System )]
    [DataRow( CompleterType.Deferred, CompilerType.Interpret )]
    public async Task AsyncBlock_ShouldBindCatchVariable_WithAwaitInCatchBlock( CompleterType completer, CompilerType compiler )
    {
        // Arrange: the handler reads the catch variable across an await
        var exceptionParam = Parameter( typeof( Exception ), "ex" );
        var resultValue = Variable( typeof( string ), "result" );

        var block = BlockAsync(
            [resultValue],
            TryCatch(
                Block(
                    typeof( void ),
                    Throw( Constant( new InvalidOperationException( "Boom" ) ) )
                ),
                Catch( exceptionParam,
                    Block(
                        typeof( void ),
                        Assign( resultValue, Await( AsyncHelper.Completer(
                            Constant( completer ),
                            Property( exceptionParam, nameof( Exception.Message ) )
                        ) ) )
                    )
                )
            ),
            resultValue
        );

        var lambda = Lambda<Func<Task<string>>>( block );
        var compiledLambda = lambda.Compile( compiler );

        // Act
        var result = await compiledLambda();

        // Assert
        Assert.AreEqual( "Boom", result );
    }

    [TestMethod]
    [DataRow( CompleterType.Immediate, CompilerType.Fast )]
    [DataRow( CompleterType.Immediate, CompilerType.System )]
    [DataRow( CompleterType.Immediate, CompilerType.Interpret )]
    [DataRow( CompleterType.Deferred, CompilerType.Fast )]
    [DataRow( CompleterType.Deferred, CompilerType.System )]
    [DataRow( CompleterType.Deferred, CompilerType.Interpret )]
    public async Task AsyncBlock_ShouldBindCatchVariable_WhenRethrowingWrapped( CompleterType completer, CompilerType compiler )
    {
        // Arrange: the handler uses the catch variable twice, as an argument and as a member target
        var exceptionParam = Parameter( typeof( Exception ), "ex" );

        var exceptionCtor = typeof( InvalidOperationException )
            .GetConstructor( [typeof( string ), typeof( Exception )] )!;

        var block = BlockAsync(
            TryCatch(
                Block(
                    typeof( object ),
                    Await( AsyncHelper.Completer( Constant( completer ), Constant( 1 ) ) ),
                    Throw( Constant( new InvalidOperationException( "Boom" ) ) ),
                    Constant( null, typeof( object ) )
                ),
                Catch( exceptionParam,
                    Throw(
                        New(
                            exceptionCtor,
                            Property( exceptionParam, nameof( Exception.Message ) ),
                            exceptionParam ),
                        typeof( object ) )
                )
            )
        );

        var lambda = Lambda<Func<Task<object>>>( block );
        var compiledLambda = lambda.Compile( compiler );

        // Act
        var exception = await Assert.ThrowsExactlyAsync<InvalidOperationException>( async () => await compiledLambda() );

        // Assert
        Assert.AreEqual( "Boom", exception.Message );
        Assert.IsInstanceOfType<InvalidOperationException>( exception.InnerException );
    }

    [TestMethod]
    [DataRow( CompleterType.Immediate, CompilerType.Fast )]
    [DataRow( CompleterType.Immediate, CompilerType.System )]
    [DataRow( CompleterType.Immediate, CompilerType.Interpret )]
    [DataRow( CompleterType.Deferred, CompilerType.Fast )]
    [DataRow( CompleterType.Deferred, CompilerType.System )]
    [DataRow( CompleterType.Deferred, CompilerType.Interpret )]
    public async Task AsyncBlock_ShouldReturnCatchBlockValue( CompleterType completer, CompilerType compiler )
    {
        // Arrange: the value of the try expression comes from the handler
        var block = BlockAsync(
            TryCatch(
                Block(
                    typeof( int ),
                    Await( AsyncHelper.Completer( Constant( completer ), Constant( 1 ) ) ),
                    Throw( Constant( new InvalidOperationException( "Boom" ) ) ),
                    Constant( 10 )
                ),
                Catch( typeof( Exception ), Constant( 50 ) )
            )
        );

        var lambda = Lambda<Func<Task<int>>>( block );
        var compiledLambda = lambda.Compile( compiler );

        // Act
        var result = await compiledLambda();

        // Assert
        Assert.AreEqual( 50, result );
    }

    // Catch filters

    [TestMethod]
    [DataRow( CompleterType.Immediate, CompilerType.Fast )]
    [DataRow( CompleterType.Immediate, CompilerType.System )]
    [DataRow( CompleterType.Immediate, CompilerType.Interpret )]
    [DataRow( CompleterType.Deferred, CompilerType.Fast )]
    [DataRow( CompleterType.Deferred, CompilerType.System )]
    [DataRow( CompleterType.Deferred, CompilerType.Interpret )]
    public async Task AsyncBlock_ShouldCatchException_WhenCatchFilterMatches( CompleterType completer, CompilerType compiler )
    {
        // Arrange
        var exceptionParam = Parameter( typeof( Exception ), "ex" );

        var block = BlockAsync(
            TryCatch(
                Block(
                    typeof( string ),
                    Await( AsyncHelper.Completer( Constant( completer ), Constant( 1 ) ) ),
                    Throw( Constant( new InvalidOperationException( "Boom" ) ) ),
                    Constant( "not-thrown" )
                ),
                MakeCatchBlock(
                    typeof( Exception ),
                    exceptionParam,
                    Property( exceptionParam, nameof( Exception.Message ) ),
                    Equal( Property( exceptionParam, nameof( Exception.Message ) ), Constant( "Boom" ) )
                )
            )
        );

        var lambda = Lambda<Func<Task<string>>>( block );
        var compiledLambda = lambda.Compile( compiler );

        // Act
        var result = await compiledLambda();

        // Assert
        Assert.AreEqual( "Boom", result );
    }

    [TestMethod]
    [DataRow( CompleterType.Immediate, CompilerType.Fast )]
    [DataRow( CompleterType.Immediate, CompilerType.System )]
    [DataRow( CompleterType.Immediate, CompilerType.Interpret )]
    [DataRow( CompleterType.Deferred, CompilerType.Fast )]
    [DataRow( CompleterType.Deferred, CompilerType.System )]
    [DataRow( CompleterType.Deferred, CompilerType.Interpret )]
    public async Task AsyncBlock_ShouldNotCatchException_WhenCatchFilterDoesNotMatch( CompleterType completer, CompilerType compiler )
    {
        // Arrange
        var exceptionParam = Parameter( typeof( Exception ), "ex" );

        var block = BlockAsync(
            TryCatch(
                Block(
                    typeof( string ),
                    Await( AsyncHelper.Completer( Constant( completer ), Constant( 1 ) ) ),
                    Throw( Constant( new InvalidOperationException( "Boom" ) ) ),
                    Constant( "not-thrown" )
                ),
                MakeCatchBlock(
                    typeof( Exception ),
                    exceptionParam,
                    Constant( "caught" ),
                    Equal( Property( exceptionParam, nameof( Exception.Message ) ), Constant( "Other" ) )
                )
            )
        );

        var lambda = Lambda<Func<Task<string>>>( block );
        var compiledLambda = lambda.Compile( compiler );

        // Act & Assert
        await Assert.ThrowsExactlyAsync<InvalidOperationException>( async () => await compiledLambda() );
    }

    [TestMethod]
    public void AsyncBlock_ShouldThrow_WithAwaitInCatchFilter()
    {
        // Arrange
        var exceptionParam = Parameter( typeof( Exception ), "ex" );

        var block = BlockAsync(
            TryCatch(
                Block(
                    typeof( int ),
                    Await( AsyncHelper.Completer( Constant( CompleterType.Immediate ), Constant( 1 ) ) )
                ),
                MakeCatchBlock(
                    typeof( Exception ),
                    exceptionParam,
                    Constant( 0 ),
                    Await( AsyncHelper.Completer( Constant( CompleterType.Immediate ), Constant( true ) ) )
                )
            )
        );

        var lambda = Lambda<Func<Task<int>>>( block );

        // Act & Assert
        Assert.ThrowsExactly<InvalidOperationException>( () => lambda.Compile( CompilerType.System ) );
    }

    [TestMethod]
    public void AsyncBlock_ShouldThrow_WithFaultHandler()
    {
        // Arrange
        var block = BlockAsync(
            TryFault(
                Block(
                    typeof( void ),
                    Await( AsyncHelper.Completer( Constant( CompleterType.Immediate ), Constant( 1 ) ) )
                ),
                Empty()
            ),
            Constant( 1 )
        );

        var lambda = Lambda<Func<Task<int>>>( block );

        // Act & Assert
        Assert.ThrowsExactly<InvalidOperationException>( () => lambda.Compile( CompilerType.System ) );
    }

    // Finally

    [TestMethod]
    [DataRow( CompleterType.Immediate, CompilerType.Fast )]
    [DataRow( CompleterType.Immediate, CompilerType.System )]
    [DataRow( CompleterType.Immediate, CompilerType.Interpret )]
    [DataRow( CompleterType.Deferred, CompilerType.Fast )]
    [DataRow( CompleterType.Deferred, CompilerType.System )]
    [DataRow( CompleterType.Deferred, CompilerType.Interpret )]
    public async Task AsyncBlock_ShouldPropagateException_WhenNoCatchHandlesIt( CompleterType completer, CompilerType compiler )
    {
        // Arrange: try/finally must not swallow the exception
        var log = Variable( typeof( string ), "log" );

        var block = BlockAsync(
            [log],
            Assign( log, Constant( "" ) ),
            TryFinally(
                Block(
                    typeof( void ),
                    Await( AsyncHelper.Completer( Constant( completer ), Constant( 1 ) ) ),
                    Throw( Constant( new InvalidOperationException( "Boom" ) ) )
                ),
                Assign( log, Constant( "F" ) )
            ),
            log
        );

        var lambda = Lambda<Func<Task<string>>>( block );
        var compiledLambda = lambda.Compile( compiler );

        // Act & Assert
        var exception = await Assert.ThrowsExactlyAsync<InvalidOperationException>( async () => await compiledLambda() );

        Assert.AreEqual( "Boom", exception.Message );
    }

    [TestMethod]
    [DataRow( CompleterType.Immediate, CompilerType.Fast )]
    [DataRow( CompleterType.Immediate, CompilerType.System )]
    [DataRow( CompleterType.Immediate, CompilerType.Interpret )]
    [DataRow( CompleterType.Deferred, CompilerType.Fast )]
    [DataRow( CompleterType.Deferred, CompilerType.System )]
    [DataRow( CompleterType.Deferred, CompilerType.Interpret )]
    public async Task AsyncBlock_ShouldRunFinally_WhenCatchHandlesException( CompleterType completer, CompilerType compiler )
    {
        // Arrange
        var log = Variable( typeof( string ), "log" );
        var concat = typeof( string ).GetMethod( nameof( string.Concat ), [typeof( string ), typeof( string )] )!;

        var block = BlockAsync(
            [log],
            Assign( log, Constant( "" ) ),
            TryCatchFinally(
                Block(
                    typeof( void ),
                    Await( AsyncHelper.Completer( Constant( completer ), Constant( 1 ) ) ),
                    Throw( Constant( new InvalidOperationException( "Boom" ) ) )
                ),
                Assign( log, Add( log, Constant( "F" ), concat ) ),
                Catch( typeof( Exception ),
                    Block( typeof( void ), Assign( log, Add( log, Constant( "C" ), concat ) ) ) )
            ),
            log
        );

        var lambda = Lambda<Func<Task<string>>>( block );
        var compiledLambda = lambda.Compile( compiler );

        // Act
        var result = await compiledLambda();

        // Assert
        Assert.AreEqual( "CF", result );
    }

    // Resumption
    //
    // An await inside a try suspends into a nested state scope. The state machine must
    // resume into that scope, and must not re-run expressions that precede the try.

    [TestMethod]
    [DataRow( CompleterType.Immediate, CompilerType.Fast )]
    [DataRow( CompleterType.Immediate, CompilerType.System )]
    [DataRow( CompleterType.Immediate, CompilerType.Interpret )]
    [DataRow( CompleterType.Deferred, CompilerType.Fast )]
    [DataRow( CompleterType.Deferred, CompilerType.System )]
    [DataRow( CompleterType.Deferred, CompilerType.Interpret )]
    public async Task AsyncBlock_ShouldNotReExecutePrologue_WithAwaitOnlyInsideTry( CompleterType completer, CompilerType compiler )
    {
        // Arrange
        var count = Variable( typeof( int ), "count" );

        var block = BlockAsync(
            [count],
            Assign( count, Add( count, Constant( 1 ) ) ),
            TryCatch(
                Block(
                    typeof( void ),
                    Await( AsyncHelper.Completer( Constant( completer ), Constant( 1 ) ) )
                ),
                Catch( typeof( Exception ), Empty() )
            ),
            count
        );

        var lambda = Lambda<Func<Task<int>>>( block );
        var compiledLambda = lambda.Compile( compiler );

        // Act
        var result = await compiledLambda();

        // Assert
        Assert.AreEqual( 1, result );
    }

    [TestMethod]
    [DataRow( CompleterType.Immediate, CompilerType.Fast )]
    [DataRow( CompleterType.Immediate, CompilerType.System )]
    [DataRow( CompleterType.Immediate, CompilerType.Interpret )]
    [DataRow( CompleterType.Deferred, CompilerType.Fast )]
    [DataRow( CompleterType.Deferred, CompilerType.System )]
    [DataRow( CompleterType.Deferred, CompilerType.Interpret )]
    public async Task AsyncBlock_ShouldIterate_WithTryCatchInsideLoop( CompleterType completer, CompilerType compiler )
    {
        // Arrange: the first iteration throws, the rest do not
        var index = Variable( typeof( int ), "i" );
        var log = Variable( typeof( string ), "log" );
        var breakLabel = Label( "breakLabel" );
        var concat = typeof( string ).GetMethod( nameof( string.Concat ), [typeof( string ), typeof( string )] )!;

        var block = BlockAsync(
            [index, log],
            Assign( log, Constant( "" ) ),
            Assign( index, Constant( 0 ) ),
            Loop(
                Block(
                    IfThen( GreaterThanOrEqual( index, Constant( 3 ) ), Break( breakLabel ) ),
                    TryCatch(
                        Block(
                            typeof( void ),
                            Assign( index, Await( AsyncHelper.Completer( Constant( completer ), Add( index, Constant( 1 ) ) ) ) ),
                            IfThen( Equal( index, Constant( 1 ) ),
                                Throw( Constant( new InvalidOperationException( "Boom" ) ) ) ),
                            Assign( log, Add( log, Constant( "T" ), concat ) )
                        ),
                        Catch( typeof( Exception ),
                            Block( typeof( void ), Assign( log, Add( log, Constant( "C" ), concat ) ) ) )
                    )
                ),
                breakLabel,
                null
            ),
            log
        );

        var lambda = Lambda<Func<Task<string>>>( block );
        var compiledLambda = lambda.Compile( compiler );

        // Act
        var result = await compiledLambda();

        // Assert
        Assert.AreEqual( "CTT", result );
    }

    [TestMethod]
    [DataRow( CompleterType.Immediate, CompilerType.Fast )]
    [DataRow( CompleterType.Immediate, CompilerType.System )]
    [DataRow( CompleterType.Immediate, CompilerType.Interpret )]
    [DataRow( CompleterType.Deferred, CompilerType.Fast )]
    [DataRow( CompleterType.Deferred, CompilerType.System )]
    [DataRow( CompleterType.Deferred, CompilerType.Interpret )]
    public async Task AsyncBlock_ShouldIterate_WithTryFinallyInsideLoop( CompleterType completer, CompilerType compiler )
    {
        // Arrange
        var index = Variable( typeof( int ), "i" );
        var log = Variable( typeof( string ), "log" );
        var breakLabel = Label( "breakLabel" );
        var concat = typeof( string ).GetMethod( nameof( string.Concat ), [typeof( string ), typeof( string )] )!;

        var block = BlockAsync(
            [index, log],
            Assign( log, Constant( "" ) ),
            Assign( index, Constant( 0 ) ),
            Loop(
                Block(
                    IfThen( GreaterThanOrEqual( index, Constant( 3 ) ), Break( breakLabel ) ),
                    TryFinally(
                        Block(
                            typeof( void ),
                            Assign( index, Await( AsyncHelper.Completer( Constant( completer ), Add( index, Constant( 1 ) ) ) ) ),
                            Assign( log, Add( log, Constant( "T" ), concat ) )
                        ),
                        Assign( log, Add( log, Constant( "F" ), concat ) )
                    )
                ),
                breakLabel,
                null
            ),
            log
        );

        var lambda = Lambda<Func<Task<string>>>( block );
        var compiledLambda = lambda.Compile( compiler );

        // Act
        var result = await compiledLambda();

        // Assert
        Assert.AreEqual( "TFTFTF", result );
    }

    // Catch-filter semantics
    //
    // Shapes borrowed from the .NET runtime's TryExpression conformance tests
    // (System.Linq.Expressions.Tests.ExceptionHandlingExpressions), lowered through
    // BlockAsync so the handler body runs in its own state.

    [TestMethod]
    [DataRow( CompleterType.Immediate, CompilerType.Fast )]
    [DataRow( CompleterType.Immediate, CompilerType.System )]
    [DataRow( CompleterType.Immediate, CompilerType.Interpret )]
    [DataRow( CompleterType.Deferred, CompilerType.Fast )]
    [DataRow( CompleterType.Deferred, CompilerType.System )]
    [DataRow( CompleterType.Deferred, CompilerType.Interpret )]
    public async Task AsyncBlock_FilterWriteToCatchVariable_IsVisibleToHandler( CompleterType completer, CompilerType compiler )
    {
        // Arrange: the filter assigns the catch variable and returns true; the handler
        // must observe the assignment.
        var exceptionParam = Parameter( typeof( InvalidOperationException ), "ex" );

        var block = BlockAsync(
            TryCatch(
                Block(
                    typeof( bool ),
                    Await( AsyncHelper.Completer( Constant( completer ), Constant( 1 ) ) ),
                    Throw( Constant( new InvalidOperationException( "Boom" ) ) ),
                    Constant( false )
                ),
                MakeCatchBlock(
                    typeof( InvalidOperationException ),
                    exceptionParam,
                    ReferenceEqual( Constant( null, typeof( InvalidOperationException ) ), exceptionParam ),
                    Block(
                        Assign( exceptionParam, Constant( null, typeof( InvalidOperationException ) ) ),
                        Constant( true ) )
                )
            )
        );

        var lambda = Lambda<Func<Task<bool>>>( block );
        var compiledLambda = lambda.Compile( compiler );

        // Act
        var result = await compiledLambda();

        // Assert
        Assert.IsTrue( result );
    }

    [TestMethod]
    [DataRow( CompleterType.Immediate, CompilerType.Fast )]
    [DataRow( CompleterType.Immediate, CompilerType.System )]
    [DataRow( CompleterType.Immediate, CompilerType.Interpret )]
    [DataRow( CompleterType.Deferred, CompilerType.Fast )]
    [DataRow( CompleterType.Deferred, CompilerType.System )]
    [DataRow( CompleterType.Deferred, CompilerType.Interpret )]
    public async Task AsyncBlock_FilterWriteToCatchVariable_IsNotVisibleToNextHandler( CompleterType completer, CompilerType compiler )
    {
        // Arrange: the first filter assigns its catch variable and declines. The second
        // handler must still see the original exception.
        var first = Parameter( typeof( InvalidOperationException ), "first" );
        var second = Parameter( typeof( InvalidOperationException ), "second" );

        var block = BlockAsync(
            TryCatch(
                Block(
                    typeof( string ),
                    Await( AsyncHelper.Completer( Constant( completer ), Constant( 1 ) ) ),
                    Throw( Constant( new InvalidOperationException( "Boom" ) ) ),
                    Constant( "not-thrown" )
                ),
                MakeCatchBlock(
                    typeof( InvalidOperationException ),
                    first,
                    Constant( "first" ),
                    Block(
                        Assign( first, Constant( null, typeof( InvalidOperationException ) ) ),
                        Constant( false ) )
                ),
                MakeCatchBlock(
                    typeof( InvalidOperationException ),
                    second,
                    Property( second, nameof( Exception.Message ) ),
                    Constant( true )
                )
            )
        );

        var lambda = Lambda<Func<Task<string>>>( block );
        var compiledLambda = lambda.Compile( compiler );

        // Act
        var result = await compiledLambda();

        // Assert
        Assert.AreEqual( "Boom", result );
    }

    [TestMethod]
    [DataRow( CompleterType.Immediate, CompilerType.System )]
    [DataRow( CompleterType.Immediate, CompilerType.Interpret )]
    [DataRow( CompleterType.Deferred, CompilerType.System )]
    [DataRow( CompleterType.Deferred, CompilerType.Interpret )]
    public async Task AsyncBlock_SecondHandlerRuns_WhenFirstFilterDeclines( CompleterType completer, CompilerType compiler )
    {
        // Arrange
        var first = Parameter( typeof( Exception ), "first" );
        var second = Parameter( typeof( Exception ), "second" );

        var block = BlockAsync(
            TryCatch(
                Block(
                    typeof( int ),
                    Await( AsyncHelper.Completer( Constant( completer ), Constant( 1 ) ) ),
                    Throw( Constant( new InvalidOperationException( "Boom" ) ) ),
                    Constant( 0 )
                ),
                MakeCatchBlock( typeof( Exception ), first, Constant( 1 ), Constant( false ) ),
                MakeCatchBlock( typeof( Exception ), second, Constant( 2 ), Constant( true ) )
            )
        );

        var lambda = Lambda<Func<Task<int>>>( block );
        var compiledLambda = lambda.Compile( compiler );

        // Act
        var result = await compiledLambda();

        // Assert
        Assert.AreEqual( 2, result );
    }

    [TestMethod]
    [DataRow( CompleterType.Immediate, CompilerType.Fast )]
    [DataRow( CompleterType.Immediate, CompilerType.System )]
    [DataRow( CompleterType.Immediate, CompilerType.Interpret )]
    [DataRow( CompleterType.Deferred, CompilerType.Fast )]
    [DataRow( CompleterType.Deferred, CompilerType.System )]
    [DataRow( CompleterType.Deferred, CompilerType.Interpret )]
    public async Task AsyncBlock_ShouldRethrowOriginal_WithBareRethrowInCatch( CompleterType completer, CompilerType compiler )
    {
        // Arrange: a bare `throw;` in a handler that lowering moves out of the try
        var exceptionParam = Parameter( typeof( Exception ), "ex" );

        var block = BlockAsync(
            TryCatch(
                Block(
                    typeof( int ),
                    Await( AsyncHelper.Completer( Constant( completer ), Constant( 1 ) ) ),
                    Throw( Constant( new InvalidOperationException( "Boom" ) ) ),
                    Constant( 0 )
                ),
                Catch( exceptionParam, Rethrow( typeof( int ) ) )
            )
        );

        var lambda = Lambda<Func<Task<int>>>( block );
        var compiledLambda = lambda.Compile( compiler );

        // Act
        var exception = await Assert.ThrowsExactlyAsync<InvalidOperationException>( async () => await compiledLambda() );

        // Assert
        Assert.AreEqual( "Boom", exception.Message );
        Assert.IsNull( exception.InnerException );
    }

    [TestMethod]
    [DataRow( CompleterType.Immediate, CompilerType.Fast )]
    [DataRow( CompleterType.Immediate, CompilerType.System )]
    [DataRow( CompleterType.Immediate, CompilerType.Interpret )]
    [DataRow( CompleterType.Deferred, CompilerType.Fast )]
    [DataRow( CompleterType.Deferred, CompilerType.System )]
    [DataRow( CompleterType.Deferred, CompilerType.Interpret )]
    public async Task AsyncBlock_ShouldRethrowOriginal_WithBareRethrowAndNoCatchVariable( CompleterType completer, CompilerType compiler )
    {
        // Arrange: the handler declares no variable, so the rewrite has to hoist one
        var block = BlockAsync(
            TryCatch(
                Block(
                    typeof( int ),
                    Await( AsyncHelper.Completer( Constant( completer ), Constant( 1 ) ) ),
                    Throw( Constant( new InvalidOperationException( "Boom" ) ) ),
                    Constant( 0 )
                ),
                Catch( typeof( InvalidOperationException ), Rethrow( typeof( int ) ) )
            )
        );

        var lambda = Lambda<Func<Task<int>>>( block );
        var compiledLambda = lambda.Compile( compiler );

        // Act
        var exception = await Assert.ThrowsExactlyAsync<InvalidOperationException>( async () => await compiledLambda() );

        // Assert
        Assert.AreEqual( "Boom", exception.Message );
    }

    [TestMethod]
    [DataRow( CompleterType.Immediate, CompilerType.Fast )]
    [DataRow( CompleterType.Immediate, CompilerType.System )]
    [DataRow( CompleterType.Immediate, CompilerType.Interpret )]
    [DataRow( CompleterType.Deferred, CompilerType.Fast )]
    [DataRow( CompleterType.Deferred, CompilerType.System )]
    [DataRow( CompleterType.Deferred, CompilerType.Interpret )]
    public async Task AsyncBlock_ShouldPreserveStackTrace_WithBareRethrowInCatch( CompleterType completer, CompilerType compiler )
    {
        // Arrange: a rethrow must not reset the stack trace of the original throw site
        var exceptionParam = Parameter( typeof( Exception ), "ex" );

        var block = BlockAsync(
            TryCatch(
                Block(
                    typeof( int ),
                    Await( AsyncHelper.Completer( Constant( completer ), Constant( 1 ) ) ),
                    Call( typeof( BlockAsyncTryCatchTests ).GetMethod( nameof( ThrowFromKnownFrame ) )! )
                ),
                Catch( exceptionParam, Rethrow( typeof( int ) ) )
            )
        );

        var lambda = Lambda<Func<Task<int>>>( block );
        var compiledLambda = lambda.Compile( compiler );

        // Act
        var exception = await Assert.ThrowsExactlyAsync<InvalidOperationException>( async () => await compiledLambda() );

        // Assert
        Assert.Contains( nameof( ThrowFromKnownFrame ), exception.StackTrace ?? string.Empty );
    }

    public static int ThrowFromKnownFrame() => throw new InvalidOperationException( "Boom" );

    [TestMethod]
    [DataRow( CompleterType.Immediate, CompilerType.Fast )]
    [DataRow( CompleterType.Immediate, CompilerType.System )]
    [DataRow( CompleterType.Immediate, CompilerType.Interpret )]
    [DataRow( CompleterType.Deferred, CompilerType.Fast )]
    [DataRow( CompleterType.Deferred, CompilerType.System )]
    [DataRow( CompleterType.Deferred, CompilerType.Interpret )]
    public async Task AsyncBlock_ShouldRethrow_WithAwaitBeforeBareRethrow( CompleterType completer, CompilerType compiler )
    {
        // Arrange: the handler itself suspends, so the rethrow runs in a later state
        var exceptionParam = Parameter( typeof( Exception ), "ex" );

        var block = BlockAsync(
            TryCatch(
                Block(
                    typeof( int ),
                    Throw( Constant( new InvalidOperationException( "Boom" ) ) ),
                    Constant( 0 )
                ),
                Catch( exceptionParam,
                    Block(
                        typeof( int ),
                        Await( AsyncHelper.Completer( Constant( completer ), Constant( 1 ) ) ),
                        Rethrow( typeof( int ) )
                    ) )
            )
        );

        var lambda = Lambda<Func<Task<int>>>( block );
        var compiledLambda = lambda.Compile( compiler );

        // Act
        var exception = await Assert.ThrowsExactlyAsync<InvalidOperationException>( async () => await compiledLambda() );

        // Assert
        Assert.AreEqual( "Boom", exception.Message );
    }

    [TestMethod]
    [DataRow( CompleterType.Immediate, CompilerType.Fast )]
    [DataRow( CompleterType.Immediate, CompilerType.System )]
    [DataRow( CompleterType.Immediate, CompilerType.Interpret )]
    [DataRow( CompleterType.Deferred, CompilerType.Fast )]
    [DataRow( CompleterType.Deferred, CompilerType.System )]
    [DataRow( CompleterType.Deferred, CompilerType.Interpret )]
    public async Task AsyncBlock_ShouldRunFinally_WhenHandlerBareRethrows( CompleterType completer, CompilerType compiler )
    {
        // Arrange: a rethrowing handler must still run the finally on the way out
        var exceptionParam = Parameter( typeof( Exception ), "ex" );

        var ranFinally = new StrongBox<bool>( false );

        var block = BlockAsync(
            TryCatchFinally(
                Block(
                    typeof( int ),
                    Await( AsyncHelper.Completer( Constant( completer ), Constant( 1 ) ) ),
                    Throw( Constant( new InvalidOperationException( "Boom" ) ) ),
                    Constant( 0 )
                ),
                MarkRan( ranFinally ),
                Catch( exceptionParam, Rethrow( typeof( int ) ) )
            )
        );

        var lambda = Lambda<Func<Task<int>>>( block );
        var compiledLambda = lambda.Compile( compiler );

        // Act
        var exception = await Assert.ThrowsExactlyAsync<InvalidOperationException>( async () => await compiledLambda() );

        // Assert
        Assert.AreEqual( "Boom", exception.Message );
        Assert.IsTrue( ranFinally.Value );
    }

    [TestMethod]
    [DataRow( CompleterType.Immediate, CompilerType.Fast )]
    [DataRow( CompleterType.Immediate, CompilerType.System )]
    [DataRow( CompleterType.Immediate, CompilerType.Interpret )]
    [DataRow( CompleterType.Deferred, CompilerType.Fast )]
    [DataRow( CompleterType.Deferred, CompilerType.System )]
    [DataRow( CompleterType.Deferred, CompilerType.Interpret )]
    public async Task AsyncBlock_ShouldCatchBareRethrow_InEnclosingHandler( CompleterType completer, CompilerType compiler )
    {
        // Arrange: an inner handler rethrows, an outer handler catches the same exception
        var inner = Parameter( typeof( Exception ), "inner" );
        var outer = Parameter( typeof( Exception ), "outer" );

        var block = BlockAsync(
            TryCatch(
                TryCatch(
                    Block(
                        typeof( string ),
                        Await( AsyncHelper.Completer( Constant( completer ), Constant( 1 ) ) ),
                        Throw( Constant( new InvalidOperationException( "Boom" ) ) ),
                        Constant( string.Empty )
                    ),
                    Catch( inner, Rethrow( typeof( string ) ) )
                ),
                Catch( outer, Property( outer, nameof( Exception.Message ) ) )
            )
        );

        var lambda = Lambda<Func<Task<string>>>( block );
        var compiledLambda = lambda.Compile( compiler );

        // Act
        var result = await compiledLambda();

        // Assert: the outer handler saw the rethrown exception
        Assert.AreEqual( "Boom", result );
    }

    [TestMethod]
    [DataRow( CompleterType.Immediate, CompilerType.Fast )]
    [DataRow( CompleterType.Immediate, CompilerType.System )]
    [DataRow( CompleterType.Immediate, CompilerType.Interpret )]
    [DataRow( CompleterType.Deferred, CompilerType.Fast )]
    [DataRow( CompleterType.Deferred, CompilerType.System )]
    [DataRow( CompleterType.Deferred, CompilerType.Interpret )]
    public async Task AsyncBlock_ShouldBindBareRethrow_ToNearestHandler( CompleterType completer, CompilerType compiler )
    {
        // Arrange: a nested handler with no await stays a real catch block. Its bare
        // rethrow belongs to it, not to the lowered handler that encloses it.
        var outerEx = Parameter( typeof( Exception ), "outerEx" );
        var innerEx = Parameter( typeof( Exception ), "innerEx" );

        var block = BlockAsync(
            TryCatch(
                Block(
                    typeof( string ),
                    Await( AsyncHelper.Completer( Constant( completer ), Constant( 1 ) ) ),
                    Throw( Constant( new InvalidOperationException( "outer" ) ) ),
                    Constant( string.Empty )
                ),
                Catch( outerEx,
                    TryCatch(
                        TryCatch(
                            Block(
                                typeof( string ),
                                Throw( Constant( new ArgumentException( "inner" ) ) ),
                                Constant( string.Empty )
                            ),
                            Catch( innerEx, Rethrow( typeof( string ) ) )
                        ),
                        Catch( typeof( ArgumentException ), Constant( "inner-rethrown" ) )
                    ) )
            )
        );

        var lambda = Lambda<Func<Task<string>>>( block );
        var compiledLambda = lambda.Compile( compiler );

        // Act
        var result = await compiledLambda();

        // Assert: the inner rethrow carried the inner exception, not the outer one
        Assert.AreEqual( "inner-rethrown", result );
    }

    [TestMethod]
    [DataRow( CompleterType.Immediate, CompilerType.Fast )]
    [DataRow( CompleterType.Immediate, CompilerType.System )]
    [DataRow( CompleterType.Immediate, CompilerType.Interpret )]
    [DataRow( CompleterType.Deferred, CompilerType.Fast )]
    [DataRow( CompleterType.Deferred, CompilerType.System )]
    [DataRow( CompleterType.Deferred, CompilerType.Interpret )]
    public async Task AsyncBlock_ShouldRethrowOriginal_WithVoidBareRethrow( CompleterType completer, CompilerType compiler )
    {
        // Arrange: a void-typed rethrow takes the other branch of the rewrite
        var exceptionParam = Parameter( typeof( Exception ), "ex" );

        var block = BlockAsync(
            TryCatch(
                Block(
                    typeof( void ),
                    Await( AsyncHelper.Completer( Constant( completer ), Constant( 1 ) ) ),
                    Throw( Constant( new InvalidOperationException( "Boom" ) ) )
                ),
                Catch( exceptionParam, Rethrow( typeof( void ) ) )
            )
        );

        var lambda = Lambda<Func<Task>>( block );
        var compiledLambda = lambda.Compile( compiler );

        // Act
        var exception = await Assert.ThrowsExactlyAsync<InvalidOperationException>( async () => await compiledLambda() );

        // Assert
        Assert.AreEqual( "Boom", exception.Message );
    }

    [TestMethod]
    [DataRow( CompleterType.Immediate, CompilerType.Fast )]
    [DataRow( CompleterType.Immediate, CompilerType.System )]
    [DataRow( CompleterType.Immediate, CompilerType.Interpret )]
    [DataRow( CompleterType.Deferred, CompilerType.Fast )]
    [DataRow( CompleterType.Deferred, CompilerType.System )]
    [DataRow( CompleterType.Deferred, CompilerType.Interpret )]
    public async Task AsyncBlock_ShouldReturnTryValue_WithTryFinallyAsTail( CompleterType completer, CompilerType compiler )
    {
        // Arrange: the try/finally is the value of the block. A finally re-points the join
        // state, so the state the group actually falls through to needs the result too.
        var ranFinally = new StrongBox<bool>( false );

        var block = BlockAsync(
            TryFinally(
                Block(
                    typeof( int ),
                    Await( AsyncHelper.Completer( Constant( completer ), Constant( 1 ) ) ),
                    Constant( 5 )
                ),
                MarkRan( ranFinally )
            )
        );

        var lambda = Lambda<Func<Task<int>>>( block );
        var compiledLambda = lambda.Compile( compiler );

        // Act
        var result = await compiledLambda();

        // Assert
        Assert.AreEqual( 5, result );
        Assert.IsTrue( ranFinally.Value );
    }

    [TestMethod]
    [DataRow( CompleterType.Immediate, CompilerType.Fast )]
    [DataRow( CompleterType.Immediate, CompilerType.System )]
    [DataRow( CompleterType.Immediate, CompilerType.Interpret )]
    [DataRow( CompleterType.Deferred, CompilerType.Fast )]
    [DataRow( CompleterType.Deferred, CompilerType.System )]
    [DataRow( CompleterType.Deferred, CompilerType.Interpret )]
    public async Task AsyncBlock_ShouldReturnCatchValue_WithTryCatchFinallyAsTail( CompleterType completer, CompilerType compiler )
    {
        // Arrange: same shape, but the value comes from the handler
        var exceptionParam = Parameter( typeof( Exception ), "ex" );

        var ranFinally = new StrongBox<bool>( false );

        var block = BlockAsync(
            TryCatchFinally(
                Block(
                    typeof( int ),
                    Await( AsyncHelper.Completer( Constant( completer ), Constant( 1 ) ) ),
                    Throw( Constant( new InvalidOperationException( "Boom" ) ) ),
                    Constant( 0 )
                ),
                MarkRan( ranFinally ),
                Catch( exceptionParam, Constant( 42 ) )
            )
        );

        var lambda = Lambda<Func<Task<int>>>( block );
        var compiledLambda = lambda.Compile( compiler );

        // Act
        var result = await compiledLambda();

        // Assert
        Assert.AreEqual( 42, result );
        Assert.IsTrue( ranFinally.Value );
    }

    [TestMethod]
    [DataRow( CompleterType.Immediate, CompilerType.Fast )]
    [DataRow( CompleterType.Immediate, CompilerType.System )]
    [DataRow( CompleterType.Immediate, CompilerType.Interpret )]
    [DataRow( CompleterType.Deferred, CompilerType.Fast )]
    [DataRow( CompleterType.Deferred, CompilerType.System )]
    [DataRow( CompleterType.Deferred, CompilerType.Interpret )]
    public async Task AsyncBlock_ShouldRethrow_FromHandlerWithFilter( CompleterType completer, CompilerType compiler )
    {
        // Arrange: the filter reads the catch variable and the handler rethrows it. The
        // filter binds to the generated catch parameter and the handler to the hoisted
        // variable, so both have to resolve to the same exception.
        var exceptionParam = Parameter( typeof( Exception ), "ex" );

        var block = BlockAsync(
            TryCatch(
                Block(
                    typeof( int ),
                    Await( AsyncHelper.Completer( Constant( completer ), Constant( 1 ) ) ),
                    Throw( Constant( new InvalidOperationException( "Boom" ) ) ),
                    Constant( 0 )
                ),
                MakeCatchBlock(
                    typeof( Exception ),
                    exceptionParam,
                    Rethrow( typeof( int ) ),
                    Equal( Property( exceptionParam, nameof( Exception.Message ) ), Constant( "Boom" ) ) )
            )
        );

        var lambda = Lambda<Func<Task<int>>>( block );
        var compiledLambda = lambda.Compile( compiler );

        // Act
        var exception = await Assert.ThrowsExactlyAsync<InvalidOperationException>( async () => await compiledLambda() );

        // Assert
        Assert.AreEqual( "Boom", exception.Message );
    }

    [TestMethod]
    [DataRow( CompleterType.Immediate, CompilerType.Fast )]
    [DataRow( CompleterType.Immediate, CompilerType.System )]
    [DataRow( CompleterType.Immediate, CompilerType.Interpret )]
    [DataRow( CompleterType.Deferred, CompilerType.Fast )]
    [DataRow( CompleterType.Deferred, CompilerType.System )]
    [DataRow( CompleterType.Deferred, CompilerType.Interpret )]
    public async Task AsyncBlock_ShouldRethrow_FromSecondHandler_WhenFirstDeclines( CompleterType completer, CompilerType compiler )
    {
        // Arrange: the rethrow is in the second handler, so it has to resolve against that
        // handler's own hoisted variable rather than the first one's.
        var first = Parameter( typeof( Exception ), "first" );
        var second = Parameter( typeof( Exception ), "second" );

        var block = BlockAsync(
            TryCatch(
                Block(
                    typeof( int ),
                    Await( AsyncHelper.Completer( Constant( completer ), Constant( 1 ) ) ),
                    Throw( Constant( new InvalidOperationException( "Boom" ) ) ),
                    Constant( 0 )
                ),
                MakeCatchBlock( typeof( Exception ), first, Constant( 1 ), Constant( false ) ),
                MakeCatchBlock( typeof( Exception ), second, Rethrow( typeof( int ) ), Constant( true ) )
            )
        );

        var lambda = Lambda<Func<Task<int>>>( block );
        var compiledLambda = lambda.Compile( compiler );

        // Act
        var exception = await Assert.ThrowsExactlyAsync<InvalidOperationException>( async () => await compiledLambda() );

        // Assert
        Assert.AreEqual( "Boom", exception.Message );
    }

    [TestMethod]
    [DataRow( CompleterType.Immediate, CompilerType.Fast )]
    [DataRow( CompleterType.Immediate, CompilerType.System )]
    [DataRow( CompleterType.Immediate, CompilerType.Interpret )]
    [DataRow( CompleterType.Deferred, CompilerType.Fast )]
    [DataRow( CompleterType.Deferred, CompilerType.System )]
    [DataRow( CompleterType.Deferred, CompilerType.Interpret )]
    public async Task AsyncBlock_ShouldRunAwaitingFinally_WhenHandlerBareRethrows( CompleterType completer, CompilerType compiler )
    {
        // Arrange: the finally suspends, so the rethrown exception has to survive across
        // the states the finally is split into before it is re-raised.
        var exceptionParam = Parameter( typeof( Exception ), "ex" );

        var ranFinally = new StrongBox<bool>( false );

        var block = BlockAsync(
            TryCatchFinally(
                Block(
                    typeof( int ),
                    Await( AsyncHelper.Completer( Constant( completer ), Constant( 1 ) ) ),
                    Throw( Constant( new InvalidOperationException( "Boom" ) ) ),
                    Constant( 0 )
                ),
                Block(
                    typeof( void ),
                    Await( AsyncHelper.Completer( Constant( completer ), Constant( 1 ) ) ),
                    MarkRan( ranFinally )
                ),
                Catch( exceptionParam, Rethrow( typeof( int ) ) )
            )
        );

        var lambda = Lambda<Func<Task<int>>>( block );
        var compiledLambda = lambda.Compile( compiler );

        // Act
        var exception = await Assert.ThrowsExactlyAsync<InvalidOperationException>( async () => await compiledLambda() );

        // Assert
        Assert.AreEqual( "Boom", exception.Message );
        Assert.IsTrue( ranFinally.Value );
    }

    [TestMethod]
    [DataRow( CompleterType.Immediate, CompilerType.Fast )]
    [DataRow( CompleterType.Immediate, CompilerType.System )]
    [DataRow( CompleterType.Immediate, CompilerType.Interpret )]
    [DataRow( CompleterType.Deferred, CompilerType.Fast )]
    [DataRow( CompleterType.Deferred, CompilerType.System )]
    [DataRow( CompleterType.Deferred, CompilerType.Interpret )]
    public async Task AsyncBlock_ShouldRethrow_FromTryCatchInsideLoop( CompleterType completer, CompilerType compiler )
    {
        // Arrange: the try is re-entered each iteration, so the region's dispatch state is
        // reset between passes. The rethrow has to leave the loop on the failing pass.
        var exceptionParam = Parameter( typeof( Exception ), "ex" );

        var iterations = new StrongBox<int>( 0 );
        var counter = Field( Constant( iterations ), nameof( StrongBox<int>.Value ) );

        var breakLabel = Label( "break" );

        var block = BlockAsync(
            Loop(
                TryCatch(
                    Block(
                        typeof( void ),
                        Assign( counter, Add( counter, Constant( 1 ) ) ),
                        Await( AsyncHelper.Completer( Constant( completer ), Constant( 1 ) ) ),
                        IfThen(
                            GreaterThanOrEqual( counter, Constant( 2 ) ),
                            Throw( Constant( new InvalidOperationException( "Boom" ) ) ) )
                    ),
                    Catch( exceptionParam, Rethrow( typeof( void ) ) )
                ),
                breakLabel )
        );

        var lambda = Lambda<Func<Task>>( block );
        var compiledLambda = lambda.Compile( compiler );

        // Act
        var exception = await Assert.ThrowsExactlyAsync<InvalidOperationException>( async () => await compiledLambda() );

        // Assert: it looped once cleanly, then rethrew out of the second pass
        Assert.AreEqual( "Boom", exception.Message );
        Assert.AreEqual( 2, iterations.Value );
    }
}
