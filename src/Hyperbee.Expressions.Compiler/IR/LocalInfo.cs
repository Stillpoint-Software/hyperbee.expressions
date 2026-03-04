namespace Hyperbee.Expressions.Compiler.IR;

/// <summary>
/// Metadata for a local variable in the IR.
/// </summary>
public readonly record struct LocalInfo( Type Type, string? Name );
