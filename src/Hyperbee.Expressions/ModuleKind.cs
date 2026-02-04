namespace Hyperbee.Expressions;

/// <summary>
/// Specifies the kind of module builder needed for state machine generation.
/// </summary>
public enum ModuleKind
{
    /// <summary>
    /// Module for async/await state machines.
    /// </summary>
    Async,

    /// <summary>
    /// Module for yield/enumerable state machines.
    /// </summary>
    Enumerable
}
