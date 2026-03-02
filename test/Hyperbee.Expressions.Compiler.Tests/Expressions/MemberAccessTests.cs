using System.Linq.Expressions;
using Hyperbee.Expressions.Compiler.Tests.TestSupport;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Hyperbee.Expressions.Compiler.Tests.Expressions;

[TestClass]
public class MemberAccessTests
{
    // --- Instance property read (string.Length) ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Property_Instance_StringLength( CompilerType compilerType )
    {
        var s = Expression.Parameter( typeof(string), "s" );
        var lengthProp = typeof(string).GetProperty( nameof(string.Length) )!;
        var body = Expression.Property( s, lengthProp );
        var lambda = Expression.Lambda<Func<string, int>>( body, s );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 5, fn( "hello" ) );
        Assert.AreEqual( 0, fn( "" ) );
        Assert.AreEqual( 11, fn( "hello world" ) );
    }

    // --- Instance property read (List<T>.Count) ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Property_Instance_ListCount( CompilerType compilerType )
    {
        var list = Expression.Parameter( typeof(System.Collections.Generic.List<int>), "list" );
        var countProp = typeof(System.Collections.Generic.List<int>).GetProperty( nameof(System.Collections.Generic.List<int>.Count) )!;
        var body = Expression.Property( list, countProp );
        var lambda = Expression.Lambda<Func<System.Collections.Generic.List<int>, int>>( body, list );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 0, fn( new System.Collections.Generic.List<int>() ) );
        Assert.AreEqual( 3, fn( new System.Collections.Generic.List<int> { 1, 2, 3 } ) );
    }

    // --- Static property read (DateTime.Now) ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Property_Static_DateTimeUtcNow( CompilerType compilerType )
    {
        var prop = typeof(DateTime).GetProperty( nameof(DateTime.UtcNow) )!;
        var body = Expression.Property( null, prop );
        var lambda = Expression.Lambda<Func<DateTime>>( body );
        var fn = lambda.Compile( compilerType );

        var before = DateTime.UtcNow;
        var result = fn();
        var after = DateTime.UtcNow;

        Assert.IsTrue( result >= before && result <= after,
            "DateTime.UtcNow should return a time within the expected range." );
    }

    // --- Static property read (string.Empty) ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Property_Static_StringEmpty( CompilerType compilerType )
    {
        var field = typeof(string).GetField( nameof(string.Empty) )!;
        var body = Expression.Field( null, field );
        var lambda = Expression.Lambda<Func<string>>( body );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( "", fn() );
    }

    // --- Instance field read ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Field_Instance_Read( CompilerType compilerType )
    {
        var obj = Expression.Parameter( typeof(TestData), "obj" );
        var field = typeof(TestData).GetField( nameof(TestData.IntField) )!;
        var body = Expression.Field( obj, field );
        var lambda = Expression.Lambda<Func<TestData, int>>( body, obj );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 42, fn( new TestData { IntField = 42 } ) );
        Assert.AreEqual( 0, fn( new TestData { IntField = 0 } ) );
        Assert.AreEqual( -1, fn( new TestData { IntField = -1 } ) );
    }

    // --- Instance field write (via Assign in Block) ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Field_Instance_Write( CompilerType compilerType )
    {
        // (obj) => { obj.IntField = 99; return obj.IntField; }
        var obj = Expression.Parameter( typeof(TestData), "obj" );
        var field = Expression.Field( obj, nameof(TestData.IntField) );
        var body = Expression.Block(
            Expression.Assign( field, Expression.Constant( 99 ) ),
            field );
        var lambda = Expression.Lambda<Func<TestData, int>>( body, obj );
        var fn = lambda.Compile( compilerType );

        var data = new TestData { IntField = 0 };
        Assert.AreEqual( 99, fn( data ) );
        Assert.AreEqual( 99, data.IntField );
    }

    // --- Instance property read (custom class) ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Property_Instance_CustomClass( CompilerType compilerType )
    {
        var obj = Expression.Parameter( typeof(TestData), "obj" );
        var prop = typeof(TestData).GetProperty( nameof(TestData.Name) )!;
        var body = Expression.Property( obj, prop );
        var lambda = Expression.Lambda<Func<TestData, string>>( body, obj );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( "hello", fn( new TestData { Name = "hello" } ) );
        Assert.IsNull( fn( new TestData { Name = null } ) );
    }

    // --- Instance property write ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Property_Instance_Write( CompilerType compilerType )
    {
        // (obj) => { obj.Name = "updated"; return obj.Name; }
        var obj = Expression.Parameter( typeof(TestData), "obj" );
        var prop = Expression.Property( obj, nameof(TestData.Name) );
        var body = Expression.Block(
            Expression.Assign( prop, Expression.Constant( "updated" ) ),
            prop );
        var lambda = Expression.Lambda<Func<TestData, string>>( body, obj );
        var fn = lambda.Compile( compilerType );

        var data = new TestData { Name = "original" };
        Assert.AreEqual( "updated", fn( data ) );
        Assert.AreEqual( "updated", data.Name );
    }

    // --- Static field read ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Field_Static_Read( CompilerType compilerType )
    {
        TestData.StaticIntField = 123;

        var field = typeof(TestData).GetField( nameof(TestData.StaticIntField) )!;
        var body = Expression.Field( null, field );
        var lambda = Expression.Lambda<Func<int>>( body );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 123, fn() );
    }

    // --- Static field write ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Field_Static_Write( CompilerType compilerType )
    {
        TestData.StaticIntField = 0;

        var field = Expression.Field( null, typeof(TestData), nameof(TestData.StaticIntField) );
        var body = Expression.Block(
            Expression.Assign( field, Expression.Constant( 456 ) ),
            field );
        var lambda = Expression.Lambda<Func<int>>( body );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 456, fn() );
        Assert.AreEqual( 456, TestData.StaticIntField );
    }

    // --- Value type property (constrained callvirt) ---
    // This validates the Phase 6 constrained callvirt fix for property access

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Property_ValueType_DateTimeYear( CompilerType compilerType )
    {
        var dt = Expression.Parameter( typeof(DateTime), "dt" );
        var prop = typeof(DateTime).GetProperty( nameof(DateTime.Year) )!;
        var body = Expression.Property( dt, prop );
        var lambda = Expression.Lambda<Func<DateTime, int>>( body, dt );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 2025, fn( new DateTime( 2025, 6, 15 ) ) );
        Assert.AreEqual( 2000, fn( new DateTime( 2000, 1, 1 ) ) );
        Assert.AreEqual( 1, fn( DateTime.MinValue ) );
    }

    // --- Nested member access (obj.Inner.Value) ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Property_Nested_Access( CompilerType compilerType )
    {
        var obj = Expression.Parameter( typeof(TestData), "obj" );
        var innerProp = typeof(TestData).GetProperty( nameof(TestData.Inner) )!;
        var valueProp = typeof(InnerData).GetProperty( nameof(InnerData.Value) )!;
        var body = Expression.Property( Expression.Property( obj, innerProp ), valueProp );
        var lambda = Expression.Lambda<Func<TestData, int>>( body, obj );
        var fn = lambda.Compile( compilerType );

        var data = new TestData { Inner = new InnerData { Value = 42 } };
        Assert.AreEqual( 42, fn( data ) );

        data.Inner = new InnerData { Value = -1 };
        Assert.AreEqual( -1, fn( data ) );
    }

    // --- Readonly field read ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Field_Readonly_Read( CompilerType compilerType )
    {
        var field = typeof(TestData).GetField( nameof(TestData.ReadonlyField) )!;
        var obj = Expression.Parameter( typeof(TestData), "obj" );
        var body = Expression.Field( obj, field );
        var lambda = Expression.Lambda<Func<TestData, string>>( body, obj );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( "readonly", fn( new TestData() ) );
    }

    // --- Property write then read ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Property_WriteAndRead_ViaBlock( CompilerType compilerType )
    {
        var obj = Expression.Parameter( typeof( TestData ), "obj" );
        var nameProp = typeof( TestData ).GetProperty( nameof( TestData.Name ) )!;

        var body = Expression.Block(
            Expression.Assign( Expression.Property( obj, nameProp ), Expression.Constant( "written" ) ),
            Expression.Property( obj, nameProp ) );

        var lambda = Expression.Lambda<Func<TestData, string>>( body, obj );
        var fn = lambda.Compile( compilerType );

        var data = new TestData();
        Assert.AreEqual( "written", fn( data ) );
        Assert.AreEqual( "written", data.Name );
    }

    // --- Field write then read ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Field_WriteAndRead_ViaBlock( CompilerType compilerType )
    {
        var obj = Expression.Parameter( typeof( TestData ), "obj" );
        var field = typeof( TestData ).GetField( nameof( TestData.IntField ) )!;

        var body = Expression.Block(
            Expression.Assign( Expression.Field( obj, field ), Expression.Constant( 77 ) ),
            Expression.Field( obj, field ) );

        var lambda = Expression.Lambda<Func<TestData, int>>( body, obj );
        var fn = lambda.Compile( compilerType );

        var data = new TestData();
        Assert.AreEqual( 77, fn( data ) );
        Assert.AreEqual( 77, data.IntField );
    }

    // --- Static property — Environment.NewLine ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Property_Static_EnvironmentNewLine( CompilerType compilerType )
    {
        var prop = typeof( Environment ).GetProperty( nameof( Environment.NewLine ) )!;
        var body = Expression.Property( null, prop );
        var lambda = Expression.Lambda<Func<string>>( body );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( Environment.NewLine, fn() );
    }

    // --- Array Length property ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Property_Array_Length( CompilerType compilerType )
    {
        var arr = Expression.Parameter( typeof( int[] ), "arr" );
        var lengthProp = typeof( int[] ).GetProperty( "Length" )!;
        var lambda = Expression.Lambda<Func<int[], int>>( Expression.Property( arr, lengthProp ), arr );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 3, fn( [1, 2, 3] ) );
        Assert.AreEqual( 0, fn( [] ) );
        Assert.AreEqual( 1, fn( [42] ) );
    }

    // --- String.Length property ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Property_String_Length( CompilerType compilerType )
    {
        var s = Expression.Parameter( typeof( string ), "s" );
        var lengthProp = typeof( string ).GetProperty( nameof( string.Length ) )!;
        var lambda = Expression.Lambda<Func<string, int>>( Expression.Property( s, lengthProp ), s );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 5, fn( "hello" ) );
        Assert.AreEqual( 0, fn( "" ) );
    }

    // --- Type.EmptyTypes field (static readonly array) ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Field_Static_TypeEmptyTypes( CompilerType compilerType )
    {
        var field = typeof( Type ).GetField( "EmptyTypes" )!;
        var lambda = Expression.Lambda<Func<Type[]>>( Expression.Field( null, field ) );
        var result = lambda.Compile( compilerType )();
        Assert.IsNotNull( result );
        Assert.AreEqual( 0, result.Length );
    }

    // --- Nested property — string length of name ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Property_NestedPropertyChain_StringLength( CompilerType compilerType )
    {
        var obj = Expression.Parameter( typeof( TestData ), "obj" );
        var nameProp = typeof( TestData ).GetProperty( nameof( TestData.Name ) )!;
        var lengthProp = typeof( string ).GetProperty( nameof( string.Length ) )!;

        var body = Expression.Property( Expression.Property( obj, nameProp ), lengthProp );
        var lambda = Expression.Lambda<Func<TestData, int>>( body, obj );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 5, fn( new TestData { Name = "hello" } ) );
        Assert.AreEqual( 0, fn( new TestData { Name = "" } ) );
    }

    // Test data classes

    public class TestData
    {
        public int IntField;
        public static int StaticIntField;
        public readonly string ReadonlyField = "readonly";
        public string? Name { get; set; }
        public InnerData? Inner { get; set; }
    }

    public class InnerData
    {
        public int Value { get; set; }
    }
}
