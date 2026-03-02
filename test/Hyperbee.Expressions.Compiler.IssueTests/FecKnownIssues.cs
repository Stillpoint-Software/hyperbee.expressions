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

    // --- Pattern 3: HyperbeeCompiler.Compile (no fallback) ---
    //
    // After Phase 3 implementation, closure patterns with mutable captured
    // variables are natively compiled by HyperbeeCompiler without needing
    // fallback to System compiler.

    [TestMethod]
    public void Pattern3_MutableCapturedVariable_HyperbeeNative()
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

        Assert.AreEqual( 2, HyperbeeCompiler.Compile<Func<int>>( outer )() );
    }

    [TestMethod]
    public void Pattern3_MutableCapturedVariable_MultipleIncrements_HyperbeeNative()
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

        Assert.AreEqual( 13, HyperbeeCompiler.Compile<Func<int>>( outer )() );
    }

    // --- Pattern 4: NegateChecked overflow (FEC known bug) ---
    //
    // FEC uses bare `neg` instead of `sub.ovf` for NegateChecked, so it does
    // not throw OverflowException when negating MinValue.

    [TestMethod]
    public void Pattern4_NegateChecked_Overflow_FecBug()
    {
        var a = Expression.Parameter( typeof(int), "a" );
        var lambda = Expression.Lambda<Func<int, int>>(
            Expression.NegateChecked( a ), a );

        // FEC compiles this but uses `neg` instead of `sub.ovf`
        // so does not throw OverflowException for MinValue
        var fec = FastExpressionCompiler.ExpressionCompiler.CompileFast( lambda );
        var fecThrew = false;
        try { fec!( int.MinValue ); } catch ( OverflowException ) { fecThrew = true; }
        Assert.IsFalse( fecThrew, "FEC known bug: NegateChecked does not throw on MinValue." );

        // Hyperbee must throw correctly
        var hb = HyperbeeCompiler.Compile( lambda );
        var hbThrew = false;
        try { hb( int.MinValue ); } catch ( OverflowException ) { hbThrew = true; }
        Assert.IsTrue( hbThrew, "Hyperbee must throw OverflowException for NegateChecked(int.MinValue)." );
    }

    // --- Pattern 5: Nested TryCatch with variable ---
    //
    // FEC can produce incorrect stack layouts with nested try/catch blocks
    // that use exception variables.

    [TestMethod]
    public void Pattern5_NestedTryCatch_WithExceptionVariable_HyperbeeNative()
    {
        var exVar = Expression.Variable( typeof(Exception), "ex" );
        var lambda = Expression.Lambda<Func<string>>(
            Expression.TryCatch(
                Expression.Block(
                    Expression.TryCatch(
                        Expression.Block(
                            Expression.Throw( Expression.New(
                                typeof(InvalidOperationException).GetConstructor(
                                    new[] { typeof(string) } )!,
                                Expression.Constant( "inner" ) ) ),
                            Expression.Constant( "not reached" )
                        ),
                        Expression.Catch(
                            exVar,
                            Expression.Property( exVar, "Message" )
                        )
                    )
                ),
                Expression.Catch(
                    typeof(Exception),
                    Expression.Constant( "outer catch" )
                )
            ) );

        Assert.AreEqual( "inner", HyperbeeCompiler.Compile<Func<string>>( lambda )() );
    }

    // --- Pattern 6: TryFinally with assignment ---
    //
    // FEC can emit incorrect IL for try/finally that assigns to a variable
    // in the finally block.

    [TestMethod]
    public void Pattern6_TryFinally_AssignInFinally_HyperbeeNative()
    {
        var result = Expression.Variable( typeof(int), "result" );
        var lambda = Expression.Lambda<Func<int>>(
            Expression.Block(
                new[] { result },
                Expression.Assign( result, Expression.Constant( 0 ) ),
                Expression.TryFinally(
                    Expression.Assign( result, Expression.Constant( 1 ) ),
                    Expression.Assign( result, Expression.Constant( 42 ) )
                ),
                result
            ) );

        Assert.AreEqual( 42, HyperbeeCompiler.Compile<Func<int>>( lambda )() );
    }

    // --- Pattern 7: Complex block with void intermediate and value return ---

    [TestMethod]
    public void Pattern7_Block_VoidIntermediateThenValueReturn_HyperbeeNative()
    {
        var list = Expression.Variable( typeof(List<int>), "list" );
        var lambda = Expression.Lambda<Func<int>>(
            Expression.Block(
                new[] { list },
                Expression.Assign( list, Expression.New( typeof(List<int>) ) ),
                Expression.Call( list, typeof(List<int>).GetMethod( "Add" )!, Expression.Constant( 42 ) ),
                Expression.Call( list, typeof(List<int>).GetMethod( "Add" )!, Expression.Constant( 99 ) ),
                Expression.Property( list, "Count" )
            ) );

        Assert.AreEqual( 2, HyperbeeCompiler.Compile<Func<int>>( lambda )() );
    }

    // --- Pattern 8: Conditional with boxing and unboxing ---
    //
    // FEC can mishandle type conversions when boxing/unboxing is involved
    // in conditional branches.

    [TestMethod]
    public void Pattern8_BoxUnbox_InConditional_HyperbeeNative()
    {
        var a = Expression.Parameter( typeof(int), "a" );
        var lambda = Expression.Lambda<Func<int, int>>(
            Expression.Condition(
                Expression.GreaterThan( a, Expression.Constant( 0 ) ),
                Expression.Convert(
                    Expression.Convert( a, typeof(object) ), // box
                    typeof(int) ), // unbox
                Expression.Constant( -1 )
            ), a );

        var fn = HyperbeeCompiler.Compile( lambda );
        Assert.AreEqual( 42, fn( 42 ) );
        Assert.AreEqual( -1, fn( -1 ) );
        Assert.AreEqual( -1, fn( 0 ) );
    }

    // --- Pattern 9: Loop with break returning value ---

    [TestMethod]
    public void Pattern9_Loop_BreakWithValue_HyperbeeNative()
    {
        var i = Expression.Variable( typeof(int), "i" );
        var breakLabel = Expression.Label( typeof(int), "break" );
        var lambda = Expression.Lambda<Func<int>>(
            Expression.Block(
                new[] { i },
                Expression.Assign( i, Expression.Constant( 0 ) ),
                Expression.Loop(
                    Expression.Block(
                        Expression.IfThen(
                            Expression.GreaterThanOrEqual( i, Expression.Constant( 5 ) ),
                            Expression.Break( breakLabel, i )
                        ),
                        Expression.AddAssign( i, Expression.Constant( 1 ) )
                    ),
                    breakLabel
                )
            ) );

        Assert.AreEqual( 5, HyperbeeCompiler.Compile<Func<int>>( lambda )() );
    }

    // --- Pattern 10: MemberInit with property bindings ---

    public class MemberInitTarget
    {
        public int X { get; set; }
        public string? Name { get; set; }
    }

    [TestMethod]
    public void Pattern10_MemberInit_HyperbeeNative()
    {
        var lambda = Expression.Lambda<Func<MemberInitTarget>>(
            Expression.MemberInit(
                Expression.New( typeof(MemberInitTarget) ),
                Expression.Bind( typeof(MemberInitTarget).GetProperty( "X" )!, Expression.Constant( 42 ) ),
                Expression.Bind( typeof(MemberInitTarget).GetProperty( "Name" )!, Expression.Constant( "test" ) )
            ) );

        var result = HyperbeeCompiler.Compile( lambda )();
        Assert.AreEqual( 42, result.X );
        Assert.AreEqual( "test", result.Name );
    }
}
