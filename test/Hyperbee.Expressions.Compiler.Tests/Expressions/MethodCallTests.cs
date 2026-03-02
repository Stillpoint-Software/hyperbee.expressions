using System.Linq.Expressions;
using Hyperbee.Expressions.Compiler.Tests.TestSupport;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Hyperbee.Expressions.Compiler.Tests.Expressions;

[TestClass]
public class MethodCallTests
{
    // --- Static method call ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Call_Static_MathMax( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(int), "a" );
        var b = Expression.Parameter( typeof(int), "b" );
        var method = typeof(Math).GetMethod( nameof(Math.Max), new[] { typeof(int), typeof(int) } )!;
        var call = Expression.Call( method, a, b );
        var lambda = Expression.Lambda<Func<int, int, int>>( call, a, b );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 5, fn( 3, 5 ) );
        Assert.AreEqual( 5, fn( 5, 3 ) );
        Assert.AreEqual( 0, fn( 0, 0 ) );
        Assert.AreEqual( int.MaxValue, fn( int.MaxValue, 0 ) );
    }

    // --- Static method call with no arguments ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Call_Static_NoArgs( CompilerType compilerType )
    {
        var method = typeof(MethodCallTests).GetMethod( nameof(ReturnFortyTwo) )!;
        var call = Expression.Call( method );
        var lambda = Expression.Lambda<Func<int>>( call );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 42, fn() );
    }

    // --- Instance method call (string.ToUpper) ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Call_Instance_StringToUpper( CompilerType compilerType )
    {
        var s = Expression.Parameter( typeof(string), "s" );
        var method = typeof(string).GetMethod( nameof(string.ToUpper), Type.EmptyTypes )!;
        var call = Expression.Call( s, method );
        var lambda = Expression.Lambda<Func<string, string>>( call, s );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( "HELLO", fn( "hello" ) );
        Assert.AreEqual( "", fn( "" ) );
        Assert.AreEqual( "ABC", fn( "abc" ) );
    }

    // --- Instance method call with arguments (string.Contains) ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Call_Instance_StringContains( CompilerType compilerType )
    {
        var s = Expression.Parameter( typeof(string), "s" );
        var sub = Expression.Parameter( typeof(string), "sub" );
        var method = typeof(string).GetMethod( nameof(string.Contains), new[] { typeof(string) } )!;
        var call = Expression.Call( s, method, sub );
        var lambda = Expression.Lambda<Func<string, string, bool>>( call, s, sub );
        var fn = lambda.Compile( compilerType );

        Assert.IsTrue( fn( "hello world", "world" ) );
        Assert.IsFalse( fn( "hello world", "xyz" ) );
        Assert.IsTrue( fn( "hello", "" ) );
    }

    // --- Instance method call (string.Substring) ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Call_Instance_StringSubstring( CompilerType compilerType )
    {
        var s = Expression.Parameter( typeof(string), "s" );
        var start = Expression.Parameter( typeof(int), "start" );
        var length = Expression.Parameter( typeof(int), "length" );
        var method = typeof(string).GetMethod( nameof(string.Substring), new[] { typeof(int), typeof(int) } )!;
        var call = Expression.Call( s, method, start, length );
        var lambda = Expression.Lambda<Func<string, int, int, string>>( call, s, start, length );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( "llo", fn( "hello", 2, 3 ) );
        Assert.AreEqual( "he", fn( "hello", 0, 2 ) );
    }

    // --- Virtual method call on reference type (object.ToString) ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Call_Virtual_ObjectToString( CompilerType compilerType )
    {
        var obj = Expression.Parameter( typeof(object), "obj" );
        var method = typeof(object).GetMethod( nameof(object.ToString) )!;
        var call = Expression.Call( obj, method );
        var lambda = Expression.Lambda<Func<object, string>>( call, obj );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( "42", fn( 42 ) );
        Assert.AreEqual( "hello", fn( "hello" ) );
        Assert.AreEqual( "True", fn( true ) );
    }

    // --- Virtual method call on value type (constrained callvirt) ---
    // This validates the Phase 6 constrained callvirt fix

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Call_ValueType_ToString( CompilerType compilerType )
    {
        // int.ToString() on a parameter — requires constrained callvirt
        var a = Expression.Parameter( typeof(int), "a" );
        var method = typeof(int).GetMethod( nameof(int.ToString), Type.EmptyTypes )!;
        var call = Expression.Call( a, method );
        var lambda = Expression.Lambda<Func<int, string>>( call, a );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( "42", fn( 42 ) );
        Assert.AreEqual( "0", fn( 0 ) );
        Assert.AreEqual( "-1", fn( -1 ) );
    }

    // --- Value type struct method call ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Call_ValueType_DateTimeAddDays( CompilerType compilerType )
    {
        var dt = Expression.Parameter( typeof(DateTime), "dt" );
        var days = Expression.Parameter( typeof(double), "days" );
        var method = typeof(DateTime).GetMethod( nameof(DateTime.AddDays) )!;
        var call = Expression.Call( dt, method, days );
        var lambda = Expression.Lambda<Func<DateTime, double, DateTime>>( call, dt, days );
        var fn = lambda.Compile( compilerType );

        var baseDate = new DateTime( 2025, 1, 1 );
        Assert.AreEqual( new DateTime( 2025, 1, 2 ), fn( baseDate, 1.0 ) );
        Assert.AreEqual( new DateTime( 2024, 12, 31 ), fn( baseDate, -1.0 ) );
    }

    // --- Generic method call (Enumerable.Count) ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Call_Static_GenericMethod( CompilerType compilerType )
    {
        var list = Expression.Parameter( typeof(int[]), "list" );
        var method = typeof(System.Linq.Enumerable)
            .GetMethod( nameof(System.Linq.Enumerable.Count), new[] { typeof(System.Collections.Generic.IEnumerable<>).MakeGenericType( Type.MakeGenericMethodParameter( 0 ) ) } );

        // Use the simpler approach: get from a concrete expression
        var countExpr = Expression.Call(
            typeof(System.Linq.Enumerable),
            nameof(System.Linq.Enumerable.Count),
            new[] { typeof(int) },
            list );
        var lambda = Expression.Lambda<Func<int[], int>>( countExpr, list );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 0, fn( Array.Empty<int>() ) );
        Assert.AreEqual( 3, fn( new[] { 1, 2, 3 } ) );
        Assert.AreEqual( 1, fn( new[] { 42 } ) );
    }

    // --- Method call returning void (wrapped in block) ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Call_Void_ListAdd( CompilerType compilerType )
    {
        // Build: (list) => { list.Add(42); return list.Count; }
        var list = Expression.Parameter( typeof(System.Collections.Generic.List<int>), "list" );
        var addMethod = typeof(System.Collections.Generic.List<int>).GetMethod( nameof(System.Collections.Generic.List<int>.Add) )!;
        var countProp = typeof(System.Collections.Generic.List<int>).GetProperty( nameof(System.Collections.Generic.List<int>.Count) )!;

        var body = Expression.Block(
            Expression.Call( list, addMethod, Expression.Constant( 42 ) ),
            Expression.Property( list, countProp ) );
        var lambda = Expression.Lambda<Func<System.Collections.Generic.List<int>, int>>( body, list );
        var fn = lambda.Compile( compilerType );

        var testList = new System.Collections.Generic.List<int>();
        Assert.AreEqual( 1, fn( testList ) );
        Assert.AreEqual( 42, testList[0] );
    }

    // --- Multiple argument method ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Call_Static_MultipleArgs( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(int), "a" );
        var b = Expression.Parameter( typeof(int), "b" );
        var c = Expression.Parameter( typeof(int), "c" );
        var method = typeof(MethodCallTests).GetMethod( nameof(AddThree) )!;
        var call = Expression.Call( method, a, b, c );
        var lambda = Expression.Lambda<Func<int, int, int, int>>( call, a, b, c );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 6, fn( 1, 2, 3 ) );
        Assert.AreEqual( 0, fn( 0, 0, 0 ) );
        Assert.AreEqual( 3, fn( 1, 1, 1 ) );
    }

    // --- Chained method calls ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Call_Chained_StringTrimToUpper( CompilerType compilerType )
    {
        // " hello ".Trim().ToUpper()
        var s = Expression.Parameter( typeof(string), "s" );
        var trimMethod = typeof(string).GetMethod( nameof(string.Trim), Type.EmptyTypes )!;
        var toUpperMethod = typeof(string).GetMethod( nameof(string.ToUpper), Type.EmptyTypes )!;
        var call = Expression.Call( Expression.Call( s, trimMethod ), toUpperMethod );
        var lambda = Expression.Lambda<Func<string, string>>( call, s );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( "HELLO", fn( " hello " ) );
        Assert.AreEqual( "", fn( "  " ) );
        Assert.AreEqual( "A", fn( "a" ) );
    }

    // --- Math.Abs ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Call_Static_MathAbs( CompilerType compilerType )
    {
        var n = Expression.Parameter( typeof( int ), "n" );
        var method = typeof( Math ).GetMethod( nameof( Math.Abs ), [typeof( int )] )!;
        var lambda = Expression.Lambda<Func<int, int>>( Expression.Call( method, n ), n );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 5, fn( -5 ) );
        Assert.AreEqual( 5, fn( 5 ) );
        Assert.AreEqual( 0, fn( 0 ) );
    }

    // --- Math.Min ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Call_Static_MathMin( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof( int ), "a" );
        var b = Expression.Parameter( typeof( int ), "b" );
        var method = typeof( Math ).GetMethod( nameof( Math.Min ), [typeof( int ), typeof( int )] )!;
        var lambda = Expression.Lambda<Func<int, int, int>>( Expression.Call( method, a, b ), a, b );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 3, fn( 3, 5 ) );
        Assert.AreEqual( 3, fn( 5, 3 ) );
        Assert.AreEqual( int.MinValue, fn( int.MinValue, 0 ) );
    }

    // --- string.Replace ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Call_Instance_StringReplace( CompilerType compilerType )
    {
        var s = Expression.Parameter( typeof( string ), "s" );
        var method = typeof( string ).GetMethod( nameof( string.Replace ), [typeof( string ), typeof( string )] )!;
        var call = Expression.Call( s, method, Expression.Constant( "o" ), Expression.Constant( "0" ) );
        var lambda = Expression.Lambda<Func<string, string>>( call, s );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( "hell0 w0rld", fn( "hello world" ) );
        Assert.AreEqual( "f00", fn( "foo" ) );
        Assert.AreEqual( "bar", fn( "bar" ) );
    }

    // --- string.StartsWith ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Call_Instance_StringStartsWith( CompilerType compilerType )
    {
        var s = Expression.Parameter( typeof( string ), "s" );
        var prefix = Expression.Parameter( typeof( string ), "prefix" );
        var method = typeof( string ).GetMethod( nameof( string.StartsWith ), [typeof( string )] )!;
        var call = Expression.Call( s, method, prefix );
        var lambda = Expression.Lambda<Func<string, string, bool>>( call, s, prefix );
        var fn = lambda.Compile( compilerType );

        Assert.IsTrue( fn( "hello world", "hello" ) );
        Assert.IsFalse( fn( "hello world", "world" ) );
        Assert.IsTrue( fn( "abc", "" ) );
    }

    // --- string.PadLeft ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Call_Instance_StringPadLeft( CompilerType compilerType )
    {
        var s = Expression.Parameter( typeof( string ), "s" );
        var width = Expression.Parameter( typeof( int ), "width" );
        var method = typeof( string ).GetMethod( nameof( string.PadLeft ), [typeof( int )] )!;
        var call = Expression.Call( s, method, width );
        var lambda = Expression.Lambda<Func<string, int, string>>( call, s, width );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( "   42", fn( "42", 5 ) );
        Assert.AreEqual( "42", fn( "42", 2 ) );  // no padding needed
    }

    // --- Math.Pow ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Call_Static_MathPow( CompilerType compilerType )
    {
        var baseVal = Expression.Parameter( typeof( double ), "baseVal" );
        var exp = Expression.Parameter( typeof( double ), "exp" );
        var method = typeof( Math ).GetMethod( nameof( Math.Pow ) )!;
        var lambda = Expression.Lambda<Func<double, double, double>>(
            Expression.Call( method, baseVal, exp ), baseVal, exp );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 8.0, fn( 2.0, 3.0 ), 1e-9 );
        Assert.AreEqual( 1.0, fn( 5.0, 0.0 ), 1e-9 );
        Assert.AreEqual( 0.25, fn( 2.0, -2.0 ), 1e-9 );
    }

    // --- Convert.ToInt32 ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Call_Static_ConvertToInt32( CompilerType compilerType )
    {
        var s = Expression.Parameter( typeof( string ), "s" );
        var method = typeof( Convert ).GetMethod( nameof( Convert.ToInt32 ), [typeof( string )] )!;
        var lambda = Expression.Lambda<Func<string, int>>( Expression.Call( method, s ), s );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 42, fn( "42" ) );
        Assert.AreEqual( 0, fn( "0" ) );
        Assert.AreEqual( -1, fn( "-1" ) );
    }

    // --- string.Format with one arg ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Call_Static_StringFormat_OneArg( CompilerType compilerType )
    {
        var n = Expression.Parameter( typeof( int ), "n" );
        var method = typeof( string ).GetMethod( nameof( string.Format ), [typeof( string ), typeof( object )] )!;
        var call = Expression.Call( method, Expression.Constant( "Value={0}" ),
            Expression.Convert( n, typeof( object ) ) );
        var lambda = Expression.Lambda<Func<int, string>>( call, n );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( "Value=42", fn( 42 ) );
        Assert.AreEqual( "Value=0", fn( 0 ) );
    }

    // --- Enumerable.Sum ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Call_Static_EnumerableSum( CompilerType compilerType )
    {
        var list = Expression.Parameter( typeof( int[] ), "list" );
        var sumExpr = Expression.Call(
            typeof( System.Linq.Enumerable ),
            nameof( System.Linq.Enumerable.Sum ),
            null,
            list );
        var lambda = Expression.Lambda<Func<int[], int>>( sumExpr, list );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 15, fn( [1, 2, 3, 4, 5] ) );
        Assert.AreEqual( 0, fn( [] ) );
    }

    // Helper methods for tests

    public static int ReturnFortyTwo() => 42;

    public static int AddThree( int a, int b, int c ) => a + b + c;
}
