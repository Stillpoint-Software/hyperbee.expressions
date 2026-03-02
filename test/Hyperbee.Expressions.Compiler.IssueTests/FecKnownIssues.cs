using System.Linq.Expressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Hyperbee.Expressions.Compiler.IssueTests;

/// <summary>
/// Documents confirmed FastExpressionCompiler (FEC) failure patterns.
///
/// Each pattern either:
///  - Demonstrates the exact wrong behavior FEC produces (_FecBug tests), or
///  - Tests that <see cref="HyperbeeCompiler.CompileWithFallback"/> returns the correct result
///    for patterns where FEC fails silently (emits invalid IL or wrong code without throwing).
///
/// Patterns where FEC generates invalid IL that crashes the JIT at runtime (AccessViolationException,
/// InvalidProgramException) cannot have runnable FEC tests — the crash is documented in comments and
/// the corresponding main test suppresses the Fast DataRow with Assert.Inconclusive.
///
/// Cross-references:
///  - Main test: Assert.Inconclusive("Suppressed: ... See FecKnownIssues.PatternXX.")
///  - This file: pattern comment + _FecBug or CompileWithFallback test
/// </summary>
[TestClass]
public class FecKnownIssues
{
    // --- Pattern 1: TryCatch + Assign (FEC #495 family) ---
    //
    // FEC produces incorrect IL when the try-body is a simple Assign expression inside TryCatch.
    // The System compiler handles it correctly.

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

