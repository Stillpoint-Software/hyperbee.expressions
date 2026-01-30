using System.Reflection;
using System.Reflection.Emit;

namespace Hyperbee.Expressions.Tests;

[TestClass]
public class ExpressionRuntimeOptionsTests
{
    [TestMethod]
    public void GetEffectiveProvider_ShouldReturnInstanceProvider_WhenSet()
    {
        // Arrange
        var instanceProvider = new CollectibleModuleBuilderProvider();
        var options = new ExpressionRuntimeOptions { Provider = instanceProvider };

        // Act
        var effectiveProvider = options.GetEffectiveProvider();

        // Assert
        Assert.AreSame( instanceProvider, effectiveProvider );
    }

    [TestMethod]
    public void MultipleOptions_ShouldHaveIndependentProviders()
    {
        // Arrange
        var provider1 = new CollectibleModuleBuilderProvider();
        var provider2 = new CustomTestModuleBuilderProvider();
        var options1 = new ExpressionRuntimeOptions { Provider = provider1 };
        var options2 = new ExpressionRuntimeOptions { Provider = provider2 };

        // Act
        var effectiveProvider1 = options1.GetEffectiveProvider();
        var effectiveProvider2 = options2.GetEffectiveProvider();

        // Assert
        Assert.AreSame( provider1, effectiveProvider1 );
        Assert.AreSame( provider2, effectiveProvider2 );
        Assert.AreNotSame( effectiveProvider1, effectiveProvider2 );
    }

    // Helper: Custom test provider
    private class CustomTestModuleBuilderProvider : IModuleBuilderProvider
    {
        private readonly Lazy<ModuleBuilder> _sharedModule = new( () =>
        {
            var assemblyName = new AssemblyName( "CustomTestAssembly" );
            var assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly( assemblyName, AssemblyBuilderAccess.Run );
            return assemblyBuilder.DefineDynamicModule( "CustomTestModule" );
        } );

        public ModuleBuilder GetModuleBuilder( ModuleKind kind )
        {
            return _sharedModule.Value;
        }
    }
}
