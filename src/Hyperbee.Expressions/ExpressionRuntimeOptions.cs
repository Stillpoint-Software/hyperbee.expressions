namespace Hyperbee.Expressions;

/// <summary>
/// Configuration options for expression runtime behavior.
/// </summary>
public class ExpressionRuntimeOptions
{
    /// <summary>
    /// Gets or sets the ModuleBuilder provider for this expression.
    /// If null, uses DefaultProvider.
    /// </summary>
    public IModuleBuilderProvider Provider { get; init; }

    /// <summary>
    /// Gets the effective provider (instance provider or default).
    /// </summary>
    internal IModuleBuilderProvider GetEffectiveProvider() => Provider ?? DefaultModuleBuilderProvider.Instance;
}
