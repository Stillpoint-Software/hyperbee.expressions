using System.Linq.Expressions;
using Hyperbee.Expressions.Compiler.Tests.TestSupport;
using Hyperbee.Expressions.CompilerServices;
using static System.Linq.Expressions.Expression;
using static Hyperbee.Expressions.ExpressionExtensions;

namespace Hyperbee.Expressions.Compiler.Tests.Expressions;

/// <summary>
/// Integration tests verifying that BlockAsync works end-to-end when
/// the async state machine MoveNext lambda is compiled by HEC
/// (via <see cref="HyperbeeCoroutineDelegateBuilder"/>).
/// </summary>
[TestClass]
public class BlockAsyncHecTests
{
    private static ExpressionRuntimeOptions HecOptions() => new()
    {
        DelegateBuilder = HyperbeeCoroutineDelegateBuilder.Instance
    };

    // -----------------------------------------------------------------------
    // Single await — simplest case
    // -----------------------------------------------------------------------

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public async Task BlockAsync_SingleAwait_HEC_ReturnsResult( CompilerType compiler )
    {
        // Arrange
        var block = BlockAsync(
            new Expression[] { Await( Call( typeof( Task ), nameof( Task.FromResult ), [typeof( int )], Constant( 42 ) ) ) },
            HecOptions()
        );

        var lambda = Lambda<Func<Task<int>>>( block );
        var compiled = lambda.Compile( compiler );

        // Act
        var result = await compiled();

        // Assert
        Assert.AreEqual( 42, result );
    }

    // -----------------------------------------------------------------------
    // Sequential awaits
    // -----------------------------------------------------------------------

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public async Task BlockAsync_SequentialAwaits_HEC_ReturnsSum( CompilerType compiler )
    {
        // Arrange
        var a = Variable( typeof( int ), "a" );
        var b = Variable( typeof( int ), "b" );

        var block = BlockAsync(
            new[] { a, b },
            new Expression[]
            {
                Assign( a, Await( Call( typeof( Task ), nameof( Task.FromResult ), [typeof( int )], Constant( 10 ) ) ) ),
                Assign( b, Await( Call( typeof( Task ), nameof( Task.FromResult ), [typeof( int )], Constant( 20 ) ) ) ),
                Add( a, b )
            },
            HecOptions()
        );

        var lambda = Lambda<Func<Task<int>>>( block );
        var compiled = lambda.Compile( compiler );

        // Act
        var result = await compiled();

        // Assert
        Assert.AreEqual( 30, result );
    }

    // -----------------------------------------------------------------------
    // Conditional await — await in IfThenElse true-branch
    // -----------------------------------------------------------------------

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public async Task BlockAsync_ConditionalAwait_HEC_TrueBranch( CompilerType compiler )
    {
        // Arrange
        var result = Variable( typeof( int ), "result" );

        var block = BlockAsync(
            new[] { result },
            new Expression[]
            {
                IfThenElse(
                    Constant( true ),
                    Assign( result, Await( Call( typeof( Task ), nameof( Task.FromResult ), [typeof( int )], Constant( 1 ) ) ) ),
                    Assign( result, Await( Call( typeof( Task ), nameof( Task.FromResult ), [typeof( int )], Constant( 2 ) ) ) )
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
        Assert.AreEqual( 1, value );
    }

    // -----------------------------------------------------------------------
    // Try/catch with await
    // -----------------------------------------------------------------------

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public async Task BlockAsync_TryCatchWithAwait_HEC_NoException( CompilerType compiler )
    {
        // Arrange
        var result = Variable( typeof( int ), "result" );
        var ex = Parameter( typeof( Exception ), "ex" );

        var block = BlockAsync(
            new[] { result },
            new Expression[]
            {
                TryCatch(
                    Assign( result, Await( Call( typeof( Task ), nameof( Task.FromResult ), [typeof( int )], Constant( 99 ) ) ) ),
                    Catch( ex, Assign( result, Constant( -1 ) ) )
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
    // Void async block
    // -----------------------------------------------------------------------

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public async Task BlockAsync_VoidResult_HEC_CompletesWithoutError( CompilerType compiler )
    {
        // Arrange
        var block = BlockAsync(
            new Expression[] { Await( Call( typeof( Task ), nameof( Task.FromResult ), [typeof( int )], Constant( 0 ) ) ) },
            HecOptions()
        );

        // Void Task
        var lambda = Lambda<Func<Task>>( block );
        var compiled = lambda.Compile( compiler );

        // Act & Assert — should complete without throwing
        await compiled();
    }

    // -----------------------------------------------------------------------
    // Diagnostics: IRCapture fires
    // -----------------------------------------------------------------------

    [TestMethod]
    public async Task BlockAsync_HEC_IRCapture_Fires()
    {
        // Arrange
        string? captured = null;

        var options = new ExpressionRuntimeOptions
        {
            DelegateBuilder = new DiagnosticsCoroutineDelegateBuilder( diag => captured = diag )
        };

        var block = BlockAsync(
            new Expression[] { Await( Call( typeof( Task ), nameof( Task.FromResult ), [typeof( int )], Constant( 7 ) ) ) },
            options
        );

        var lambda = Lambda<Func<Task<int>>>( block );
        var compiled = lambda.Compile( CompilerType.System );

        // Act
        var result = await compiled();

        // Assert
        Assert.AreEqual( 7, result );
        Assert.IsNotNull( captured, "IRCapture should have been invoked" );
        Assert.IsTrue( captured.Length > 0, "IR listing should not be empty" );
    }

    /// <summary>
    /// A delegate builder that captures the IR listing from HEC via <see cref="CompilerDiagnostics"/>.
    /// </summary>
    private sealed class DiagnosticsCoroutineDelegateBuilder( Action<string> irCapture ) : ICoroutineDelegateBuilder
    {
        public Delegate Create( LambdaExpression lambda )
        {
            return HyperbeeCompiler.Compile(
                lambda,
                new Hyperbee.Expressions.Compiler.Diagnostics.CompilerDiagnostics { IRCapture = irCapture }
            );
        }
    }
}
