namespace Hyperbee.Expressions.Compiler.IR;

/// <summary>
/// Metadata for a branch target label in the IR.
/// </summary>
public readonly record struct LabelInfo( int InstructionIndex = -1 );
