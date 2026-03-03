using System.Linq.Expressions;
using Hyperbee.Expressions.Compiler.Tests.TestSupport;
using Hyperbee.Expressions.CompilerServices;
using static System.Linq.Expressions.Expression;
using static Hyperbee.Expressions.ExpressionExtensions;

namespace Hyperbee.Expressions.Compiler.Tests.Integration;

/// <summary>
/// Integration tests verifying try/catch/finally patterns inside BlockAsync
/// when the state machine MoveNext is compiled by HEC.
/// </summary>
[TestClass]
public class BlockAsyncTryCatchTests
{
    private static ExpressionRuntimeOptions HecOptions() => new()
    {
        DelegateBuilder = HyperbeeCoroutineDelegateBuilder.Instance
    };

    // -----------------------------------------------------------------------
    // Await in try block, no exception thrown
    // -----------------------------------------------------------------------

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public async Task BlockAsync_TryCatch_AwaitInTryBlock_NoException( CompilerType compiler )
    {
        // Arrange
        var result = Variable( typeof( int ), "result" );
        var ex = Parameter( typeof( Exception ), "ex" );

        var block = BlockAsync(
            new[] { result },
            new Expression[]
            {
                TryCatch(
                    Assign( result, Await( Call( typeof( Task ), nameof( Task.FromResult ), [typeof( int )], Constant( 10 ) ) ) ),
                    Catch( ex, Assign( result, Constant( 0 ) ) )
                ),
                result
            },
            HecOptions()
        );

        var lambda = Lambda<Func<Task<int>>>( block );
        var compiled = lambda.Compile( compiler );

        // Act
        var value = await compiled();

        // Assert
        Assert.AreEqual( 10, value );
    }

    // -----------------------------------------------------------------------
    // Exception thrown — await in catch block handles it
    // -----------------------------------------------------------------------

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public async Task BlockAsync_TryCatch_AwaitInCatchBlock_HandlesException( CompilerType compiler )
    {
        // Arrange
        var result = Variable( typeof( int ), "result" );
        var ex = Parameter( typeof( Exception ), "ex" );

        var block = BlockAsync(
            new[] { result },
            new Expression[]
            {
                TryCatch(
                    Block(
                        Throw( Constant( new Exception( "test" ) ) ),
                        Constant( 1 )
                    ),
                    Catch(
                        ex,
                        Assign( result, Await( Call( typeof( Task ), nameof( Task.FromResult ), [typeof( int )], Constant( 99 ) ) ) )
                    )
                ),
                result
            },
            HecOptions()
        );

        var lambda = Lambda<Func<Task<int>>>( block );
        var compiled = lambda.Compile( compiler );

        // Act
        var value = await compiled();

        // Assert
        Assert.AreEqual( 99, value );
    }

    // -----------------------------------------------------------------------
    // TryFinally: await in try block, finally overwrites result
    // -----------------------------------------------------------------------

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public async Task BlockAsync_TryFinally_AwaitInTry_FinallyOverwritesResult( CompilerType compiler )
    {
        // Arrange
        var result = Variable( typeof( int ), "result" );

        var block = BlockAsync(
            new[] { result },
            new Expression[]
            {
                TryFinally(
                    Assign( result, Await( Call( typeof( Task ), nameof( Task.FromResult ), [typeof( int )], Constant( 15 ) ) ) ),
                    Assign( result, Constant( 25 ) )
                ),
                result
            },
            HecOptions()
        );

        var lambda = Lambda<Func<Task<int>>>( block );
        var compiled = lambda.Compile( compiler );

        // Act
        var value = await compiled();

        // Assert
        Assert.AreEqual( 25, value );
    }

