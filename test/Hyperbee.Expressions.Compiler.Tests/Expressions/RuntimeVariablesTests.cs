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

    // --- Bool variable — boxed as bool, not int ---

    [TestMethod]
    public void RuntimeVariables_BoolVariable_ReturnsBoxedBool()
    {
        var flag = Expression.Variable( typeof( bool ), "flag" );

        var lambda = Expression.Lambda<Func<IRuntimeVariables>>(
            Expression.Block(
                [flag],
                Expression.Assign( flag, Expression.Constant( true ) ),
                Expression.RuntimeVariables( flag )
            ) );

        var systemVars = lambda.Compile()();
        var hyperbeeVars = HyperbeeCompiler.Compile( lambda )();

        Assert.AreEqual( 1, hyperbeeVars.Count );
        Assert.AreEqual( systemVars[0], hyperbeeVars[0] );
        Assert.AreEqual( true, hyperbeeVars[0] );
    }

    // --- Unassigned variable returns the type default ---

    [TestMethod]
    public void RuntimeVariables_UnassignedIntVariable_ReturnsZero()
    {
        var x = Expression.Variable( typeof( int ), "x" );

        var lambda = Expression.Lambda<Func<IRuntimeVariables>>(
            Expression.Block(
                [x],
                Expression.RuntimeVariables( x )   // x never assigned
            ) );

        var systemVars = lambda.Compile()();
        var hyperbeeVars = HyperbeeCompiler.Compile( lambda )();

        Assert.AreEqual( 0, hyperbeeVars[0] );
        Assert.AreEqual( systemVars[0], hyperbeeVars[0] );
    }

    // --- Subset of locals: only the listed variables are exposed ---

    [TestMethod]
    public void RuntimeVariables_SubsetOfLocals_OnlySpecifiedAreExposed()
    {
        var a = Expression.Variable( typeof( int ), "a" );
        var b = Expression.Variable( typeof( int ), "b" );
        var c = Expression.Variable( typeof( int ), "c" );

        var lambda = Expression.Lambda<Func<IRuntimeVariables>>(
            Expression.Block(
                [a, b, c],
                Expression.Assign( a, Expression.Constant( 1 ) ),
                Expression.Assign( b, Expression.Constant( 2 ) ),
                Expression.Assign( c, Expression.Constant( 3 ) ),
                Expression.RuntimeVariables( a, c )   // b intentionally omitted
            ) );

        var fn = HyperbeeCompiler.Compile( lambda );
        var vars = fn();

        Assert.AreEqual( 2, vars.Count );
        Assert.AreEqual( 1, vars[0] );   // a
        Assert.AreEqual( 3, vars[1] );   // c
    }

    // --- Second assignment overwrites: RuntimeVariables reflects the latest value ---

    [TestMethod]
    public void RuntimeVariables_ReassignedVariable_ReflectsLatestValue()
    {
        var x = Expression.Variable( typeof( int ), "x" );

        var lambda = Expression.Lambda<Func<IRuntimeVariables>>(
            Expression.Block(
                [x],
                Expression.Assign( x, Expression.Constant( 10 ) ),
                Expression.Assign( x, Expression.Constant( 42 ) ),
                Expression.RuntimeVariables( x )
            ) );

        var systemVars = lambda.Compile()();
        var hyperbeeVars = HyperbeeCompiler.Compile( lambda )();

        Assert.AreEqual( 42, hyperbeeVars[0] );
        Assert.AreEqual( systemVars[0], hyperbeeVars[0] );
    }

    // --- Five variables of the same type: all indices accessible ---

    [TestMethod]
    public void RuntimeVariables_FiveVariables_AllAccessible()
    {
        var v0 = Expression.Variable( typeof( int ), "v0" );
        var v1 = Expression.Variable( typeof( int ), "v1" );
        var v2 = Expression.Variable( typeof( int ), "v2" );
        var v3 = Expression.Variable( typeof( int ), "v3" );
        var v4 = Expression.Variable( typeof( int ), "v4" );

        var lambda = Expression.Lambda<Func<IRuntimeVariables>>(
            Expression.Block(
                [v0, v1, v2, v3, v4],
                Expression.Assign( v0, Expression.Constant( 10 ) ),
                Expression.Assign( v1, Expression.Constant( 20 ) ),
                Expression.Assign( v2, Expression.Constant( 30 ) ),
                Expression.Assign( v3, Expression.Constant( 40 ) ),
                Expression.Assign( v4, Expression.Constant( 50 ) ),
                Expression.RuntimeVariables( v0, v1, v2, v3, v4 )
            ) );

        var systemVars = lambda.Compile()();
        var hyperbeeVars = HyperbeeCompiler.Compile( lambda )();

        Assert.AreEqual( 5, hyperbeeVars.Count );
        for ( var i = 0; i < 5; i++ )
            Assert.AreEqual( systemVars[i], hyperbeeVars[i] );

        Assert.AreEqual( 10, hyperbeeVars[0] );
        Assert.AreEqual( 50, hyperbeeVars[4] );
    }

    // --- Nullable int is boxed and readable ---

    [TestMethod]
    public void RuntimeVariables_NullableInt_ReturnsCorrectValue()
    {
        var x = Expression.Variable( typeof( int? ), "x" );

        var lambda = Expression.Lambda<Func<IRuntimeVariables>>(
            Expression.Block(
                [x],
                Expression.Assign( x, Expression.Constant( 7, typeof( int? ) ) ),
                Expression.RuntimeVariables( x )
            ) );

        var systemVars = lambda.Compile()();
        var hyperbeeVars = HyperbeeCompiler.Compile( lambda )();

        Assert.AreEqual( systemVars[0], hyperbeeVars[0] );
        Assert.AreEqual( 7, hyperbeeVars[0] );
    }

    // --- Variable value set by a conditional branch ---

    [TestMethod]
    public void RuntimeVariables_InConditionalBranch_ReflectsCorrectAssignment()
    {
        var flag = Expression.Parameter( typeof( bool ), "flag" );
        var x = Expression.Variable( typeof( int ), "x" );

        var lambda = Expression.Lambda<Func<bool, IRuntimeVariables>>(
            Expression.Block(
                [x],
                Expression.IfThenElse(
                    flag,
                    Expression.Assign( x, Expression.Constant( 1 ) ),
                    Expression.Assign( x, Expression.Constant( 2 ) )
                ),
                Expression.RuntimeVariables( x )
            ), flag );

        var fn = HyperbeeCompiler.Compile( lambda );

        Assert.AreEqual( 1, fn( true  )[0] );
        Assert.AreEqual( 2, fn( false )[0] );
    }

    // --- Mutation through IRuntimeVariables indexer is reflected back to the source variable ---

    [TestMethod]
    public void RuntimeVariables_WriteThroughIndexer_UpdatesSourceVariable()
    {
        var x = Expression.Variable( typeof( int ), "x" );
        var rv = Expression.Variable( typeof( IRuntimeVariables ), "rv" );
        var setItem = typeof( IRuntimeVariables ).GetProperty( "Item" )!.GetSetMethod()!;

        var lambda = Expression.Lambda<Func<int>>(
            Expression.Block(
                [x, rv],
                Expression.Assign( x, Expression.Constant( 0 ) ),
                Expression.Assign( rv, Expression.RuntimeVariables( x ) ),
                Expression.Call( rv, setItem, Expression.Constant( 0 ), Expression.Convert( Expression.Constant( 77 ), typeof( object ) ) ),
                x   // should now be 77
            ) );

        var systemResult = lambda.Compile()();
        var hyperbeeResult = HyperbeeCompiler.Compile( lambda )();

        Assert.AreEqual( 77, hyperbeeResult );
        Assert.AreEqual( systemResult, hyperbeeResult );
    }
}
