namespace Hyperbee.Expressions.Compiler.Diagnostics;

/// <summary>
/// Optional diagnostics callbacks for the HEC compiler pipeline.
/// Pass an instance to <see cref="HyperbeeCompiler.Compile(System.Linq.Expressions.LambdaExpression, CompilerDiagnostics?)"/>
/// to capture intermediate representations.
/// </summary>
public class CompilerDiagnostics
{
    /// <summary>
    /// Called after IR lowering and transformation with a human-readable IR listing.
    /// </summary>
    public Action<string>? IRCapture { get; init; }

    /// <summary>
    /// Called after IL emission with a human-readable IL disassembly.
    /// </summary>
    public Action<string>? ILCapture { get; init; }
}
