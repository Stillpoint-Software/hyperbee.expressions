using System.Linq.Expressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Hyperbee.Expressions.Compiler.IssueTests;

/// <summary>
/// Regression guards for known FEC (FastExpressionCompiler) failure patterns.
/// Each test documents the pattern and asserts <see cref="HyperbeeCompiler.CompileWithFallback"/>
/// returns the correct result. These all pass now via fallback to the System compiler and will
/// serve as correctness regressions once Hyperbee's own IL emitter is implemented.
/// </summary>
[TestClass]
public class FecKnownIssues
{
    // --- Pattern 1: TryCatch + Assign (FEC #495 family) ---
    //
    // FEC produces incorrect IL when the try-body is a simple Assign expression inside TryCatch.
    // The System compiler handles this correctly.

    [TestMethod]
    public void Pattern1_TryCatch_WithAssign_ReturnsCorrectResult()
    {
        var result = Expression.Variable( typeof(int), "result" );
        var lambda = Expression.Lambda<Func<int>>(
            Expression.Block(
                new[] { result },
                Expression.TryCatch(
                    Expression.Assign( result, Expression.Constant( 42 ) ),
                    Expression.Catch( typeof(Exception), Expression.Constant( 0 ) )
                ),
                result
            ) );

        // FEC: produces incorrect IL for this pattern.
        // Hyperbee must be correct (currently falls back to System).
        Assert.AreEqual( 42, HyperbeeCompiler.CompileWithFallback<Func<int>>( lambda )() );
    }

    [TestMethod]
    public void Pattern1_TryCatch_WithAssign_CatchPath_ReturnsCorrectResult()
    {
        // Verify the catch branch also works: assign inside try throws, catch assigns -1
        var result = Expression.Variable( typeof(int), "result" );
        var throwing = Expression.Block(
            typeof(int),
            Expression.Throw( Expression.New( typeof(InvalidOperationException) ) ),
            Expression.Constant( 0 ) );
        var lambda = Expression.Lambda<Func<int>>(
            Expression.Block(
                new[] { result },
                Expression.TryCatch(
                    Expression.Assign( result, throwing ),
                    Expression.Catch(
                        typeof(InvalidOperationException),
                        Expression.Assign( result, Expression.Constant( -1 ) ) )
                ),
                result
            ) );

        Assert.AreEqual( -1, HyperbeeCompiler.CompileWithFallback<Func<int>>( lambda )() );
    }

    // --- Pattern 2: Return label from inside TryCatch (FEC error 1007) ---
    //
    // FEC does not detect this as unsupported; it emits invalid IL instead of
    // throwing NotSupportedExpressionException. The System compiler handles it correctly.
    // See also: FEC_Issue_Draft.md in the repository root.

    [TestMethod]
    public void Pattern2_ReturnLabelInsideTryCatch_ReturnsCorrectResult()
    {
        var returnLabel = Expression.Label( typeof(int), "return" );
        var lambda = Expression.Lambda<Func<int>>(
            Expression.Block(
                typeof(int),
                Expression.TryCatch(
                    Expression.Return( returnLabel, Expression.Constant( 42 ) ),
                    Expression.Catch( typeof(Exception),
                        Expression.Return( returnLabel, Expression.Constant( -1 ) ) )
                ),
                Expression.Label( returnLabel, Expression.Constant( 0 ) )
            ) );

        // FEC: does not detect this as unsupported; emits invalid IL.
        // Hyperbee compiles correctly (no longer needs fallback).
        Assert.AreEqual( 42, HyperbeeCompiler.CompileWithFallback<Func<int>>( lambda )() );
    }

    [TestMethod]
    public void Pattern2_ReturnLabelInsideTryCatch_CatchBranch_ReturnsCorrectResult()
    {
        // Verify the catch branch returns the correct value
        var returnLabel = Expression.Label( typeof(int), "return" );
        var lambda = Expression.Lambda<Func<int>>(
            Expression.Block(
                typeof(int),
                Expression.TryCatch(
                    Expression.Block(
                        Expression.Throw( Expression.New( typeof(InvalidOperationException) ) ),
                        Expression.Return( returnLabel, Expression.Constant( 42 ) )
                    ),
                    Expression.Catch( typeof(Exception),
                        Expression.Return( returnLabel, Expression.Constant( -1 ) ) )
                ),
                Expression.Label( returnLabel, Expression.Constant( 0 ) )
            ) );

        Assert.AreEqual( -1, HyperbeeCompiler.CompileWithFallback<Func<int>>( lambda )() );
    }

    // --- Pattern 1 & 2: HyperbeeCompiler.Compile (no fallback) ---
    //
    // After Phase 2 implementation, these patterns are natively compiled by
    // HyperbeeCompiler without needing fallback to System compiler.

