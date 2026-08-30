using System.Reflection;
using System.Reflection.Emit;

namespace Hyperbee.Expressions;

/// <summary>
/// A <see cref="IModuleBuilderProvider"/> whose generated types can be unloaded, scoped to
/// the lifetime of the provider instance.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="DefaultModuleBuilderProvider"/> emits into one assembly per module kind, held
/// in a static for the life of the process, so every state machine type ever built stays
/// loaded. That suits an application that compiles a fixed set of expressions at startup.
/// It does not suit a host that compiles expressions in response to input -- a scripting
/// engine, a rules engine, a request-scoped pipeline -- where types accumulate without bound.
/// </para>
/// <para>
/// This provider creates its own collectible assembly, which unloads once nothing references
/// the provider, the types it produced, or any instance or delegate of those types. Hold the
/// provider for as long as the compiled delegates are in use; drop both together to reclaim.
/// Collection is not immediate -- the runtime unloads when the last reference is collected.
/// </para>
/// <example>
/// <code>
/// var provider = new CollectibleModuleBuilderProvider();
/// var options = new ExpressionRuntimeOptions { ModuleBuilderProvider = provider };
/// var compiled = Lambda&lt;Func&lt;Task&lt;int&gt;&gt;&gt;( BlockAsync( ..., options ) ).Compile();
/// </code>
/// </example>
/// </remarks>
public sealed class CollectibleModuleBuilderProvider : IModuleBuilderProvider
{
    private readonly Lazy<ModuleBuilder> _asyncModuleBuilder;
    private readonly Lazy<ModuleBuilder> _enumerableModuleBuilder;

    public CollectibleModuleBuilderProvider()
    {
        // Distinct names keep the two assemblies apart in a diagnostic dump, the way the
        // process-wide provider names its own.

        _asyncModuleBuilder = new Lazy<ModuleBuilder>( () =>
            CreateModuleBuilder( "CollectibleStateMachineAssembly", "CollectibleStateMachineModule" ) );

        _enumerableModuleBuilder = new Lazy<ModuleBuilder>( () =>
            CreateModuleBuilder( "CollectibleYieldStateMachineAssembly", "CollectibleYieldStateMachineModule" ) );
    }

    public ModuleBuilder GetModuleBuilder( ModuleKind kind )
    {
        return kind switch
        {
            ModuleKind.Async => _asyncModuleBuilder.Value,
            ModuleKind.Enumerable => _enumerableModuleBuilder.Value,
            _ => throw new ArgumentOutOfRangeException( nameof( kind ), kind, "Unknown module kind" )
        };
    }

    private static ModuleBuilder CreateModuleBuilder( string assemblyName, string moduleName )
    {
        var name = new AssemblyName( assemblyName );
        var assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly( name, AssemblyBuilderAccess.RunAndCollect );

        return assemblyBuilder.DefineDynamicModule( moduleName );
    }
}
