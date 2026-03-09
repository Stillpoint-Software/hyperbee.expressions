using System.Linq.Expressions;

namespace Hyperbee.Expressions.Compiler;

/// <summary>
/// Extension methods for <see cref="System.Linq.Expressions.Expression{TDelegate}"/> that compile using <see cref="HyperbeeCompiler"/>.
/// </summary>
public static class HyperbeeCompilerExtensions
{
    /// <summary>Compiles the expression using the Hyperbee compiler. Throws on unsupported patterns.</summary>
    public static TDelegate CompileHyperbee<TDelegate>( this Expression<TDelegate> expression )
        where TDelegate : Delegate
        => HyperbeeCompiler.Compile( expression );
}
