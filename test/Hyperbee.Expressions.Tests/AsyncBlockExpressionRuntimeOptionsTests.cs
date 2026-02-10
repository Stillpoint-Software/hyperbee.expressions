using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;
using Hyperbee.Expressions.Tests.TestSupport;
using static System.Linq.Expressions.Expression;
using static Hyperbee.Expressions.ExpressionExtensions;

namespace Hyperbee.Expressions.Tests;

[TestClass]
public class AsyncBlockExpressionRuntimeOptionsTests
{
    [TestMethod]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Interpret )]
    public async Task BlockAsync_ShouldUseDefaultProvider_WhenNoOptionsProvided( CompilerType compiler )
    {
        // Arrange
        var block = BlockAsync(
            Await( AsyncHelper.Completer(
                Constant( CompleterType.Immediate ),
                Constant( 42 )
            ) )
        );

        var lambda = Lambda<Func<Task<int>>>( block );
        var compiledLambda = lambda.Compile( compiler );

        // Act
        var result = await compiledLambda();

        // Assert
        Assert.AreEqual( 42, result );
    }

    [TestMethod]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Interpret )]
    public async Task BlockAsync_ShouldUseCollectibleProvider_WhenProvidedInOptions( CompilerType compiler )
    {
        // Arrange
        var options = new ExpressionRuntimeOptions
        {
            ModuleBuilderProvider = new CollectibleModuleBuilderProvider()
        };

        var block = BlockAsync(
            new[]
            {
                Await( AsyncHelper.Completer(
                    Constant( CompleterType.Immediate ),
                    Constant( 100 )
                ) )
            },
            options
        );

        var lambda = Lambda<Func<Task<int>>>( block );
        var compiledLambda = lambda.Compile( compiler );

        // Act
        var result = await compiledLambda();

        // Assert
        Assert.AreEqual( 100, result );
    }

    [TestMethod]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Interpret )]
    public async Task BlockAsync_ShouldUseCustomProvider_WhenProvidedInOptions( CompilerType compiler )
    {
        // Arrange
        var customProvider = new CustomTestModuleBuilderProvider();
        var options = new ExpressionRuntimeOptions { ModuleBuilderProvider = customProvider };

        var block = BlockAsync(
            new[]
            {
                Await( AsyncHelper.Completer(
                    Constant( CompleterType.Immediate ),
                    Constant( 200 )
                ) )
            },
            options
        );

        var lambda = Lambda<Func<Task<int>>>( block );
        var compiledLambda = lambda.Compile( compiler );

        // Act
        var result = await compiledLambda();

        // Assert
        Assert.AreEqual( 200, result );
        Assert.IsTrue( customProvider.WasCalled );
    }

    [TestMethod]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Interpret )]
    public async Task BlockAsync_WithVariables_ShouldUseCustomProvider( CompilerType compiler )
    {
        // Arrange
        var options = new ExpressionRuntimeOptions
        {
            ModuleBuilderProvider = new CollectibleModuleBuilderProvider()
        };

        var result1 = Variable( typeof( int ), "result1" );
        var result2 = Variable( typeof( int ), "result2" );

        var block = BlockAsync(
            new[] { result1, result2 },
            new Expression[]
            {
                Assign( result1, Await( AsyncHelper.Completer(
                    Constant( CompleterType.Immediate ),
                    Constant( 10 )
                ) ) ),
                Assign( result2, Await( AsyncHelper.Completer(
                    Constant( CompleterType.Immediate ),
                    Constant( 20 )
                ) ) ),
                Add( result1, result2 )
            },
            options
        );

        var lambda = Lambda<Func<Task<int>>>( block );
        var compiledLambda = lambda.Compile( compiler );

        // Act
        var result = await compiledLambda();

        // Assert
        Assert.AreEqual( 30, result );
    }

    [TestMethod]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Interpret )]
    public async Task BlockAsync_MultipleCalls_ShouldUseSameModuleBuilder( CompilerType compiler )
    {
        // Arrange
        var trackingProvider = new TrackingModuleBuilderProvider();
        var options = new ExpressionRuntimeOptions { ModuleBuilderProvider = trackingProvider };

        var block1 = BlockAsync(
            new[] { Await( AsyncHelper.Completer( Constant( CompleterType.Immediate ), Constant( 1 ) ) ) },
            options
        );

        var block2 = BlockAsync(
            new[] { Await( AsyncHelper.Completer( Constant( CompleterType.Immediate ), Constant( 2 ) ) ) },
            options
        );

        var lambda1 = Lambda<Func<Task<int>>>( block1 );
        var lambda2 = Lambda<Func<Task<int>>>( block2 );
        var compiledLambda1 = lambda1.Compile( compiler );
        var compiledLambda2 = lambda2.Compile( compiler );

        // Act
        var result1 = await compiledLambda1();
        var result2 = await compiledLambda2();

        // Assert
        Assert.AreEqual( 1, result1 );
        Assert.AreEqual( 2, result2 );
        // Provider should be called at least once per block (might be called during Reduce)
        Assert.IsTrue( trackingProvider.AsyncCallCount >= 2,
            $"Expected at least 2 calls, but got {trackingProvider.AsyncCallCount}" );
        // Verify the same ModuleBuilder instance is reused
        Assert.AreSame( trackingProvider.GetModuleBuilder( ModuleKind.Async ),
            trackingProvider.GetModuleBuilder( ModuleKind.Async ) );
    }

    // Helper: Custom test provider that tracks usage
    private class CustomTestModuleBuilderProvider : IModuleBuilderProvider
    {
        private readonly Lazy<ModuleBuilder> _sharedModule = new( () =>
        {
            var assemblyName = new AssemblyName( "CustomTestAssembly" );
            var assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly( assemblyName, AssemblyBuilderAccess.Run );
            return assemblyBuilder.DefineDynamicModule( "CustomTestModule" );
        } );

        public bool WasCalled { get; private set; }

        public ModuleBuilder GetModuleBuilder( ModuleKind kind )
        {
            WasCalled = true;
            return _sharedModule.Value;
        }
    }

    // Helper: Tracking provider that counts calls
    private class TrackingModuleBuilderProvider : IModuleBuilderProvider
    {
        private readonly Lazy<ModuleBuilder> _asyncModule = new( () =>
        {
            var assemblyName = new AssemblyName( "TrackingAsyncAssembly" );
            var assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly( assemblyName, AssemblyBuilderAccess.Run );
            return assemblyBuilder.DefineDynamicModule( "TrackingAsyncModule" );
        } );

        private readonly Lazy<ModuleBuilder> _enumerableModule = new( () =>
        {
            var assemblyName = new AssemblyName( "TrackingEnumerableAssembly" );
            var assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly( assemblyName, AssemblyBuilderAccess.Run );
            return assemblyBuilder.DefineDynamicModule( "TrackingEnumerableModule" );
        } );

        public int AsyncCallCount { get; private set; }
        public int EnumerableCallCount { get; private set; }

        public ModuleBuilder GetModuleBuilder( ModuleKind kind )
        {
            return kind switch
            {
                ModuleKind.Async => GetAsyncModule(),
                ModuleKind.Enumerable => GetEnumerableModule(),
                _ => throw new ArgumentOutOfRangeException( nameof( kind ) )
            };
        }

        private ModuleBuilder GetAsyncModule()
        {
            AsyncCallCount++;
            return _asyncModule.Value;
        }

        private ModuleBuilder GetEnumerableModule()
        {
            EnumerableCallCount++;
            return _enumerableModule.Value;
        }
    }
}
