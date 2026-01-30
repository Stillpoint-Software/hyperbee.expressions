namespace Hyperbee.Expressions;

/// <summary>
/// Configuration options for expression runtime behavior.
/// </summary>
public class ExpressionRuntimeOptions
{
    /// <summary>
    /// Gets or sets the ModuleBuilder provider for this expression.
    /// Defaults to <see cref="DefaultModuleBuilderProvider"/>
    /// </summary>
    public IModuleBuilderProvider ModuleBuilderProvider { get; init; } = DefaultModuleBuilderProvider.Instance;
}
