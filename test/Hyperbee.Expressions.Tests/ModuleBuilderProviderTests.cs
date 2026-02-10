using System.Reflection;
using System.Reflection.Emit;

namespace Hyperbee.Expressions.Tests;

[TestClass]
public class ModuleBuilderProviderTests
{
    [TestMethod]
    public void DefaultModuleBuilderProvider_ShouldReturnAsyncModuleBuilder()
    {
        // Arrange
        var provider = new DefaultModuleBuilderProvider();

        // Act
        var moduleBuilder = provider.GetModuleBuilder( ModuleKind.Async );

        // Assert
        Assert.IsNotNull( moduleBuilder );
        Assert.IsInstanceOfType( moduleBuilder, typeof( ModuleBuilder ) );
    }

    [TestMethod]
    public void DefaultModuleBuilderProvider_ShouldReturnEnumerableModuleBuilder()
    {
        // Arrange
        var provider = new DefaultModuleBuilderProvider();

        // Act
        var moduleBuilder = provider.GetModuleBuilder( ModuleKind.Enumerable );

        // Assert
        Assert.IsNotNull( moduleBuilder );
        Assert.IsInstanceOfType( moduleBuilder, typeof( ModuleBuilder ) );
    }

    [TestMethod]
    public void DefaultModuleBuilderProvider_ShouldReturnSameInstanceOnMultipleCalls()
    {
        // Arrange
        var provider = new DefaultModuleBuilderProvider();

        // Act
        var moduleBuilder1 = provider.GetModuleBuilder( ModuleKind.Async );
        var moduleBuilder2 = provider.GetModuleBuilder( ModuleKind.Async );

        // Assert
        Assert.AreSame( moduleBuilder1, moduleBuilder2 );
    }

    [TestMethod]
    public void DefaultModuleBuilderProvider_ShouldReturnDifferentInstancesForDifferentKinds()
    {
        // Arrange
        var provider = new DefaultModuleBuilderProvider();

        // Act
        var asyncModule = provider.GetModuleBuilder( ModuleKind.Async );
        var enumerableModule = provider.GetModuleBuilder( ModuleKind.Enumerable );

        // Assert
        Assert.AreNotSame( asyncModule, enumerableModule );
    }

    [TestMethod]
    public void DefaultModuleBuilderProvider_ShouldThrowOnInvalidModuleKind()
    {
        // Arrange
        var provider = new DefaultModuleBuilderProvider();

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>( () => provider.GetModuleBuilder( (ModuleKind) 999 ) );
    }

    [TestMethod]
    public void CollectibleModuleBuilderProvider_ShouldReturnAsyncModuleBuilder()
    {
        // Arrange
        var provider = new CollectibleModuleBuilderProvider();

        // Act
        var moduleBuilder = provider.GetModuleBuilder( ModuleKind.Async );

        // Assert
        Assert.IsNotNull( moduleBuilder );
        Assert.IsInstanceOfType( moduleBuilder, typeof( ModuleBuilder ) );
    }

    [TestMethod]
    public void CollectibleModuleBuilderProvider_ShouldReturnEnumerableModuleBuilder()
    {
        // Arrange
        var provider = new CollectibleModuleBuilderProvider();

        // Act
        var moduleBuilder = provider.GetModuleBuilder( ModuleKind.Enumerable );

        // Assert
        Assert.IsNotNull( moduleBuilder );
        Assert.IsInstanceOfType( moduleBuilder, typeof( ModuleBuilder ) );
    }

    [TestMethod]
    public void CollectibleModuleBuilderProvider_ShouldReturnSameInstanceOnMultipleCalls()
    {
        // Arrange
        var provider = new CollectibleModuleBuilderProvider();

        // Act
        var moduleBuilder1 = provider.GetModuleBuilder( ModuleKind.Async );
        var moduleBuilder2 = provider.GetModuleBuilder( ModuleKind.Async );

        // Assert
        Assert.AreSame( moduleBuilder1, moduleBuilder2 );
    }

    [TestMethod]
    public void CollectibleModuleBuilderProvider_ShouldReturnDifferentInstancesForDifferentKinds()
    {
        // Arrange
        var provider = new CollectibleModuleBuilderProvider();

        // Act
        var asyncModule = provider.GetModuleBuilder( ModuleKind.Async );
        var enumerableModule = provider.GetModuleBuilder( ModuleKind.Enumerable );

        // Assert
        Assert.AreNotSame( asyncModule, enumerableModule );
    }

    [TestMethod]
    public void CollectibleModuleBuilderProvider_ShouldThrowOnInvalidModuleKind()
    {
        // Arrange
        var provider = new CollectibleModuleBuilderProvider();

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>( () => provider.GetModuleBuilder( (ModuleKind) 999 ) );
    }

    [TestMethod]
    public void CollectibleModuleBuilderProvider_ShouldUseDifferentAssemblyNameThanDefault()
    {
        // Arrange
        var defaultProvider = new DefaultModuleBuilderProvider();
        var collectibleProvider = new CollectibleModuleBuilderProvider();

        // Act
        var defaultModule = defaultProvider.GetModuleBuilder( ModuleKind.Async );
        var collectibleModule = collectibleProvider.GetModuleBuilder( ModuleKind.Async );

        // Assert
        Assert.AreNotEqual( defaultModule.Assembly.FullName, collectibleModule.Assembly.FullName );
    }

    [TestMethod]
    public void CustomModuleBuilderProvider_ShouldBeUsable()
    {
        // Arrange
        var customProvider = new CustomTestModuleBuilderProvider();

        // Act
        var asyncModule = customProvider.GetModuleBuilder( ModuleKind.Async );
        var enumerableModule = customProvider.GetModuleBuilder( ModuleKind.Enumerable );

        // Assert
        Assert.IsNotNull( asyncModule );
        Assert.IsNotNull( enumerableModule );
        Assert.AreSame( asyncModule, enumerableModule ); // Custom provider uses same module for both
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
