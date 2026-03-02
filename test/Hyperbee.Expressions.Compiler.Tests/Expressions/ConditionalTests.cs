using System.Linq.Expressions;
using Hyperbee.Expressions.Compiler.Tests.TestSupport;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Hyperbee.Expressions.Compiler.Tests.Expressions;

[TestClass]
public class ConditionalTests
{
    // --- Simple ternary: a > b ? a : b ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Conditional_SimpleTernary_Max( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(int), "a" );
        var b = Expression.Parameter( typeof(int), "b" );
        var lambda = Expression.Lambda<Func<int, int, int>>(
            Expression.Condition( Expression.GreaterThan( a, b ), a, b ),
            a, b );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 5, fn( 5, 3 ) );
        Assert.AreEqual( 5, fn( 3, 5 ) );
        Assert.AreEqual( 4, fn( 4, 4 ) );
        Assert.AreEqual( 0, fn( 0, -1 ) );
    }

    // --- Nested conditional ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Conditional_Nested( CompilerType compilerType )
    {
        // x < 0 ? -1 : (x == 0 ? 0 : 1)  — sign function
        var x = Expression.Parameter( typeof(int), "x" );
        var inner = Expression.Condition(
            Expression.Equal( x, Expression.Constant( 0 ) ),
            Expression.Constant( 0 ),
            Expression.Constant( 1 ) );
        var outer = Expression.Condition(
            Expression.LessThan( x, Expression.Constant( 0 ) ),
            Expression.Constant( -1 ),
            inner );
        var lambda = Expression.Lambda<Func<int, int>>( outer, x );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( -1, fn( -5 ) );
        Assert.AreEqual( 0, fn( 0 ) );
        Assert.AreEqual( 1, fn( 7 ) );
    }

    // --- Conditional with method calls in branches ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Conditional_WithMethodCallsInBranches( CompilerType compilerType )
    {
        var s = Expression.Parameter( typeof(string), "s" );
        var toUpper = typeof(string).GetMethod( nameof(string.ToUpper), Type.EmptyTypes )!;
        var toLower = typeof(string).GetMethod( nameof(string.ToLower), Type.EmptyTypes )!;
        var length  = typeof(string).GetProperty( nameof(string.Length) )!;

        // s.Length > 3 ? s.ToUpper() : s.ToLower()
        var lambda = Expression.Lambda<Func<string, string>>(
            Expression.Condition(
                Expression.GreaterThan(
                    Expression.Property( s, length ),
                    Expression.Constant( 3 ) ),
                Expression.Call( s, toUpper ),
                Expression.Call( s, toLower ) ),
            s );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( "HELLO", fn( "hello" ) );
        Assert.AreEqual( "hi", fn( "HI" ) );
        Assert.AreEqual( "FOUR", fn( "four" ) );
    }

    // --- IfThen (void, no else) ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void IfThen_Void( CompilerType compilerType )
    {
        var result = Expression.Variable( typeof(int), "result" );
        var condition = Expression.GreaterThan( result, Expression.Constant( 0 ) );
        var assign = Expression.Assign( result, Expression.Constant( 99 ) );

        // Block: result = 1; if (result > 0) result = 99; return result;
        var body = Expression.Block(
            new[] { result },
            Expression.Assign( result, Expression.Constant( 1 ) ),
            Expression.IfThen( condition, assign ),
            result );
        var lambda = Expression.Lambda<Func<int>>( body );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 99, fn() );
    }

    // --- Conditional with boxing/unboxing in branches ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Conditional_BoxUnbox_InBranches( CompilerType compilerType )
    {
        // a > 0 ? (int)(object)a : -1   — box then unbox in true branch
        var a = Expression.Parameter( typeof(int), "a" );
        var lambda = Expression.Lambda<Func<int, int>>(
            Expression.Condition(
                Expression.GreaterThan( a, Expression.Constant( 0 ) ),
                Expression.Convert(
                    Expression.Convert( a, typeof(object) ),  // box
                    typeof(int) ),                             // unbox
                Expression.Constant( -1 ) ),
            a );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 42, fn( 42 ) );
        Assert.AreEqual( -1, fn( -1 ) );
        Assert.AreEqual( -1, fn( 0 ) );
    }

    // --- Nested conditional with branches of different types (boxing to object) ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Conditional_Nested_DifferentTypeBranches_BoxedToObject( CompilerType compilerType )
    {
        // x > 0 ? (x > 10 ? (object)x : (object)"medium") : (object)"negative"
        var x = Expression.Parameter( typeof(int), "x" );
        var lambda = Expression.Lambda<Func<int, object>>(
            Expression.Condition(
                Expression.GreaterThan( x, Expression.Constant( 0 ) ),
                Expression.Condition(
                    Expression.GreaterThan( x, Expression.Constant( 10 ) ),
                    Expression.Convert( x, typeof(object) ),
                    Expression.Convert( Expression.Constant( "medium" ), typeof(object) ) ),
                Expression.Convert( Expression.Constant( "negative" ), typeof(object) ) ),
            x );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 42, fn( 42 ) );
        Assert.AreEqual( "medium", fn( 5 ) );
        Assert.AreEqual( "negative", fn( -1 ) );
    }

    // --- IfThenElse with typed result ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void IfThenElse_TypedResult( CompilerType compilerType )
    {
        var flag = Expression.Parameter( typeof(bool), "flag" );

        // flag ? "yes" : "no"
        var lambda = Expression.Lambda<Func<bool, string>>(
            Expression.Condition(
                flag,
                Expression.Constant( "yes" ),
                Expression.Constant( "no" ) ),
            flag );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( "yes", fn( true ) );
        Assert.AreEqual( "no", fn( false ) );
    }
}
