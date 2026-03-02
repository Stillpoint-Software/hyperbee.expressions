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
}