    // -----------------------------------------------------------------------
    // TryCatchFinally: await in try, catch, and finally — finally wins
    // -----------------------------------------------------------------------

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public async Task BlockAsync_TryCatchFinally_AllAwaited_FinallyWins( CompilerType compiler )
    {
        // Arrange
        var result = Variable( typeof( int ), "result" );
        var ex = Parameter( typeof( Exception ), "ex" );

        var block = BlockAsync(
            new[] { result },
            new Expression[]
            {
                TryCatchFinally(
                    Assign( result, Await( Call( typeof( Task ), nameof( Task.FromResult ), [typeof( int )], Constant( 10 ) ) ) ),
                    Assign( result, Await( Call( typeof( Task ), nameof( Task.FromResult ), [typeof( int )], Constant( 30 ) ) ) ),
                    Catch( ex,
                        Assign( result, Await( Call( typeof( Task ), nameof( Task.FromResult ), [typeof( int )], Constant( 20 ) ) ) )
                    )
                ),
                result
            },
            HecOptions()
        );

        var lambda = Lambda<Func<Task<int>>>( block );
        var compiled = lambda.Compile( compiler );

        // Act
        var value = await compiled();

        // Assert — finally always executes and sets result = 30
        Assert.AreEqual( 30, value );
    }

    // -----------------------------------------------------------------------
    // TryCatchFinally: exception thrown — catch handles, finally still runs
    // -----------------------------------------------------------------------

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public async Task BlockAsync_TryCatchFinally_ExceptionThrown_FinallyRuns( CompilerType compiler )
    {
        // Arrange
        var result = Variable( typeof( int ), "result" );
        var ex = Parameter( typeof( Exception ), "ex" );

        var block = BlockAsync(
            new[] { result },
            new Expression[]
            {
                TryCatchFinally(
                    Block(
                        Throw( Constant( new Exception() ) ),
                        Constant( 1 )
                    ),
                    Assign( result, Await( Call( typeof( Task ), nameof( Task.FromResult ), [typeof( int )], Constant( 50 ) ) ) ),
                    Catch( ex,
                        Assign( result, Await( Call( typeof( Task ), nameof( Task.FromResult ), [typeof( int )], Constant( 30 ) ) ) )
                    )
                ),
                result
            },
            HecOptions()
        );

        var lambda = Lambda<Func<Task<int>>>( block );
        var compiled = lambda.Compile( compiler );

        // Act
        var value = await compiled();

        // Assert — catch sets 30, finally overwrites with 50
        Assert.AreEqual( 50, value );
    }

    // -----------------------------------------------------------------------
    // Await after unreachable throw (dead code after throw)
    // -----------------------------------------------------------------------

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public async Task BlockAsync_TryCatch_AwaitAfterThrow_CatchHandles( CompilerType compiler )
    {
        // Arrange
        var result = Variable( typeof( int ), "result" );
        var ex = Parameter( typeof( Exception ), "ex" );

        var block = BlockAsync(
            new[] { result },
            new Expression[]
            {
                TryCatch(
                    Block(
                        Assign( result, Constant( 10 ) ),
                        Throw( Constant( new Exception( "expected" ) ) ),
                        // unreachable await
                        Await( Call( typeof( Task ), nameof( Task.FromResult ), [typeof( int )], Constant( 20 ) ) )
                    ),
                    Catch( ex, Assign( result, Constant( 50 ) ) )
                ),
                result
            },
            HecOptions()
        );

        var lambda = Lambda<Func<Task<int>>>( block );
        var compiled = lambda.Compile( compiler );

        // Act
        var value = await compiled();

        // Assert
        Assert.AreEqual( 50, value );
    }

