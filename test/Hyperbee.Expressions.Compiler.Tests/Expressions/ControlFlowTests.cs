using System.Linq.Expressions;
using Hyperbee.Expressions.Compiler.Tests.TestSupport;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Hyperbee.Expressions.Compiler.Tests.Expressions;

[TestClass]
public class ControlFlowTests
{
    // ================================================================
    // Goto — forward jump skips code
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Goto_ForwardJump_SkipsCode( CompilerType compilerType )
    {
        // var x = 0;
        // goto skip;
        // x = 999;  // skipped
        // skip: return x;
        var x = Expression.Variable( typeof( int ), "x" );
        var skip = Expression.Label( "skip" );

        var body = Expression.Block(
            new[] { x },
            Expression.Assign( x, Expression.Constant( 0 ) ),
            Expression.Goto( skip ),
            Expression.Assign( x, Expression.Constant( 999 ) ),  // never runs
            Expression.Label( skip ),
            x );

        var lambda = Expression.Lambda<Func<int>>( body );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 0, fn() );
    }

    // ================================================================
    // Goto — backward jump implements loop
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Goto_BackwardJump_LoopPattern( CompilerType compilerType )
    {
        // var i = 0; var sum = 0;
        // top: if (i >= 3) goto done;
        //      sum += i; i++; goto top;
        // done: return sum;  // 0+1+2 = 3
        var i = Expression.Variable( typeof( int ), "i" );
        var sum = Expression.Variable( typeof( int ), "sum" );
        var top = Expression.Label( "top" );
        var done = Expression.Label( typeof( int ), "done" );

        var body = Expression.Block(
            new[] { i, sum },
            Expression.Assign( i, Expression.Constant( 0 ) ),
            Expression.Assign( sum, Expression.Constant( 0 ) ),
            Expression.Label( top ),
            Expression.IfThen(
                Expression.GreaterThanOrEqual( i, Expression.Constant( 3 ) ),
                Expression.Goto( done, sum ) ),
            Expression.AddAssign( sum, i ),
            Expression.PostIncrementAssign( i ),
            Expression.Goto( top ),
            Expression.Label( done, Expression.Constant( 0 ) ) );

        var lambda = Expression.Lambda<Func<int>>( body );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 3, fn() );
    }

    // ================================================================
    // Label with default value — used when goto not reached
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Label_Default_UsedWhenGotoNotReached( CompilerType compilerType )
    {
        // Falls through to label without goto — uses label's default value
        var done = Expression.Label( typeof( int ), "done" );
        var x = Expression.Variable( typeof( int ), "x" );

        var body = Expression.Block(
            new[] { x },
            Expression.Assign( x, Expression.Constant( 5 ) ),
            Expression.Label( done, x ) );

        var lambda = Expression.Lambda<Func<int>>( body );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 5, fn() );
    }

    // ================================================================
    // Goto with value — assigns to labeled variable
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Goto_WithValue_ReturnsLabeledValue( CompilerType compilerType )
    {
        // goto exit with value 42
        var exit = Expression.Label( typeof( int ), "exit" );

        var body = Expression.Block(
            Expression.Goto( exit, Expression.Constant( 42 ) ),
            Expression.Constant( 0 ),           // never reached
            Expression.Label( exit, Expression.Constant( -1 ) ) );

        var lambda = Expression.Lambda<Func<int>>( body );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 42, fn() );
    }

    // ================================================================
    // Void label — goto to void label
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Goto_Void_LabelNoValue( CompilerType compilerType )
    {
        // var x = 0;
        // if (true) goto end;
        // x = 999;
        // end: return x;
        var x = Expression.Variable( typeof( int ), "x" );
        var end = Expression.Label( "end" );

        var body = Expression.Block(
            new[] { x },
            Expression.Assign( x, Expression.Constant( 0 ) ),
            Expression.IfThen( Expression.Constant( true ), Expression.Goto( end ) ),
            Expression.Assign( x, Expression.Constant( 999 ) ),
            Expression.Label( end ),
            x );

        var lambda = Expression.Lambda<Func<int>>( body );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 0, fn() );
    }

    // ================================================================
    // Multiple labels — correct target reached
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Goto_MultipleLabels_CorrectTargetReached( CompilerType compilerType )
    {
        // var x = 0;
        // goto labelB;
        // labelA: x = 1; goto done;
        // labelB: x = 2; goto done;
        // done: return x;
        var x = Expression.Variable( typeof( int ), "x" );
        var labelA = Expression.Label( "labelA" );
        var labelB = Expression.Label( "labelB" );
        var done = Expression.Label( typeof( int ), "done" );

        var body = Expression.Block(
            new[] { x },
            Expression.Assign( x, Expression.Constant( 0 ) ),
            Expression.Goto( labelB ),
            Expression.Label( labelA ),
            Expression.Assign( x, Expression.Constant( 1 ) ),
            Expression.Goto( done, x ),
            Expression.Label( labelB ),
            Expression.Assign( x, Expression.Constant( 2 ) ),
            Expression.Goto( done, x ),
            Expression.Label( done, Expression.Constant( 0 ) ) );

        var lambda = Expression.Lambda<Func<int>>( body );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 2, fn() );
    }

    // ================================================================
    // Return from nested block — early exit
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Return_FromNestedBlock_SkipsRemainingExprs( CompilerType compilerType )
    {
        // Simulates early return via goto on a return label
        var returnLabel = Expression.Label( typeof( int ), "return" );
        var x = Expression.Variable( typeof( int ), "x" );

        var inner = Expression.Block(
            Expression.Assign( x, Expression.Constant( 42 ) ),
            Expression.Goto( returnLabel, x ),
            Expression.Assign( x, Expression.Constant( 999 ) ) );  // skipped

        var body = Expression.Block(
            new[] { x },
            inner,
            Expression.Assign( x, Expression.Constant( -1 ) ),   // skipped
            Expression.Label( returnLabel, x ) );

        var lambda = Expression.Lambda<Func<int>>( body );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 42, fn() );
    }

    // ================================================================
    // Block with label — goto skips middle expressions
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Block_WithLabel_GotoSkipsMiddleExpressions( CompilerType compilerType )
    {
        // var acc = 0;
        // acc += 1;
        // goto skip;
        // acc += 100;   // skipped
        // skip: acc += 1000;
        // return acc; // 1 + 1000 = 1001
        var acc = Expression.Variable( typeof( int ), "acc" );
        var skip = Expression.Label( "skip" );

        var body = Expression.Block(
            new[] { acc },
            Expression.Assign( acc, Expression.Constant( 0 ) ),
            Expression.AddAssign( acc, Expression.Constant( 1 ) ),
            Expression.Goto( skip ),
            Expression.AddAssign( acc, Expression.Constant( 100 ) ),
            Expression.Label( skip ),
            Expression.AddAssign( acc, Expression.Constant( 1000 ) ),
            acc );

        var lambda = Expression.Lambda<Func<int>>( body );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 1001, fn() );
    }

    // ================================================================
    // Return from conditional — early exit based on condition
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Return_FromConditional_EarlyExit( CompilerType compilerType )
    {
        // (x > 0) ? goto earlyReturn(1) : noop
        // return 0;
        // earlyReturn: result label
        var x = Expression.Parameter( typeof( int ), "x" );
        var earlyReturn = Expression.Label( typeof( int ), "earlyReturn" );

        var body = Expression.Block(
            Expression.IfThen(
                Expression.GreaterThan( x, Expression.Constant( 0 ) ),
                Expression.Goto( earlyReturn, Expression.Constant( 1 ) ) ),
            Expression.Label( earlyReturn, Expression.Constant( 0 ) ) );

        var lambda = Expression.Lambda<Func<int, int>>( body, x );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 1, fn( 5 ) );
        Assert.AreEqual( 0, fn( 0 ) );
        Assert.AreEqual( 0, fn( -1 ) );
    }

    // ================================================================
    // Loop break with value — assigned to variable
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Loop_BreakWithValue_AssignedToVariable( CompilerType compilerType )
    {
        // FEC known bug: FEC generates invalid IL for Loop(break, typedLabel) — JIT rejects
        // the stack layout for typed break labels. See FecKnownIssues.Pattern24.
        if ( compilerType == CompilerType.Fast )
            Assert.Inconclusive( "Suppressed: FEC generates invalid IL for Loop with typed break label. See FecKnownIssues.Pattern24." );

        // var i = 0;
        // var result = loop { if (i == 3) break(99); i++; }
        // return result;
        var i = Expression.Variable( typeof( int ), "i" );
        var breakLabel = Expression.Label( typeof( int ), "break" );

        var loop = Expression.Loop(
            Expression.Block(
                Expression.IfThen(
                    Expression.Equal( i, Expression.Constant( 3 ) ),
                    Expression.Break( breakLabel, Expression.Constant( 99 ) ) ),
                Expression.PostIncrementAssign( i ) ),
            breakLabel );

        var body = Expression.Block(
            new[] { i },
            Expression.Assign( i, Expression.Constant( 0 ) ),
            loop );

        var lambda = Expression.Lambda<Func<int>>( body );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 99, fn() );
    }

    // ================================================================
    // Goto inside loop — exits loop to outer label
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Goto_InsideLoop_ExitsToOuterLabel( CompilerType compilerType )
    {
        // var i = 0;
        // loop { if (i == 2) goto done; i++; }
        // done: return i;
        var i = Expression.Variable( typeof( int ), "i" );
        var breakLabel = Expression.Label( "loopBreak" );
        var done = Expression.Label( typeof( int ), "done" );

        var loop = Expression.Loop(
            Expression.Block(
                Expression.IfThen(
                    Expression.Equal( i, Expression.Constant( 2 ) ),
                    Expression.Goto( done, i ) ),
                Expression.PostIncrementAssign( i ) ),
            breakLabel );

        var body = Expression.Block(
            new[] { i },
            Expression.Assign( i, Expression.Constant( 0 ) ),
            loop,
            Expression.Label( done, Expression.Constant( -1 ) ) );

        var lambda = Expression.Lambda<Func<int>>( body );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 2, fn() );
    }

    // ================================================================
    // Label with int type — default zero when fall-through
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Label_IntType_DefaultZeroWhenFallThrough( CompilerType compilerType )
    {
        // Label default is Expression.Default(typeof(int)) = 0
        var done = Expression.Label( typeof( int ), "done" );

        var body = Expression.Block(
            Expression.Label( done, Expression.Default( typeof( int ) ) ) );

        var lambda = Expression.Lambda<Func<int>>( body );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 0, fn() );
    }

    // ================================================================
    // Conditional early return via parameter
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Goto_ConditionalBranch_ToOneOfTwoLabels( CompilerType compilerType )
    {
        // if (x > 0) goto labelPos else goto labelNeg
        // labelPos: return 1
        // labelNeg: return -1
        var x = Expression.Parameter( typeof( int ), "x" );
        var result = Expression.Variable( typeof( int ), "result" );
        var labelPos = Expression.Label( "pos" );
        var labelNeg = Expression.Label( "neg" );
        var done = Expression.Label( typeof( int ), "done" );

        var body = Expression.Block(
            new[] { result },
            Expression.IfThenElse(
                Expression.GreaterThan( x, Expression.Constant( 0 ) ),
                Expression.Goto( labelPos ),
                Expression.Goto( labelNeg ) ),
            Expression.Label( labelPos ),
            Expression.Assign( result, Expression.Constant( 1 ) ),
            Expression.Goto( done, result ),
            Expression.Label( labelNeg ),
            Expression.Assign( result, Expression.Constant( -1 ) ),
            Expression.Label( done, result ) );

        var lambda = Expression.Lambda<Func<int, int>>( body, x );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 1, fn( 5 ) );
        Assert.AreEqual( -1, fn( -3 ) );
        Assert.AreEqual( -1, fn( 0 ) );
    }
}
