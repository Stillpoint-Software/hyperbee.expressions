using System.Linq.Expressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Hyperbee.Expressions.Compiler.Tests.TestSupport;

/// <summary>
/// Compiles the same expression with System and Hyperbee compilers and asserts outputs match.
/// </summary>
public static class ExpressionVerifier
{
    /// <summary>
    /// Compiles <paramref name="lambda"/> with both the System and Hyperbee compilers and
    /// asserts that the outputs match for every set of <paramref name="inputs"/>.
    /// </summary>
    public static void Verify<TDelegate>(
        Expression<TDelegate> lambda,
        params object[][] inputs )
        where TDelegate : Delegate
    {
        var system   = lambda.Compile();
        var hyperbee = HyperbeeCompiler.CompileWithFallback( lambda );

        foreach ( var args in inputs )
        {
            var expected = system.DynamicInvoke( args );
            var actual   = hyperbee.DynamicInvoke( args );
            Assert.AreEqual( expected, actual,
                $"Mismatch for input ({string.Join( ", ", args )}): " +
                $"System={expected}, Hyperbee={actual}" );
        }
    }
}