    // -----------------------------------------------------------------------
    // Nested try/catch — inner catch handles the exception
    // -----------------------------------------------------------------------

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public async Task BlockAsync_NestedTryCatch_InnerCatchHandles_ReturnsInnerCatchValue( CompilerType compiler )
    {
        // Arrange
        var result = Variable( typeof( int ), "result" );
        var outerEx = Parameter( typeof( Exception ), "outerEx" );
        var innerEx = Parameter( typeof( Exception ), "innerEx" );

        var block = BlockAsync(
            new[] { result },
            new Expression[]
            {
                TryCatch(
                    TryCatch(
                        Block(
                            Throw( Constant( new Exception( "inner" ) ) ),
                            Constant( 0 )
                        ),
                        Catch( innerEx,
                            Assign( result, Await( Call( typeof( Task ), nameof( Task.FromResult ), [typeof( int )], Constant( 77 ) ) ) )
                        )
                    ),
                    Catch( outerEx, Assign( result, Constant( 0 ) ) )
                ),
                result
            },
            HecOptions()
        );

        var lambda = Lambda<Func<Task<int>>>( block );
        var compiled = lambda.Compile( compiler );

        // Act
        var value = await compiled();

        // Assert — inner catch handles exception and awaits 77
        Assert.AreEqual( 77, value );
    }

    // -----------------------------------------------------------------------
    // Nested try/catch — inner throws again, outer catch handles
    // -----------------------------------------------------------------------

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public async Task BlockAsync_NestedTryCatch_OuterCatchHandles_ReturnsOuterCatchValue( CompilerType compiler )
    {
        // Arrange
        var result = Variable( typeof( int ), "result" );
        var outerEx = Parameter( typeof( Exception ), "outerEx" );
        var innerEx = Parameter( typeof( Exception ), "innerEx" );

        var block = BlockAsync(
            new[] { result },
            new Expression[]
            {
                TryCatch(
                    Block(
                        TryCatch(
                            Block(
                                Throw( Constant( new Exception( "inner" ) ) ),
                                Constant( 0 )
                            ),
                            Catch( innerEx,
                                Assign( result, Await( Call( typeof( Task ), nameof( Task.FromResult ), [typeof( int )], Constant( 20 ) ) ) )
                            )
                        ),
                        Throw( Constant( new Exception( "outer" ) ) ),
                        Constant( 0 )
                    ),
                    Catch( outerEx, Assign( result, Constant( 50 ) ) )
                ),
                result
            },
            HecOptions()
        );

        var lambda = Lambda<Func<Task<int>>>( block );
        var compiled = lambda.Compile( compiler );

        // Act
        var value = await compiled();

        // Assert — outer catch handles rethrown exception
        Assert.AreEqual( 50, value );
    }

    // -----------------------------------------------------------------------
    // Complex nested try/catch — multiple levels, deepest await wins
    // -----------------------------------------------------------------------

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public async Task BlockAsync_ComplexNestedTryCatch_ReturnsDeepestAwait( CompilerType compiler )
    {
        // Arrange — three nesting levels, no exceptions thrown
        var result = Variable( typeof( int ), "result" );

        var block = BlockAsync(
            new[] { result },
            new Expression[]
            {
                Await( Call( typeof( Task ), nameof( Task.FromResult ), [typeof( int )], Constant( 0 ) ) ),
                TryCatch(
                    Block(
                        Assign( result, Await( Call( typeof( Task ), nameof( Task.FromResult ), [typeof( int )], Constant( 10 ) ) ) ),
                        TryCatch(
                            Block(
                                Assign( result, Await( Call( typeof( Task ), nameof( Task.FromResult ), [typeof( int )], Constant( 20 ) ) ) ),
                                TryCatch(
                                    Assign( result, Await( Call( typeof( Task ), nameof( Task.FromResult ), [typeof( int )], Constant( 30 ) ) ) ),
                                    Catch( typeof( Exception ), Assign( result, Constant( 1 ) ) )
                                )
                            ),
                            Catch( typeof( Exception ), Assign( result, Constant( 2 ) ) )
                        )
                    ),
                    Catch( typeof( Exception ), Assign( result, Constant( 6 ) ) )
                ),
                result
            },
            HecOptions()
        );

        var lambda = Lambda<Func<Task<int>>>( block );
        var compiled = lambda.Compile( compiler );

        // Act
        var value = await compiled();

        // Assert — no exceptions, deepest await sets result to 30
        Assert.AreEqual( 30, value );
    }
}
