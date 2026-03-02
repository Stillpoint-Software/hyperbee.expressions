using System.Linq.Expressions;

namespace Hyperbee.ExpressionCompiler;

public static class HyperbeeCompilerExtensions
{
    public static TDelegate CompileHyperbee<TDelegate>( this Expression<TDelegate> expression )
        where TDelegate : Delegate
        => HyperbeeCompiler.Compile( expression );
}