    [TestMethod]
    public void Pattern1_TryCatch_WithAssign_HyperbeeNative()
    {
        var result = Expression.Variable( typeof(int), "result" );
        var lambda = Expression.Lambda<Func<int>>(
            Expression.Block(
                new[] { result },
                Expression.TryCatch(
                    Expression.Assign( result, Expression.Constant( 42 ) ),
                    Expression.Catch( typeof(Exception), Expression.Constant( 0 ) )
                ),
                result
            ) );

        Assert.AreEqual( 42, HyperbeeCompiler.Compile<Func<int>>( lambda )() );
    }

    [TestMethod]
    public void Pattern1_TryCatch_WithAssign_CatchPath_HyperbeeNative()
    {
        var result = Expression.Variable( typeof(int), "result" );
        var throwing = Expression.Block(
            typeof(int),
            Expression.Throw( Expression.New( typeof(InvalidOperationException) ) ),
            Expression.Constant( 0 ) );
        var lambda = Expression.Lambda<Func<int>>(
            Expression.Block(
                new[] { result },
                Expression.TryCatch(
                    Expression.Assign( result, throwing ),
                    Expression.Catch(
                        typeof(InvalidOperationException),
                        Expression.Assign( result, Expression.Constant( -1 ) ) )
                ),
                result
            ) );

        Assert.AreEqual( -1, HyperbeeCompiler.Compile<Func<int>>( lambda )() );
    }

    [TestMethod]
    public void Pattern2_ReturnLabelInsideTryCatch_HyperbeeNative()
    {
        var returnLabel = Expression.Label( typeof(int), "return" );
        var lambda = Expression.Lambda<Func<int>>(
            Expression.Block(
                typeof(int),
                Expression.TryCatch(
                    Expression.Return( returnLabel, Expression.Constant( 42 ) ),
                    Expression.Catch( typeof(Exception),
                        Expression.Return( returnLabel, Expression.Constant( -1 ) ) )
                ),
                Expression.Label( returnLabel, Expression.Constant( 0 ) )
            ) );

        Assert.AreEqual( 42, HyperbeeCompiler.Compile<Func<int>>( lambda )() );
    }

    [TestMethod]
    public void Pattern2_ReturnLabelInsideTryCatch_CatchBranch_HyperbeeNative()
    {
        var returnLabel = Expression.Label( typeof(int), "return" );
        var lambda = Expression.Lambda<Func<int>>(
            Expression.Block(
                typeof(int),
                Expression.TryCatch(
                    Expression.Block(
                        Expression.Throw( Expression.New( typeof(InvalidOperationException) ) ),
                        Expression.Return( returnLabel, Expression.Constant( 42 ) )
                    ),
                    Expression.Catch( typeof(Exception),
                        Expression.Return( returnLabel, Expression.Constant( -1 ) ) )
                ),
                Expression.Label( returnLabel, Expression.Constant( 0 ) )
            ) );

        Assert.AreEqual( -1, HyperbeeCompiler.Compile<Func<int>>( lambda )() );
    }

    // --- Pattern 3: Mutable captured variable in nested lambda ---
    //
    // FEC may fail to share the captured variable correctly across nested lambdas,
    // resulting in the counter not being incremented as expected.

    [TestMethod]
    public void Pattern3_MutableCapturedVariable_InNestedLambda_ReturnsCorrectCount()
    {
        var counter = Expression.Variable( typeof(int), "counter" );
        var increment = Expression.Lambda<Action>(
            Expression.Assign( counter, Expression.Add( counter, Expression.Constant( 1 ) ) ) );
        var outer = Expression.Lambda<Func<int>>(
            Expression.Block(
                new[] { counter },
                Expression.Assign( counter, Expression.Constant( 0 ) ),
                Expression.Invoke( increment ),
                Expression.Invoke( increment ),
                counter
            ) );

        // FEC: may fail to share the captured variable correctly.
        // Hyperbee must compile correctly (currently falls back to System).
        Assert.AreEqual( 2, HyperbeeCompiler.CompileWithFallback<Func<int>>( outer )() );
    }

    [TestMethod]
    public void Pattern3_MutableCapturedVariable_InNestedLambda_MultipleIncrements()
    {
        var counter = Expression.Variable( typeof(int), "counter" );
        var increment = Expression.Lambda<Action>(
            Expression.Assign( counter, Expression.Add( counter, Expression.Constant( 1 ) ) ) );
        var outer = Expression.Lambda<Func<int>>(
            Expression.Block(
                new[] { counter },
                Expression.Assign( counter, Expression.Constant( 10 ) ),
                Expression.Invoke( increment ),
                Expression.Invoke( increment ),
                Expression.Invoke( increment ),
                counter
            ) );

        Assert.AreEqual( 13, HyperbeeCompiler.CompileWithFallback<Func<int>>( outer )() );
    }
}
