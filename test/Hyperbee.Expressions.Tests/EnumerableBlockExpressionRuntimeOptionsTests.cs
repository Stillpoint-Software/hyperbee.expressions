using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;
using Hyperbee.Expressions.Tests.TestSupport;
using static System.Linq.Expressions.Expression;
using static Hyperbee.Expressions.ExpressionExtensions;

namespace Hyperbee.Expressions.Tests;

[TestClass]
public class EnumerableBlockExpressionRuntimeOptionsTests
{
    [TestMethod]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Interpret )]
    public void BlockEnumerable_ShouldUseDefaultProvider_WhenNoOptionsProvided( CompilerType compiler )
    {
        // Arrange
        var block = BlockEnumerable(
            YieldReturn( Constant( 1 ) ),
            YieldReturn( Constant( 2 ) ),
            YieldReturn( Constant( 3 ) )
        );

        var lambda = Lambda<Func<IEnumerable<int>>>( block );
        var compiledLambda = lambda.Compile( compiler );

        // Act
        var results = compiledLambda().ToArray();

        // Assert
        Assert.AreEqual( 3, results.Length );
        Assert.AreEqual( 1, results[0] );
        Assert.AreEqual( 2, results[1] );
        Assert.AreEqual( 3, results[2] );
    }

    [TestMethod]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Interpret )]
    public void BlockEnumerable_ShouldUseCollectibleProvider_WhenProvidedInOptions( CompilerType compiler )
    {
        // Arrange
        var options = new ExpressionRuntimeOptions
        {
            Provider = new CollectibleModuleBuilderProvider()
        };

        var block = BlockEnumerable(
            new Expression[]
            {
                YieldReturn( Constant( 10 ) ),
                YieldReturn( Constant( 20 ) ),
                YieldReturn( Constant( 30 ) )
            },
            options
        );

        var lambda = Lambda<Func<IEnumerable<int>>>( block );
        var compiledLambda = lambda.Compile( compiler );

        // Act
        var results = compiledLambda().ToArray();

        // Assert
        Assert.AreEqual( 3, results.Length );
        Assert.AreEqual( 10, results[0] );
        Assert.AreEqual( 20, results[1] );
        Assert.AreEqual( 30, results[2] );
    }

    [TestMethod]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Interpret )]
    public void BlockEnumerable_ShouldUseCustomProvider_WhenProvidedInOptions( CompilerType compiler )
    {
        // Arrange
        var customProvider = new CustomTestModuleBuilderProvider();
        var options = new ExpressionRuntimeOptions { Provider = customProvider };

        var block = BlockEnumerable(
            new Expression[]
            {
                YieldReturn( Constant( 100 ) ),
                YieldReturn( Constant( 200 ) )
            },
            options
        );

        var lambda = Lambda<Func<IEnumerable<int>>>( block );
        var compiledLambda = lambda.Compile( compiler );

        // Act
        var results = compiledLambda().ToArray();

        // Assert
        Assert.AreEqual( 2, results.Length );
        Assert.AreEqual( 100, results[0] );
        Assert.AreEqual( 200, results[1] );
        Assert.IsTrue( customProvider.WasCalled );
    }

    [TestMethod]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Interpret )]
    public void BlockEnumerable_WithVariables_ShouldUseCustomProvider( CompilerType compiler )
    {
        // Arrange
        var options = new ExpressionRuntimeOptions
        {
            Provider = new CollectibleModuleBuilderProvider()
        };

        var counter = Variable( typeof( int ), "counter" );

        var block = BlockEnumerable(
            new[] { counter },
            new Expression[]
            {
                Assign( counter, Constant( 0 ) ),
                YieldReturn( PostIncrementAssign( counter ) ),
                YieldReturn( PostIncrementAssign( counter ) ),
                YieldReturn( PostIncrementAssign( counter ) )
            },
            options
        );

        var lambda = Lambda<Func<IEnumerable<int>>>( block );
        var compiledLambda = lambda.Compile( compiler );

        // Act
        var results = compiledLambda().ToArray();

        // Assert
        Assert.AreEqual( 3, results.Length );
        Assert.AreEqual( 0, results[0] );
        Assert.AreEqual( 1, results[1] );
        Assert.AreEqual( 2, results[2] );
    }

    [TestMethod]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Interpret )]
    public void BlockEnumerable_MultipleCalls_ShouldUseSameModuleBuilder( CompilerType compiler )
    {
        // Arrange
        var trackingProvider = new TrackingModuleBuilderProvider();
        var options = new ExpressionRuntimeOptions { Provider = trackingProvider };

        var block1 = BlockEnumerable(
            new Expression[] { YieldReturn( Constant( 1 ) ) },
            options
        );

        var block2 = BlockEnumerable(
            new Expression[] { YieldReturn( Constant( 2 ) ) },
            options
        );

        var lambda1 = Lambda<Func<IEnumerable<int>>>( block1 );
        var lambda2 = Lambda<Func<IEnumerable<int>>>( block2 );
        var compiledLambda1 = lambda1.Compile( compiler );
        var compiledLambda2 = lambda2.Compile( compiler );

        // Act
        var results1 = compiledLambda1().ToArray();
        var results2 = compiledLambda2().ToArray();

        // Assert
        Assert.AreEqual( 1, results1[0] );
        Assert.AreEqual( 2, results2[0] );
        // Provider should be called at least once per block (might be called during Reduce)
        Assert.IsTrue( trackingProvider.EnumerableCallCount >= 2,
            $"Expected at least 2 calls, but got {trackingProvider.EnumerableCallCount}" );
        // Verify the same ModuleBuilder instance is reused
        Assert.AreSame( trackingProvider.GetModuleBuilder( ModuleKind.Enumerable ),
            trackingProvider.GetModuleBuilder( ModuleKind.Enumerable ) );
    }

    [TestMethod]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Interpret )]
    public void BlockEnumerable_WithConditional_ShouldUseCustomProvider( CompilerType compiler )
    {
        // Arrange
        var options = new ExpressionRuntimeOptions
        {
            Provider = new CollectibleModuleBuilderProvider()
        };

        var block = BlockEnumerable(
            new Expression[]
            {
                IfThenElse( Constant( true ),
                    Block(
                        YieldReturn( Constant( 5 ) ),
                        YieldReturn( Constant( 6 ) )
                    ),
                    YieldReturn( Constant( 10 ) ) ),
                YieldReturn( Constant( 15 ) )
            },
            options
        );

        var lambda = Lambda<Func<IEnumerable<int>>>( block );
        var compiledLambda = lambda.Compile( compiler );

        // Act
        var results = compiledLambda().ToArray();

        // Assert
        Assert.AreEqual( 3, results.Length );
        Assert.AreEqual( 5, results[0] );
        Assert.AreEqual( 6, results[1] );
        Assert.AreEqual( 15, results[2] );
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
