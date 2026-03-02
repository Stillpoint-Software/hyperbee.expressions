using System.Linq.Expressions;
using Hyperbee.Expressions.Compiler.Tests.TestSupport;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Hyperbee.Expressions.Compiler.Tests.Expressions;

[TestClass]
public class LoopTests
{
    // ================================================================
    // Loop with break (counter to 5)
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public void Loop_Counter_BreaksAt5( CompilerType compilerType )
    {
        // int i = 0;
        // loop { if (i >= 5) break; i = i + 1; }
        // return i;
        var i = Expression.Variable( typeof( int ), "i" );
        var breakLabel = Expression.Label( typeof( int ), "break" );

        var loop = Expression.Loop(
            Expression.IfThenElse(
                Expression.LessThan( i, Expression.Constant( 5 ) ),
                Expression.Assign( i, Expression.Add( i, Expression.Constant( 1 ) ) ),
                Expression.Break( breakLabel, i ) ),
            breakLabel );

        var body = Expression.Block(
            new[] { i },
            Expression.Assign( i, Expression.Constant( 0 ) ),
            loop );

        var lambda = Expression.Lambda<Func<int>>( body );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 5, fn() );
    }

    // ================================================================
    // Loop with void break
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public void Loop_VoidBreak_SumTo10( CompilerType compilerType )
    {
        // int sum = 0, i = 0;
        // loop { if (i >= 10) break; sum += i; i++; }
        // return sum;
        var sum = Expression.Variable( typeof( int ), "sum" );
        var i = Expression.Variable( typeof( int ), "i" );
        var breakLabel = Expression.Label( "break" );

        var loop = Expression.Loop(
            Expression.Block(
                Expression.IfThen(
                    Expression.GreaterThanOrEqual( i, Expression.Constant( 10 ) ),
                    Expression.Break( breakLabel ) ),
                Expression.AddAssign( sum, i ),
                Expression.PostIncrementAssign( i ) ),
            breakLabel );

        var body = Expression.Block(
            new[] { sum, i },
            Expression.Assign( sum, Expression.Constant( 0 ) ),
            Expression.Assign( i, Expression.Constant( 0 ) ),
            loop,
            sum );

        var lambda = Expression.Lambda<Func<int>>( body );
        var fn = lambda.Compile( compilerType );

        // Sum 0..9 = 45
        Assert.AreEqual( 45, fn() );
    }

    // ================================================================
    // Loop with continue
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public void Loop_Continue_SkipsOddNumbers( CompilerType compilerType )
    {
        // int sum = 0, i = 0;
        // loop {
        //   if (i >= 10) break;
        //   i++;
        //   if (i % 2 != 0) continue;
        //   sum += i;
        // }
        // return sum;
        var sum = Expression.Variable( typeof( int ), "sum" );
        var i = Expression.Variable( typeof( int ), "i" );
        var breakLabel = Expression.Label( "break" );
        var continueLabel = Expression.Label( "continue" );

        var loop = Expression.Loop(
            Expression.Block(
                Expression.IfThen(
                    Expression.GreaterThanOrEqual( i, Expression.Constant( 10 ) ),
                    Expression.Break( breakLabel ) ),
                Expression.PreIncrementAssign( i ),
                Expression.IfThen(
                    Expression.NotEqual(
                        Expression.Modulo( i, Expression.Constant( 2 ) ),
                        Expression.Constant( 0 ) ),
                    Expression.Continue( continueLabel ) ),
                Expression.AddAssign( sum, i ) ),
            breakLabel,
            continueLabel );

        var body = Expression.Block(
            new[] { sum, i },
            Expression.Assign( sum, Expression.Constant( 0 ) ),
            Expression.Assign( i, Expression.Constant( 0 ) ),
            loop,
            sum );

        var lambda = Expression.Lambda<Func<int>>( body );
        var fn = lambda.Compile( compilerType );

        // Even numbers 2+4+6+8+10 = 30
        Assert.AreEqual( 30, fn() );
    }

    // ================================================================
    // Nested loops
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public void Loop_Nested_MultiplicationTable( CompilerType compilerType )
    {
        // int sum = 0;
        // for (i = 1; i <= 3; i++)
        //   for (j = 1; j <= 3; j++)
        //     sum += i * j;
        // return sum;
        var sum = Expression.Variable( typeof( int ), "sum" );
        var i = Expression.Variable( typeof( int ), "i" );
        var j = Expression.Variable( typeof( int ), "j" );
        var outerBreak = Expression.Label( "outerBreak" );
        var innerBreak = Expression.Label( "innerBreak" );

        var innerLoop = Expression.Loop(
            Expression.Block(
                Expression.IfThen(
                    Expression.GreaterThan( j, Expression.Constant( 3 ) ),
                    Expression.Break( innerBreak ) ),
                Expression.AddAssign( sum, Expression.Multiply( i, j ) ),
                Expression.PostIncrementAssign( j ) ),
            innerBreak );

        var outerLoop = Expression.Loop(
            Expression.Block(
                Expression.IfThen(
                    Expression.GreaterThan( i, Expression.Constant( 3 ) ),
                    Expression.Break( outerBreak ) ),
                Expression.Assign( j, Expression.Constant( 1 ) ),
                innerLoop,
                Expression.PostIncrementAssign( i ) ),
            outerBreak );

        var body = Expression.Block(
            new[] { sum, i, j },
            Expression.Assign( sum, Expression.Constant( 0 ) ),
            Expression.Assign( i, Expression.Constant( 1 ) ),
            outerLoop,
            sum );

        var lambda = Expression.Lambda<Func<int>>( body );
        var fn = lambda.Compile( compilerType );

        // (1*1 + 1*2 + 1*3) + (2*1 + 2*2 + 2*3) + (3*1 + 3*2 + 3*3) = 6 + 12 + 18 = 36
        Assert.AreEqual( 36, fn() );
    }

    // ================================================================
    // While simulation — condition checked at top
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Loop_WhileSimulation_ConditionAtTop( CompilerType compilerType )
    {
        // while (i < 5) { sum += i; i++; }
        var sum = Expression.Variable( typeof( int ), "sum" );
        var i = Expression.Variable( typeof( int ), "i" );
        var breakLabel = Expression.Label( "break" );

        var loop = Expression.Loop(
            Expression.Block(
                Expression.IfThen(
                    Expression.GreaterThanOrEqual( i, Expression.Constant( 5 ) ),
                    Expression.Break( breakLabel ) ),
                Expression.AddAssign( sum, i ),
                Expression.PostIncrementAssign( i ) ),
            breakLabel );

        var body = Expression.Block(
            new[] { sum, i },
            Expression.Assign( sum, Expression.Constant( 0 ) ),
            Expression.Assign( i, Expression.Constant( 0 ) ),
            loop,
            sum );

        var lambda = Expression.Lambda<Func<int>>( body );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 10, fn() );  // 0+1+2+3+4 = 10
    }

    // ================================================================
    // Do-while simulation — executes at least once
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Loop_DoWhileSimulation_ExecutesAtLeastOnce( CompilerType compilerType )
    {
        // do { count++; } while (count < 3);
        var count = Expression.Variable( typeof( int ), "count" );
        var breakLabel = Expression.Label( "break" );

        var loop = Expression.Loop(
            Expression.Block(
                Expression.PostIncrementAssign( count ),
                Expression.IfThen(
                    Expression.GreaterThanOrEqual( count, Expression.Constant( 3 ) ),
                    Expression.Break( breakLabel ) ) ),
            breakLabel );

        var body = Expression.Block(
            new[] { count },
            Expression.Assign( count, Expression.Constant( 0 ) ),
            loop,
            count );

        var lambda = Expression.Lambda<Func<int>>( body );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 3, fn() );
    }

    // ================================================================
    // Infinite loop — break immediately
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Loop_InfiniteLoop_BreakImmediately( CompilerType compilerType )
    {
        // loop { break(42); }
        var breakLabel = Expression.Label( typeof( int ), "break" );

        var loop = Expression.Loop(
            Expression.Break( breakLabel, Expression.Constant( 42 ) ),
            breakLabel );

        var lambda = Expression.Lambda<Func<int>>( loop );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 42, fn() );
    }

    // ================================================================
    // Loop counting downward
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Loop_CounterDownward_BreaksAtZero( CompilerType compilerType )
    {
        // var i = 5; var prod = 1;
        // loop { if (i == 0) break; prod *= i; i--; }
        // return prod; // 5! = 120
        var i = Expression.Variable( typeof( int ), "i" );
        var prod = Expression.Variable( typeof( int ), "prod" );
        var breakLabel = Expression.Label( "break" );

        var loop = Expression.Loop(
            Expression.Block(
                Expression.IfThen(
                    Expression.Equal( i, Expression.Constant( 0 ) ),
                    Expression.Break( breakLabel ) ),
                Expression.MultiplyAssign( prod, i ),
                Expression.PostDecrementAssign( i ) ),
            breakLabel );

        var body = Expression.Block(
            new[] { i, prod },
            Expression.Assign( i, Expression.Constant( 5 ) ),
            Expression.Assign( prod, Expression.Constant( 1 ) ),
            loop,
            prod );

        var lambda = Expression.Lambda<Func<int>>( body );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 120, fn() );  // 5! = 120
    }

    // ================================================================
    // Loop array sum — iterate all elements
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Loop_ArraySum_AllElements( CompilerType compilerType )
    {
        // int[] arr = {1, 2, 3, 4, 5};
        // var sum = 0; var i = 0;
        // loop { if (i >= arr.Length) break; sum += arr[i]; i++; }
        // return sum;
        var arr = Expression.Parameter( typeof( int[] ), "arr" );
        var sum = Expression.Variable( typeof( int ), "sum" );
        var i = Expression.Variable( typeof( int ), "i" );
        var breakLabel = Expression.Label( "break" );
        var lengthProp = typeof( int[] ).GetProperty( "Length" )!;

        var loop = Expression.Loop(
            Expression.Block(
                Expression.IfThen(
                    Expression.GreaterThanOrEqual( i, Expression.Property( arr, lengthProp ) ),
                    Expression.Break( breakLabel ) ),
                Expression.AddAssign( sum, Expression.ArrayIndex( arr, i ) ),
                Expression.PostIncrementAssign( i ) ),
            breakLabel );

        var body = Expression.Block(
            new[] { sum, i },
            Expression.Assign( sum, Expression.Constant( 0 ) ),
            Expression.Assign( i, Expression.Constant( 0 ) ),
            loop,
            sum );

        var lambda = Expression.Lambda<Func<int[], int>>( body, arr );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 15, fn( [1, 2, 3, 4, 5] ) );
        Assert.AreEqual( 0, fn( [] ) );
    }

    // ================================================================
    // Loop with continue — skips conditional
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Loop_ContinueSkipsIfCondition( CompilerType compilerType )
    {
        // Sums only multiples of 3 from 1..9
        var sum = Expression.Variable( typeof( int ), "sum" );
        var i = Expression.Variable( typeof( int ), "i" );
        var breakLabel = Expression.Label( "break" );
        var continueLabel = Expression.Label( "continue" );

        var loop = Expression.Loop(
            Expression.Block(
                Expression.IfThen(
                    Expression.GreaterThan( i, Expression.Constant( 9 ) ),
                    Expression.Break( breakLabel ) ),
                Expression.PostIncrementAssign( i ),
                Expression.IfThen(
                    Expression.NotEqual( Expression.Modulo( i, Expression.Constant( 3 ) ), Expression.Constant( 0 ) ),
                    Expression.Continue( continueLabel ) ),
                Expression.AddAssign( sum, i ) ),
            breakLabel,
            continueLabel );

        var body = Expression.Block(
            new[] { sum, i },
            Expression.Assign( sum, Expression.Constant( 0 ) ),
            Expression.Assign( i, Expression.Constant( 0 ) ),
            loop,
            sum );

        var lambda = Expression.Lambda<Func<int>>( body );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 18, fn() );  // 3 + 6 + 9 = 18
    }

    // ================================================================
    // Fibonacci via loop
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Loop_FibonacciSequence_NthTerm( CompilerType compilerType )
    {
        // Compute fib(n) iteratively
        var n = Expression.Parameter( typeof( int ), "n" );
        var a = Expression.Variable( typeof( int ), "a" );
        var b = Expression.Variable( typeof( int ), "b" );
        var tmp = Expression.Variable( typeof( int ), "tmp" );
        var i = Expression.Variable( typeof( int ), "i" );
        var breakLabel = Expression.Label( "break" );

        var loop = Expression.Loop(
            Expression.Block(
                Expression.IfThen(
                    Expression.GreaterThanOrEqual( i, n ),
                    Expression.Break( breakLabel ) ),
                Expression.Assign( tmp, Expression.Add( a, b ) ),
                Expression.Assign( a, b ),
                Expression.Assign( b, tmp ),
                Expression.PostIncrementAssign( i ) ),
            breakLabel );

        var body = Expression.Block(
            new[] { a, b, tmp, i },
            Expression.Assign( a, Expression.Constant( 0 ) ),
            Expression.Assign( b, Expression.Constant( 1 ) ),
            Expression.Assign( i, Expression.Constant( 0 ) ),
            loop,
            b );

        var lambda = Expression.Lambda<Func<int, int>>( body, n );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 1, fn( 1 ) );  // after 1 iteration: b=1 (fib(2))
        Assert.AreEqual( 8, fn( 5 ) );  // after 5 iterations: b=8 (fib(6))
        Assert.AreEqual( 55, fn( 9 ) ); // after 9 iterations: b=55 (fib(10))
    }

    // ================================================================
    // Loop with multiple break points
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Loop_MultipleBreakPoints_EarlyExitOnNegative( CompilerType compilerType )
    {
        // FEC known bug: FEC generates invalid IL for Loop with multiple typed Break targets
        // (JIT rejects the stack layout). See FecKnownIssues.Pattern26.
        if ( compilerType == CompilerType.Fast )
            Assert.Inconclusive( "Suppressed: FEC generates invalid IL for Loop with multiple typed Break targets. See FecKnownIssues.Pattern26." );

        // Sums elements until hitting a negative value or count of 3
        var arr = Expression.Parameter( typeof( int[] ), "arr" );
        var sum = Expression.Variable( typeof( int ), "sum" );
        var i = Expression.Variable( typeof( int ), "i" );
        var breakLabel = Expression.Label( typeof( int ), "break" );

        var loop = Expression.Loop(
            Expression.Block(
                Expression.IfThen(
                    Expression.GreaterThanOrEqual( i, Expression.Constant( 3 ) ),
                    Expression.Break( breakLabel, sum ) ),
                Expression.IfThen(
                    Expression.LessThan( Expression.ArrayIndex( arr, i ), Expression.Constant( 0 ) ),
                    Expression.Break( breakLabel, sum ) ),
                Expression.AddAssign( sum, Expression.ArrayIndex( arr, i ) ),
                Expression.PostIncrementAssign( i ) ),
            breakLabel );

        var body = Expression.Block(
            new[] { sum, i },
            Expression.Assign( sum, Expression.Constant( 0 ) ),
            Expression.Assign( i, Expression.Constant( 0 ) ),
            loop );

        var lambda = Expression.Lambda<Func<int[], int>>( body, arr );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 6, fn( [1, 2, 3, 4, 5] ) );  // stops at count 3
        Assert.AreEqual( 3, fn( [1, 2, -1, 4, 5] ) );  // stops at negative
    }
}
