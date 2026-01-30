using System.Reflection;
using System.Reflection.Emit;

namespace Hyperbee.Expressions;

/// <summary>
/// Default ModuleBuilder provider using AssemblyBuilderAccess.Run.
/// This maintains backward compatibility with existing behavior.
/// </summary>
public sealed class DefaultModuleBuilderProvider : IModuleBuilderProvider
{
    public static readonly DefaultModuleBuilderProvider Instance = new();
    
    private static readonly Lazy<ModuleBuilder> AsyncModuleBuilder = new( () =>
        CreateModuleBuilder( "RuntimeStateMachineAssembly", "RuntimeStateMachineModule" ) );

    private static readonly Lazy<ModuleBuilder> EnumerableModuleBuilder = new( () =>
        CreateModuleBuilder( "RuntimeYieldStateMachineAssembly", "RuntimeYieldStateMachineModule" ) );

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
        var assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly( name, AssemblyBuilderAccess.Run );
        return assemblyBuilder.DefineDynamicModule( moduleName );
    }
}