        Assert.AreEqual( 42, HyperbeeCompiler.CompileWithFallback<Func<int>>( lambda )() );
    }

    [TestMethod]
    public void Pattern1_TryCatch_WithAssign_CatchPath_ReturnsCorrectResult()
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

        Assert.AreEqual( -1, HyperbeeCompiler.CompileWithFallback<Func<int>>( lambda )() );
    }

    // --- Pattern 2: Return label from inside TryCatch (FEC error 1007) ---
    //
    // FEC does not detect this as unsupported; it emits invalid IL instead of
    // throwing NotSupportedExpressionException. The System compiler handles it correctly.

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

        Assert.AreEqual( 42, HyperbeeCompiler.CompileWithFallback<Func<int>>( lambda )() );
    }

    [TestMethod]
    public void Pattern2_ReturnLabelInsideTryCatch_CatchBranch_ReturnsCorrectResult()
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

        Assert.AreEqual( -1, HyperbeeCompiler.CompileWithFallback<Func<int>>( lambda )() );
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

    // --- Pattern 4: NegateChecked overflow (FEC known bug) ---
    //
    // FEC emits bare `neg` instead of `sub.ovf` for NegateChecked, so it does
    // not throw OverflowException when negating MinValue.
    // Confirmed: FEC returns int.MinValue instead of throwing.

    [TestMethod]
    public void Pattern4_NegateChecked_Overflow_FecBug()
    {
        var a = Expression.Parameter( typeof(int), "a" );
        var lambda = Expression.Lambda<Func<int, int>>(
            Expression.NegateChecked( a ), a );

        // FEC emits `neg` — does not throw OverflowException for MinValue
        var fec = FastExpressionCompiler.ExpressionCompiler.CompileFast( lambda );
        var fecThrew = false;
        try { fec( int.MinValue ); } catch ( OverflowException ) { fecThrew = true; }
        Assert.IsFalse( fecThrew, "FEC known bug: NegateChecked does not throw on MinValue." );

        // Hyperbee emits `sub.ovf` — must throw correctly
        var hb = HyperbeeCompiler.Compile( lambda );
        var hbThrew = false;
        try { hb( int.MinValue ); } catch ( OverflowException ) { hbThrew = true; }
        Assert.IsTrue( hbThrew, "Hyperbee must throw OverflowException for NegateChecked(int.MinValue)." );
    }

    // --- Pattern 21: Not(bool?) crashes FEC with AccessViolationException ---
    //
    // FEC generates incorrect IL for lifted Not on bool?. When invoked with null, FEC's
    // generated code reads protected memory, crashing the test host (AccessViolationException).
    //
    // Root cause: FEC does not null-guard the lifted Not operation — it attempts to extract
    // and negate the underlying bool without checking HasValue first.
    //
    // No runnable FEC test: AccessViolationException is unrecoverable in managed code.
    // Confirmed by running NullableTests with/without the Fast DataRow:
    //   With Fast DataRow: test run aborted mid-suite (host crash)
    //   Without Fast DataRow: suite completes cleanly
    //
    // Main test: NullableTests.Not_NullableBool — Fast DataRow suppressed via Assert.Inconclusive.

    // --- Pattern 22: ListInit with non-void Add method (e.g. HashSet<T>.Add returns bool) ---
    //
    // FEC generates invalid IL for ListInit when the ElementInit method returns a non-void value.
    // FEC fails to pop the unused return value, leaving a mismatched stack that the JIT rejects
    // with InvalidProgramException at runtime.
    //
    // No runnable FEC test: JIT rejects the delegate on first invocation, crashing the host.
    //
    // Main test: CollectionInitTests.ListInit_HashSet_NoOrder — Fast DataRow suppressed.

    // --- Pattern 23: LessThan on ulong emits signed comparison (clt instead of clt.un) ---
    //
    // FEC emits `clt` (signed) instead of `clt.un` (unsigned) for ulong LessThan.
    // This produces wrong results when the high bit is set (ulong.MaxValue reads as -1 in signed).
    // Confirmed: FEC returns false for (0 < ulong.MaxValue) which should be true.

    [TestMethod]
    public void Pattern23_LessThan_ULong_FecBug()
    {
        var a = Expression.Parameter( typeof(ulong), "a" );
        var b = Expression.Parameter( typeof(ulong), "b" );
        var lambda = Expression.Lambda<Func<ulong, ulong, bool>>( Expression.LessThan( a, b ), a, b );

        // FEC uses signed clt — ulong.MaxValue is interpreted as -1, so 0 < MaxValue is false (wrong)
        var fec = FastExpressionCompiler.ExpressionCompiler.CompileFast( lambda );
        Assert.IsFalse( fec( 0UL, ulong.MaxValue ), "FEC known bug: 0 < ulong.MaxValue returns false (signed clt)." );

        // Hyperbee uses unsigned clt.un — correct result
        var hb = HyperbeeCompiler.Compile( lambda );
        Assert.IsTrue( hb( 0UL, ulong.MaxValue ), "Hyperbee must return true for 0 < ulong.MaxValue." );
        Assert.IsFalse( hb( ulong.MaxValue, 0UL ), "ulong.MaxValue < 0 must be false." );
        Assert.IsFalse( hb( ulong.MaxValue, ulong.MaxValue ), "Equal values must return false." );
    }

    // --- Pattern 24: Loop with typed break label (Loop expression returns value) ---
    //
    // FEC generates invalid IL when a Loop uses a typed break label (non-void Loop return type).
    // The JIT rejects the IL with InvalidProgramException because FEC does not correctly
    // handle the value-producing break path.
    //
    // No runnable FEC test: JIT rejects on first invocation.
    //
    // Main test: ControlFlowTests.Loop_BreakWithValue_AssignedToVariable — Fast DataRow suppressed.

    // --- Pattern 25: ConvertChecked ulong→long emits conv.ovf.i8 instead of conv.ovf.i8.un ---
    //
    // FEC emits `conv.ovf.i8` (signed source) for ConvertChecked(ulong→long).
    // The correct instruction is `conv.ovf.i8.un` (unsigned source).
    // FEC does not throw OverflowException for ulong values exceeding long.MaxValue.
    // Confirmed: FEC silently returns a wrong value instead of throwing.

    [TestMethod]
    public void Pattern25_ConvertChecked_ULongToLong_FecBug()
    {
        var a = Expression.Parameter( typeof(ulong), "a" );
        var lambda = Expression.Lambda<Func<ulong, long>>( Expression.ConvertChecked( a, typeof(long) ), a );

        var overflowValue = (ulong) long.MaxValue + 1;

        // FEC emits conv.ovf.i8 (signed) — does not throw for ulong > long.MaxValue
        var fec = FastExpressionCompiler.ExpressionCompiler.CompileFast( lambda );
        var fecThrew = false;
        try { fec( overflowValue ); } catch ( OverflowException ) { fecThrew = true; }
        Assert.IsFalse( fecThrew, "FEC known bug: ConvertChecked(ulong→long) does not throw on overflow." );

        // Hyperbee emits conv.ovf.i8.un (unsigned) — must throw correctly
        var hb = HyperbeeCompiler.Compile( lambda );
        Assert.AreEqual( 42L, hb( 42UL ) );
        Assert.AreEqual( long.MaxValue, hb( (ulong) long.MaxValue ) );
        var hbThrew = false;
        try { hb( overflowValue ); } catch ( OverflowException ) { hbThrew = true; }
        Assert.IsTrue( hbThrew, "Hyperbee must throw OverflowException for ulong > long.MaxValue." );
    }

    // --- Pattern 26: Loop with multiple typed Break targets ---
    //
    // FEC generates invalid IL when a Loop has multiple Break expressions that carry values.
    // The JIT rejects the code with InvalidProgramException because FEC does not correctly
    // balance the evaluation stack across all break paths.
    //
    // No runnable FEC test: JIT rejects on first invocation.
    //
    // Main test: LoopTests.Loop_MultipleBreakPoints_EarlyExitOnNegative — Fast DataRow suppressed.

    // --- Pattern 27: ConvertChecked uint→int emits conv.ovf.i4 instead of conv.ovf.i4.un ---
    //
    // FEC emits `conv.ovf.i4` (signed source) for ConvertChecked(uint→int).
    // The correct instruction is `conv.ovf.i4.un` (unsigned source).
    // FEC does not throw OverflowException for uint values exceeding int.MaxValue.
    // Confirmed: FEC silently returns a wrong value (wraps to negative) instead of throwing.

    [TestMethod]
    public void Pattern27_ConvertChecked_UIntToInt_FecBug()
    {
        var a = Expression.Parameter( typeof(uint), "a" );
        var lambda = Expression.Lambda<Func<uint, int>>( Expression.ConvertChecked( a, typeof(int) ), a );

        var overflowValue = (uint) int.MaxValue + 1;

        // FEC emits conv.ovf.i4 (signed) — does not throw for uint > int.MaxValue
        var fec = FastExpressionCompiler.ExpressionCompiler.CompileFast( lambda );
        var fecThrew = false;
        try { fec( overflowValue ); } catch ( OverflowException ) { fecThrew = true; }
        Assert.IsFalse( fecThrew, "FEC known bug: ConvertChecked(uint→int) does not throw on overflow." );

        // Hyperbee emits conv.ovf.i4.un (unsigned) — must throw correctly
        var hb = HyperbeeCompiler.Compile( lambda );
        Assert.AreEqual( 42, hb( 42u ) );
        Assert.AreEqual( int.MaxValue, hb( (uint) int.MaxValue ) );
        var hbThrew = false;
        try { hb( overflowValue ); } catch ( OverflowException ) { hbThrew = true; }
        Assert.IsTrue( hbThrew, "Hyperbee must throw OverflowException for uint > int.MaxValue." );
    }
}
