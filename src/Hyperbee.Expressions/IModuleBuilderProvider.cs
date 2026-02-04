using System.Reflection.Emit;

namespace Hyperbee.Expressions;

/// <summary>
/// Provides ModuleBuilder instances for dynamic type generation.
/// Implement this interface to control AssemblyBuilderAccess or provide custom module configuration.
/// </summary>
public interface IModuleBuilderProvider
{
    /// <summary>
    /// Gets a ModuleBuilder for the specified kind of state machine.
    /// </summary>
    /// <param name="kind">The kind of module builder needed.</param>
    /// <returns>A ModuleBuilder instance.</returns>
    ModuleBuilder GetModuleBuilder( ModuleKind kind );
}
