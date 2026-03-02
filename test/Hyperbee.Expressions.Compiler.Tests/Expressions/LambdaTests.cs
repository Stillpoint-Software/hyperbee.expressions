using System.Linq.Expressions;
using Hyperbee.Expressions.Compiler.Tests.TestSupport;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Hyperbee.Expressions.Compiler.Tests.Expressions;

[TestClass]
public class LambdaTests
{
    // ================================================================
    // Zero-parameter lambda returning constant
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Lambda_ZeroParameters_ReturnsConstant( CompilerType compilerType )
    {
        var lambda = Expression.Lambda<Func<int>>( Expression.Constant( 42 ) );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 42, fn() );
    }

    // ================================================================
    // Zero-parameter lambda with block body
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Lambda_ZeroParameters_Block( CompilerType compilerType )
    {
        var x = Expression.Variable( typeof(int), "x" );
        var body = Expression.Block(
            new[] { x },
            Expression.Assign( x, Expression.Constant( 10 ) ),
            Expression.Multiply( x, Expression.Constant( 3 ) ) );
        var lambda = Expression.Lambda<Func<int>>( body );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 30, fn() );
    }

    // ================================================================
    // Lambda with two int parameters
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Lambda_TwoIntParameters_Add( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(int), "a" );
        var b = Expression.Parameter( typeof(int), "b" );
        var lambda = Expression.Lambda<Func<int, int, int>>( Expression.Add( a, b ), a, b );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 5, fn( 2, 3 ) );
        Assert.AreEqual( 0, fn( -1, 1 ) );
    }

    // ================================================================
    // Lambda with three parameters
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Lambda_ThreeParameters_SumAll( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(int), "a" );
        var b = Expression.Parameter( typeof(int), "b" );
        var c = Expression.Parameter( typeof(int), "c" );
        var body = Expression.Add( Expression.Add( a, b ), c );
        var lambda = Expression.Lambda<Func<int, int, int, int>>( body, a, b, c );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 6, fn( 1, 2, 3 ) );
        Assert.AreEqual( 0, fn( 0, 0, 0 ) );
        Assert.AreEqual( -1, fn( -1, 0, 0 ) );
    }

    // ================================================================
    // Lambda returning string
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Lambda_StringParam_Uppercase( CompilerType compilerType )
    {
        var s = Expression.Parameter( typeof(string), "s" );
        var toUpper = typeof(string).GetMethod( "ToUpper", Type.EmptyTypes )!;
        var lambda = Expression.Lambda<Func<string, string>>(
            Expression.Call( s, toUpper ), s );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( "HELLO", fn( "hello" ) );
        Assert.AreEqual( "WORLD", fn( "World" ) );
    }

    // ================================================================
    // Lambda returning bool from comparison
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Lambda_BoolReturn_GreaterThan( CompilerType compilerType )
    {
        var x = Expression.Parameter( typeof(int), "x" );
        var lambda = Expression.Lambda<Func<int, bool>>(
            Expression.GreaterThan( x, Expression.Constant( 0 ) ), x );
        var fn = lambda.Compile( compilerType );

        Assert.IsTrue( fn( 1 ) );
        Assert.IsFalse( fn( 0 ) );
        Assert.IsFalse( fn( -1 ) );
    }

    // ================================================================
    // Lambda returning nullable int
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Lambda_ReturnsNullableInt( CompilerType compilerType )
    {
        var x = Expression.Parameter( typeof(int), "x" );
        var body = Expression.Condition(
            Expression.GreaterThan( x, Expression.Constant( 0 ) ),
            Expression.Convert( x, typeof(int?) ),
            Expression.Constant( null, typeof(int?) ) );
        var lambda = Expression.Lambda<Func<int, int?>>( body, x );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 5, fn( 5 ) );
        Assert.IsNull( fn( 0 ) );
        Assert.IsNull( fn( -3 ) );
    }

    // ================================================================
    // Action lambda (void return)
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Lambda_VoidReturn_Action( CompilerType compilerType )
    {
        var counter = Expression.Variable( typeof(int), "counter" );
        var body = Expression.Block(
            typeof( void ),
            new[] { counter },
            Expression.Assign( counter, Expression.Constant( 0 ) ) );
        var lambda = Expression.Lambda<Action>( body );
        var fn = lambda.Compile( compilerType );

        fn(); // Should not throw
    }

    // ================================================================
    // Lambda invoke on inner lambda
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Lambda_InvokeOnInnerLambda( CompilerType compilerType )
    {
        var x = Expression.Parameter( typeof(int), "x" );
        var inner = Expression.Lambda<Func<int, int>>(
            Expression.Multiply( x, Expression.Constant( 2 ) ), x );
        var invoke = Expression.Invoke( inner, Expression.Constant( 5 ) );
        var lambda = Expression.Lambda<Func<int>>( invoke );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 10, fn() );
    }

    // ================================================================
    // Lambda with parameter used multiple times
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Lambda_ParameterUsedMultipleTimes( CompilerType compilerType )
    {
        var x = Expression.Parameter( typeof(int), "x" );
        var body = Expression.Add(
            Expression.Multiply( x, x ),
            x );
        var lambda = Expression.Lambda<Func<int, int>>( body, x );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 6, fn( 2 ) );  // 2*2 + 2 = 6
        Assert.AreEqual( 12, fn( 3 ) ); // 3*3 + 3 = 12
        Assert.AreEqual( 0, fn( 0 ) );  // 0*0 + 0 = 0
    }

    // ================================================================
    // Lambda with conditional expression as body
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Lambda_ConditionalBody_AbsoluteValue( CompilerType compilerType )
    {
        var x = Expression.Parameter( typeof(int), "x" );
        var body = Expression.Condition(
            Expression.GreaterThanOrEqual( x, Expression.Constant( 0 ) ),
            x,
            Expression.Negate( x ) );
        var lambda = Expression.Lambda<Func<int, int>>( body, x );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 5, fn( 5 ) );
        Assert.AreEqual( 5, fn( -5 ) );
        Assert.AreEqual( 0, fn( 0 ) );
    }

    // ================================================================
    // Invoke: pass lambda as Expression.Constant and invoke it
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Lambda_InvokeConstantDelegate( CompilerType compilerType )
    {
        Func<int, int> addOne = x => x + 1;
        var delegateConst = Expression.Constant( addOne );
        var param = Expression.Parameter( typeof(int), "n" );
        var invoke = Expression.Invoke( delegateConst, param );
        var lambda = Expression.Lambda<Func<int, int>>( invoke, param );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 6, fn( 5 ) );
        Assert.AreEqual( 1, fn( 0 ) );
    }

    // ================================================================
    // Lambda with string concatenation (3 params)
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Lambda_StringConcat_ThreeParams( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(string), "a" );
        var b = Expression.Parameter( typeof(string), "b" );
        var c = Expression.Parameter( typeof(string), "c" );
        var concatMethod = typeof(string).GetMethod( "Concat",
            [typeof(string), typeof(string), typeof(string)] )!;
        var body = Expression.Call( null, concatMethod, a, b, c );
        var lambda = Expression.Lambda<Func<string, string, string, string>>( body, a, b, c );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( "abc", fn( "a", "b", "c" ) );
        Assert.AreEqual( "hello world!", fn( "hello", " world", "!" ) );
    }

    // ================================================================
    // Lambda with type conversion parameter
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Lambda_CastParamBeforeUse( CompilerType compilerType )
    {
        var x = Expression.Parameter( typeof(object), "x" );
        var body = Expression.Add(
            Expression.Convert( x, typeof(int) ),
            Expression.Constant( 1 ) );
        var lambda = Expression.Lambda<Func<object, int>>( body, x );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 6, fn( 5 ) );
        Assert.AreEqual( 1, fn( 0 ) );
    }

    // ================================================================
    // Two separate lambdas with same parameter names — no conflicts
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Lambda_SameParamNames_TwoLambdas_Independent( CompilerType compilerType )
    {
        var x1 = Expression.Parameter( typeof(int), "x" );
        var lambda1 = Expression.Lambda<Func<int, int>>(
            Expression.Multiply( x1, Expression.Constant( 2 ) ), x1 );

        var x2 = Expression.Parameter( typeof(int), "x" );
        var lambda2 = Expression.Lambda<Func<int, int>>(
            Expression.Add( x2, Expression.Constant( 100 ) ), x2 );

        var fn1 = lambda1.Compile( compilerType );
        var fn2 = lambda2.Compile( compilerType );

        Assert.AreEqual( 10, fn1( 5 ) );
        Assert.AreEqual( 105, fn2( 5 ) );
    }

    // ================================================================
    // Lambda with default parameter value behavior
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Lambda_DefaultValueExpression_Int( CompilerType compilerType )
    {
        var body = Expression.Default( typeof(int) );
        var lambda = Expression.Lambda<Func<int>>( body );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 0, fn() );
    }

    // ================================================================
    // Lambda with boxing: int param returned as object
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Lambda_BoxIntParam_ReturnsObject( CompilerType compilerType )
    {
        var x = Expression.Parameter( typeof(int), "x" );
        var body = Expression.Convert( x, typeof(object) );
        var lambda = Expression.Lambda<Func<int, object>>( body, x );
        var fn = lambda.Compile( compilerType );

        var result = fn( 42 );
        Assert.IsInstanceOfType<int>( result );
        Assert.AreEqual( 42, (int) result );
    }

    // ================================================================
    // Lambda with compound expression: (a + b) * (a - b)
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Lambda_CompoundArithmetic( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(int), "a" );
        var b = Expression.Parameter( typeof(int), "b" );
        var body = Expression.Multiply(
            Expression.Add( a, b ),
            Expression.Subtract( a, b ) );
        var lambda = Expression.Lambda<Func<int, int, int>>( body, a, b );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( (5 + 3) * (5 - 3), fn( 5, 3 ) ); // 16
        Assert.AreEqual( 0, fn( 4, 4 ) );  // (4+4)*(4-4)=0
        Assert.AreEqual( -9, fn( 0, 3 ) ); // (0+3)*(0-3)=-9
    }

    // ================================================================
    // Lambda capturing variable — returned as delegate from block
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Lambda_CapturesVariable_ReturnedAsDelegateFromBlock( CompilerType compilerType )
    {
        // () => { var m = 3; return (x) => x * m; }
        var multiplier = Expression.Variable( typeof(int), "multiplier" );
        var x = Expression.Parameter( typeof(int), "x" );
        var inner = Expression.Lambda<Func<int, int>>( Expression.Multiply( x, multiplier ), x );
        var outer = Expression.Lambda<Func<Func<int, int>>>(
            Expression.Block(
                new[] { multiplier },
                Expression.Assign( multiplier, Expression.Constant( 3 ) ),
                inner ) );

        var getMultiplier = outer.Compile( compilerType );
        var multiply = getMultiplier();

        Assert.AreEqual( 21, multiply( 7 ) );
        Assert.AreEqual( 0, multiply( 0 ) );
        Assert.AreEqual( -3, multiply( -1 ) );
    }
}
