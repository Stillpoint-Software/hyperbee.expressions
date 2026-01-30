using System.Reflection;
using System.Reflection.Emit;

namespace Hyperbee.Expressions;

/// <summary>
/// ModuleBuilder provider using AssemblyBuilderAccess.RunAndCollect.
/// Use this when working with types from collectible assemblies.
/// </summary>
public sealed class CollectibleModuleBuilderProvider : IModuleBuilderProvider
{
    public static readonly CollectibleModuleBuilderProvider Instance = new();
    
    private static readonly Lazy<ModuleBuilder> AsyncModuleBuilder = new( () =>
        CreateModuleBuilder( "RuntimeStateMachineAssembly_Collectible", "RuntimeStateMachineModule" ) );

    private static readonly Lazy<ModuleBuilder> EnumerableModuleBuilder = new( () =>
        CreateModuleBuilder( "RuntimeYieldStateMachineAssembly_Collectible", "RuntimeYieldStateMachineModule" ) );

    public ModuleBuilder GetModuleBuilder( ModuleKind kind )
    {
        return kind switch
        {
            ModuleKind.Async => AsyncModuleBuilder.Value,
            ModuleKind.Enumerable => EnumerableModuleBuilder.Value,
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
