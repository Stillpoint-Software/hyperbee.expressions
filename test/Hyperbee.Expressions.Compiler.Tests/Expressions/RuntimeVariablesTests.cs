using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Hyperbee.Expressions.Compiler.Tests.Expressions;

[TestClass]
public class RuntimeVariablesTests
{
    // --- Basic RuntimeVariables ---

    [TestMethod]
    public void RuntimeVariables_ReadValues_ReturnsCorrectValues()
    {
        var x = Expression.Variable( typeof(int), "x" );
        var y = Expression.Variable( typeof(int), "y" );

        var lambda = Expression.Lambda<Func<IRuntimeVariables>>(
            Expression.Block(
                new[] { x, y },
                Expression.Assign( x, Expression.Constant( 10 ) ),
                Expression.Assign( y, Expression.Constant( 20 ) ),
                Expression.RuntimeVariables( x, y )
            ) );

        var fn = HyperbeeCompiler.Compile( lambda );
        var vars = fn();

        Assert.AreEqual( 2, vars.Count );
        Assert.AreEqual( 10, vars[0] );
        Assert.AreEqual( 20, vars[1] );
    }

    [TestMethod]
    public void RuntimeVariables_WriteValues_ModifiesOriginalVariables()
    {
        // Create expression that returns IRuntimeVariables, modifies through it,
        // then reads back via the variables directly.
        var x = Expression.Variable( typeof(int), "x" );
        var rv = Expression.Variable( typeof(IRuntimeVariables), "rv" );

        // Build: { x = 5; rv = RuntimeVariables(x); rv[0] = 42; return x; }
        var setItem = typeof(IRuntimeVariables).GetProperty( "Item" )!.GetSetMethod()!;

        var lambda = Expression.Lambda<Func<int>>(
            Expression.Block(
                new[] { x, rv },
                Expression.Assign( x, Expression.Constant( 5 ) ),
                Expression.Assign( rv, Expression.RuntimeVariables( x ) ),
                Expression.Call( rv, setItem, Expression.Constant( 0 ), Expression.Convert( Expression.Constant( 42 ), typeof(object) ) ),
                x
            ) );

        // Verify with System compiler first
        var systemResult = lambda.Compile()();
        Assert.AreEqual( 42, systemResult, "System compiler should return 42." );

        // Verify Hyperbee matches
        var hyperbeeResult = HyperbeeCompiler.Compile( lambda )();
        Assert.AreEqual( 42, hyperbeeResult, "Hyperbee compiler should return 42." );
    }

    [TestMethod]
    public void RuntimeVariables_SingleVariable_Works()
    {
        var x = Expression.Variable( typeof(string), "x" );

        var lambda = Expression.Lambda<Func<IRuntimeVariables>>(
            Expression.Block(
                new[] { x },
                Expression.Assign( x, Expression.Constant( "hello" ) ),
                Expression.RuntimeVariables( x )
            ) );

        var fn = HyperbeeCompiler.Compile( lambda );
        var vars = fn();

        Assert.AreEqual( 1, vars.Count );
        Assert.AreEqual( "hello", vars[0] );
    }

    [TestMethod]
    public void RuntimeVariables_MatchesSystemCompiler()
    {
        var a = Expression.Variable( typeof(int), "a" );
        var b = Expression.Variable( typeof(double), "b" );
        var c = Expression.Variable( typeof(string), "c" );

        var lambda = Expression.Lambda<Func<IRuntimeVariables>>(
            Expression.Block(
                new[] { a, b, c },
                Expression.Assign( a, Expression.Constant( 1 ) ),
                Expression.Assign( b, Expression.Constant( 2.5 ) ),
                Expression.Assign( c, Expression.Constant( "test" ) ),
                Expression.RuntimeVariables( a, b, c )
            ) );

        var systemVars = lambda.Compile()();
        var hyperbeeVars = HyperbeeCompiler.Compile( lambda )();

        Assert.AreEqual( systemVars.Count, hyperbeeVars.Count );
        for ( var i = 0; i < systemVars.Count; i++ )
        {
            Assert.AreEqual( systemVars[i], hyperbeeVars[i],
                $"Mismatch at index {i}: System={systemVars[i]}, Hyperbee={hyperbeeVars[i]}" );
        }
    }

    [TestMethod]
    public void RuntimeVariables_WithParameter_Works()
    {
        var p = Expression.Parameter( typeof(int), "p" );
        var x = Expression.Variable( typeof(int), "x" );

        var lambda = Expression.Lambda<Func<int, IRuntimeVariables>>(
            Expression.Block(
                new[] { x },
                Expression.Assign( x, Expression.Add( p, Expression.Constant( 100 ) ) ),
                Expression.RuntimeVariables( p, x )
            ), p );

        var fn = HyperbeeCompiler.Compile( lambda );
        var vars = fn( 7 );

        Assert.AreEqual( 2, vars.Count );
        Assert.AreEqual( 7, vars[0] );
        Assert.AreEqual( 107, vars[1] );
    }
}
