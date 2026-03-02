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

    // --- Pattern 11: Compound assignment (AddAssign) in expression position ---
    //
    // FEC can mishandle compound assignment operators when the result value
    // is used (expression position rather than statement position).

    [TestMethod]
    public void Pattern11_AddAssign_ExpressionPosition_HyperbeeNative()
    {
        var x = Expression.Variable( typeof(int), "x" );
        var lambda = Expression.Lambda<Func<int>>(
            Expression.Block(
                new[] { x },
                Expression.Assign( x, Expression.Constant( 10 ) ),
                // AddAssign returns the new value: x += 5 => 15
                Expression.AddAssign( x, Expression.Constant( 5 ) )
            ) );

        Assert.AreEqual( 15, HyperbeeCompiler.Compile<Func<int>>( lambda )() );
    }

    [TestMethod]
    public void Pattern11_SubtractAssign_ExpressionPosition_HyperbeeNative()
    {
        var x = Expression.Variable( typeof(int), "x" );
        var lambda = Expression.Lambda<Func<int>>(
            Expression.Block(
                new[] { x },
                Expression.Assign( x, Expression.Constant( 10 ) ),
                Expression.SubtractAssign( x, Expression.Constant( 3 ) )
            ) );

        Assert.AreEqual( 7, HyperbeeCompiler.Compile<Func<int>>( lambda )() );
    }

    // --- Pattern 12: TypeAs with value that is null ---
    //
    // FEC can mishandle TypeAs when the result is null (e.g., incompatible types).

    [TestMethod]
    public void Pattern12_TypeAs_NullResult_HyperbeeNative()
    {
        var obj = Expression.Parameter( typeof(object), "obj" );
        var lambda = Expression.Lambda<Func<object, string?>>(
            Expression.TypeAs( obj, typeof(string) ), obj );

        var fn = HyperbeeCompiler.Compile( lambda );
        Assert.AreEqual( "hello", fn( "hello" ) );
        Assert.IsNull( fn( 42 ) );
        Assert.IsNull( fn( null! ) );
    }

    // --- Pattern 13: Nested lambda capturing multiple variables ---
    //
    // FEC sometimes fails to correctly manage multiple captured variables
    // in deeply nested lambdas.

    [TestMethod]
    public void Pattern13_MultipleCapturedVariables_HyperbeeNative()
    {
        var x = Expression.Variable( typeof(int), "x" );
        var y = Expression.Variable( typeof(int), "y" );
        var adder = Expression.Lambda<Func<int>>(
            Expression.Add( x, y ) );
        var outer = Expression.Lambda<Func<int>>(
            Expression.Block(
                new[] { x, y },
                Expression.Assign( x, Expression.Constant( 10 ) ),
                Expression.Assign( y, Expression.Constant( 32 ) ),
                Expression.Invoke( adder )
            ) );

        Assert.AreEqual( 42, HyperbeeCompiler.Compile<Func<int>>( outer )() );
    }

    // --- Pattern 14: TryCatch with exception filter ---
    //
    // Exception filters (when clauses) are a complex CLR feature that
    // FEC has limited support for.

    [TestMethod]
    public void Pattern14_TryCatch_WithFilter_HyperbeeNative()
    {
        var ex = Expression.Variable( typeof(Exception), "ex" );
        var lambda = Expression.Lambda<Func<string>>(
            Expression.TryCatch(
                Expression.Block(
                    Expression.Throw( Expression.New(
                        typeof(InvalidOperationException).GetConstructor(
                            new[] { typeof(string) } )!,
                        Expression.Constant( "filtered" ) ) ),
                    Expression.Constant( "not reached" )
                ),
                Expression.Catch(
                    ex,
                    Expression.Property( ex, "Message" ),
                    // Filter: only catch if message contains "filtered"
                    Expression.Call(
                        Expression.Property( ex, "Message" ),
                        typeof(string).GetMethod( "Contains", new[] { typeof(string) } )!,
                        Expression.Constant( "filtered" ) )
                )
            ) );

        Assert.AreEqual( "filtered", HyperbeeCompiler.Compile<Func<string>>( lambda )() );
    }

    [TestMethod]
    public void Pattern14_TryCatch_FilterDoesNotMatch_FallsThrough()
    {
        var ex = Expression.Variable( typeof(Exception), "ex" );
        var lambda = Expression.Lambda<Func<string>>(
            Expression.TryCatch(
                Expression.Block(
                    Expression.Throw( Expression.New(
                        typeof(InvalidOperationException).GetConstructor(
                            new[] { typeof(string) } )!,
                        Expression.Constant( "wrong message" ) ) ),
                    Expression.Constant( "not reached" )
                ),
                // First handler: filtered, won't match
                Expression.Catch(
                    ex,
                    Expression.Constant( "handler1" ),
                    Expression.Call(
                        Expression.Property( ex, "Message" ),
                        typeof(string).GetMethod( "Contains", new[] { typeof(string) } )!,
                        Expression.Constant( "NOMATCH" ) )
                ),
                // Second handler: catches all
                Expression.Catch(
                    typeof(Exception),
                    Expression.Constant( "handler2" )
                )
            ) );

        Assert.AreEqual( "handler2", HyperbeeCompiler.Compile<Func<string>>( lambda )() );
    }

    // --- Pattern 15: Coalesce with nullable value type ---
    //
    // FEC has known issues with coalesce on nullable value types,
    // especially when conversion lambdas are involved.

    [TestMethod]
    public void Pattern15_Coalesce_NullableInt_HyperbeeNative()
    {
        var x = Expression.Parameter( typeof(int?), "x" );
        var lambda = Expression.Lambda<Func<int?, int>>(
            Expression.Coalesce( x, Expression.Constant( -1 ) ), x );

        var fn = HyperbeeCompiler.Compile( lambda );
        Assert.AreEqual( 42, fn( 42 ) );
        Assert.AreEqual( -1, fn( null ) );
    }

    // --- Pattern 16: Value type virtual method call (constrained callvirt) ---
    //
    // FEC can produce incorrect IL for virtual calls on value types
    // (missing constrained. prefix causes boxing or verification failure).

    public struct PointStruct
    {
        public int X { get; set; }
        public int Y { get; set; }
        public override string ToString() => $"({X},{Y})";
    }

    [TestMethod]
    public void Pattern16_ValueType_VirtualCall_ToString_HyperbeeNative()
    {
        var p = Expression.Parameter( typeof(PointStruct), "p" );
        var lambda = Expression.Lambda<Func<PointStruct, string>>(
            Expression.Call( p, typeof(object).GetMethod( "ToString" )! ), p );

        var fn = HyperbeeCompiler.Compile( lambda );
        Assert.AreEqual( "(3,4)", fn( new PointStruct { X = 3, Y = 4 } ) );
    }

    // --- Pattern 17: Switch with enum values ---
    //
    // Enum switch expressions can trip up FEC's type handling.

    public enum Color { Red, Green, Blue }

    [TestMethod]
    public void Pattern17_Switch_Enum_HyperbeeNative()
    {
        var color = Expression.Parameter( typeof(Color), "color" );
        var lambda = Expression.Lambda<Func<Color, string>>(
            Expression.Switch(
                color,
                Expression.Constant( "unknown" ),
                Expression.SwitchCase( Expression.Constant( "red" ),
                    Expression.Constant( Color.Red ) ),
                Expression.SwitchCase( Expression.Constant( "green" ),
                    Expression.Constant( Color.Green ) ),
                Expression.SwitchCase( Expression.Constant( "blue" ),
                    Expression.Constant( Color.Blue ) )
            ), color );

        var fn = HyperbeeCompiler.Compile( lambda );
        Assert.AreEqual( "red", fn( Color.Red ) );
        Assert.AreEqual( "green", fn( Color.Green ) );
        Assert.AreEqual( "blue", fn( Color.Blue ) );
        Assert.AreEqual( "unknown", fn( (Color) 99 ) );
    }

    // --- Pattern 18: Array element assignment inside try/catch ---
    //
    // Combining array operations with exception handling is an area
    // where FEC's single-pass approach can produce incorrect stack layouts.

    [TestMethod]
    public void Pattern18_ArrayAssign_InsideTryCatch_HyperbeeNative()
    {
        var arr = Expression.Variable( typeof(int[]), "arr" );
        var lambda = Expression.Lambda<Func<int>>(
            Expression.Block(
                new[] { arr },
                Expression.Assign( arr,
                    Expression.NewArrayBounds( typeof(int), Expression.Constant( 3 ) ) ),
                Expression.TryCatch(
                    Expression.Block(
                        Expression.Assign(
                            Expression.ArrayAccess( arr, Expression.Constant( 0 ) ),
                            Expression.Constant( 10 ) ),
                        Expression.Assign(
                            Expression.ArrayAccess( arr, Expression.Constant( 1 ) ),
                            Expression.Constant( 20 ) ),
                        Expression.Assign(
                            Expression.ArrayAccess( arr, Expression.Constant( 2 ) ),
                            Expression.Constant( 30 ) ),
                        Expression.Constant( 0 )
                    ),
                    Expression.Catch( typeof(Exception), Expression.Constant( -1 ) )
                ),
                Expression.ArrayIndex( arr, Expression.Constant( 0 ) )
            ) );

        Assert.AreEqual( 10, HyperbeeCompiler.Compile<Func<int>>( lambda )() );
    }

    // --- Pattern 19: Deeply nested conditional with different types ---
    //
    // Nested ternary expressions that require boxing or type conversion
    // in different branches.

    [TestMethod]
    public void Pattern19_NestedConditional_WithBoxing_HyperbeeNative()
    {
        var x = Expression.Parameter( typeof(int), "x" );
        // x > 0 ? (x > 10 ? (object)x : (object)"medium") : (object)"negative"
        var lambda = Expression.Lambda<Func<int, object>>(
            Expression.Condition(
                Expression.GreaterThan( x, Expression.Constant( 0 ) ),
                Expression.Condition(
                    Expression.GreaterThan( x, Expression.Constant( 10 ) ),
                    Expression.Convert( x, typeof(object) ),
                    Expression.Convert( Expression.Constant( "medium" ), typeof(object) )
                ),
                Expression.Convert( Expression.Constant( "negative" ), typeof(object) )
            ), x );

        var fn = HyperbeeCompiler.Compile( lambda );
        Assert.AreEqual( 42, fn( 42 ) );
        Assert.AreEqual( "medium", fn( 5 ) );
        Assert.AreEqual( "negative", fn( -1 ) );
    }

    // --- Pattern 20: Complex closure - lambda returned from block ---
    //
    // Returning a compiled delegate from a block that captures local variables.

    [TestMethod]
    public void Pattern20_ReturnDelegateFromBlock_HyperbeeNative()
    {
        var multiplier = Expression.Variable( typeof(int), "multiplier" );
        var x = Expression.Parameter( typeof(int), "x" );
        var innerLambda = Expression.Lambda<Func<int, int>>(
            Expression.Multiply( x, multiplier ), x );

        var outer = Expression.Lambda<Func<Func<int, int>>>(
            Expression.Block(
                new[] { multiplier },
                Expression.Assign( multiplier, Expression.Constant( 3 ) ),
                innerLambda
            ) );

        var getMultiplier = HyperbeeCompiler.Compile( outer );
        var multiply = getMultiplier();
        Assert.AreEqual( 21, multiply( 7 ) );
        Assert.AreEqual( 0, multiply( 0 ) );
        Assert.AreEqual( -3, multiply( -1 ) );
    }

    // --- Pattern 21: Not(bool?) crashes FEC with AccessViolationException ---
    //
    // FEC generates incorrect IL for lifted Not on bool?. When the delegate is invoked
    // with a null argument, FEC's generated code attempts to read protected memory,
    // crashing the entire test host process with AccessViolationException.
    //
    // Root cause: FEC does not null-guard the lifted Not operation — it attempts to
    // extract and negate the underlying bool value without checking HasValue first.
    //
    // AccessViolationException is fatal; it cannot be caught in managed code.
    // For this reason no runnable test case is provided for the FEC variant.
    // The test was confirmed by running the full NullableTests suite with and without
    // the Not_NullableBool(Fast) DataRow:
    //   - With Fast DataRow:    648 tests "pass" then Test Run Aborted (host crash)
    //   - Without Fast DataRow: 807 tests pass cleanly (no abort)
    //
    // Hyperbee handles this correctly via LowerLiftedUnary with HasValue null-check.

    [TestMethod]
    public void Pattern21_Not_NullableBool_HyperbeeNative()
    {
        // Verify Hyperbee correctly handles lifted Not on bool? (including null propagation)
        var a = Expression.Parameter( typeof(bool?), "a" );
        var lambda = Expression.Lambda<Func<bool?, bool?>>( Expression.Not( a ), a );

        var fn = HyperbeeCompiler.Compile( lambda );
        Assert.AreEqual( false, fn( true ) );
        Assert.AreEqual( true, fn( false ) );
        Assert.IsNull( fn( null ) );
    }
}
