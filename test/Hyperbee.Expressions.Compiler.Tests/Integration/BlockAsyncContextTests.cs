using System.Linq.Expressions;
using Hyperbee.Expressions.Compiler.Tests.TestSupport;
using Hyperbee.Expressions.CompilerServices;
using static System.Linq.Expressions.Expression;
using static Hyperbee.Expressions.ExpressionExtensions;

namespace Hyperbee.Expressions.Compiler.Tests.Integration;

/// <summary>
/// Integration tests verifying that <see cref="CoroutineBuilderContext"/> correctly routes
/// MoveNext compilation to HEC without explicit <see cref="ExpressionRuntimeOptions"/>.
///
/// Two mechanisms are tested:
/// <list type="bullet">
///   <item>
///     <description>
///       <b>Global default</b>: <see cref="HyperbeeCompiler.UseAsDefault"/> sets HEC as the
///       process-wide default. When <c>BlockAsync</c> is called without options and the outer
///       compiler is System (SEC), <see cref="CoroutineBuilderContext.Current"/> returns the
///       global default (HEC) for MoveNext compilation.
///     </description>
///   </item>
///   <item>
///     <description>
///       <b>Per-compilation ambient</b>: All <c>HyperbeeCompiler</c> public entry points set
///       HEC as the per-compilation ambient via <see cref="CoroutineBuilderContext.Exchange"/>
///       in a save/restore pattern. Any <c>BlockAsync</c> reduction that occurs during the
///       compilation automatically picks up HEC.
///     </description>
///   </item>
/// </list>
/// </summary>
[TestClass]
public class BlockAsyncContextTests
{
    private static ICoroutineDelegateBuilder? _savedDefault;

    [ClassInitialize]
    public static void ClassInitialize( TestContext _ )
    {
        // Set HEC as the process-wide default — returned value is saved for cleanup.
        _savedDefault = HyperbeeCompiler.UseAsDefault();
    }

    [ClassCleanup]
    public static void ClassCleanup()
    {
        // Restore the previous default so other test classes are not affected.
        HyperbeeCompiler.ClearDefault();
    }

    // -----------------------------------------------------------------------
    // Global default (CompilerType.System outer — no ambient, relies on _default)
    // -----------------------------------------------------------------------

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public async Task BlockAsync_NoOptions_SingleAwait_ReturnsResult( CompilerType compiler )
    {
        // Arrange — no HecOptions() passed to BlockAsync
        var block = BlockAsync(
            Await( Call( typeof( Task ), nameof( Task.FromResult ), [typeof( int )], Constant( 42 ) ) )
        );

        var lambda = Lambda<Func<Task<int>>>( block );
        var compiled = lambda.Compile( compiler );

        // Act
        var result = await compiled();

        // Assert
        Assert.AreEqual( 42, result );
    }

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public async Task BlockAsync_NoOptions_SequentialAwaits_ReturnsSum( CompilerType compiler )
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
            }
        );

        var lambda = Lambda<Func<Task<int>>>( block );
        var compiled = lambda.Compile( compiler );

        // Act
        var result = await compiled();

        // Assert
        Assert.AreEqual( 30, result );
    }

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public async Task BlockAsync_NoOptions_ConditionalAwait_TrueBranch( CompilerType compiler )
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
            }
        );

        var lambda = Lambda<Func<Task<int>>>( block );
        var compiled = lambda.Compile( compiler );

        // Act
        var value = await compiled();

        // Assert
        Assert.AreEqual( 1, value );
    }

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public async Task BlockAsync_NoOptions_TryCatchWithAwait_NoException( CompilerType compiler )
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
            }
        );

        var lambda = Lambda<Func<Task<int>>>( block );
        var compiled = lambda.Compile( compiler );

        // Act
        var value = await compiled();

        // Assert
        Assert.AreEqual( 99, value );
    }

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public async Task BlockAsync_NoOptions_VoidResult_CompletesWithoutError( CompilerType compiler )
    {
        // Arrange
        var block = BlockAsync(
            Await( Call( typeof( Task ), nameof( Task.FromResult ), [typeof( int )], Constant( 0 ) ) )
        );

        var lambda = Lambda<Func<Task>>( block );
        var compiled = lambda.Compile( compiler );

        // Act & Assert — should complete without throwing
        await compiled();
    }

    // -----------------------------------------------------------------------
    // Explicit ExpressionRuntimeOptions still overrides the ambient/default
    // -----------------------------------------------------------------------

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public async Task BlockAsync_ExplicitRuntimeOptions_AreRespected( CompilerType compiler )
    {
        // Arrange — behavioral ExpressionRuntimeOptions are passed through; no DelegateBuilder needed.
        var options = new ExpressionRuntimeOptions { Optimize = true };

        var block = BlockAsync(
            new Expression[] { Await( Call( typeof( Task ), nameof( Task.FromResult ), [typeof( int )], Constant( 7 ) ) ) },
            options
        );

        var lambda = Lambda<Func<Task<int>>>( block );
        var compiled = lambda.Compile( compiler );

        // Act
        var result = await compiled();

        // Assert
        Assert.AreEqual( 7, result );
    }
}
