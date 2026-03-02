using System.Linq.Expressions;
using Hyperbee.Expressions.Compiler.Tests.TestSupport;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Hyperbee.Expressions.Compiler.Tests.Expressions;

[TestClass]
public class ConstructorTests
{
    public class SimpleClass
    {
        public int Value { get; set; }
    }

    public class ParamClass
    {
        public int X { get; }
        public string Name { get; }

        public ParamClass( int x, string name )
        {
            X = x;
            Name = name;
        }
    }

    public struct SimpleStruct
    {
        public int Value;

        public SimpleStruct( int value )
        {
            Value = value;
        }
    }

    // --- New: default constructor ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void New_DefaultConstructor( CompilerType compilerType )
    {
        var newExpr = Expression.New( typeof(SimpleClass) );
        var lambda = Expression.Lambda<Func<SimpleClass>>( newExpr );
        var fn = lambda.Compile( compilerType );

        var result = fn();
        Assert.IsNotNull( result );
        Assert.AreEqual( 0, result.Value );
    }

    // --- New: parameterized constructor ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void New_ParameterizedConstructor( CompilerType compilerType )
    {
        var ctor = typeof(ParamClass).GetConstructor( new[] { typeof(int), typeof(string) } )!;
        var newExpr = Expression.New( ctor, Expression.Constant( 42 ), Expression.Constant( "hello" ) );
        var lambda = Expression.Lambda<Func<ParamClass>>( newExpr );
        var fn = lambda.Compile( compilerType );

        var result = fn();
        Assert.AreEqual( 42, result.X );
        Assert.AreEqual( "hello", result.Name );
    }

    // --- New: parameterized constructor with dynamic args ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void New_ParameterizedConstructor_DynamicArgs( CompilerType compilerType )
    {
        var x = Expression.Parameter( typeof(int), "x" );
        var name = Expression.Parameter( typeof(string), "name" );
        var ctor = typeof(ParamClass).GetConstructor( new[] { typeof(int), typeof(string) } )!;
        var newExpr = Expression.New( ctor, x, name );
        var lambda = Expression.Lambda<Func<int, string, ParamClass>>( newExpr, x, name );
        var fn = lambda.Compile( compilerType );

        var result = fn( 99, "world" );
        Assert.AreEqual( 99, result.X );
        Assert.AreEqual( "world", result.Name );
    }

    // --- New: struct constructor ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void New_StructConstructor( CompilerType compilerType )
    {
        var ctor = typeof(SimpleStruct).GetConstructor( new[] { typeof(int) } )!;
        var newExpr = Expression.New( ctor, Expression.Constant( 42 ) );
        // Box the struct to return as object to avoid managed pointer issues
        var boxed = Expression.Convert( newExpr, typeof(object) );
        var lambda = Expression.Lambda<Func<object>>( boxed );
        var fn = lambda.Compile( compilerType );

        var result = (SimpleStruct) fn();
        Assert.AreEqual( 42, result.Value );
    }

    // --- New: struct default value ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void New_StructDefault( CompilerType compilerType )
    {
        var newExpr = Expression.New( typeof(SimpleStruct) );
        var boxed = Expression.Convert( newExpr, typeof(object) );
        var lambda = Expression.Lambda<Func<object>>( boxed );
        var fn = lambda.Compile( compilerType );

        var result = (SimpleStruct) fn();
        Assert.AreEqual( 0, result.Value );
    }

    // --- New: List<int> then use it ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void New_ListOfInt( CompilerType compilerType )
    {
        var newExpr = Expression.New( typeof(List<int>) );
        var lambda = Expression.Lambda<Func<List<int>>>( newExpr );
        var fn = lambda.Compile( compilerType );

        var result = fn();
        Assert.IsNotNull( result );
        Assert.AreEqual( 0, result.Count );
    }

    // --- New: constructor with capacity arg ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void New_ListOfInt_WithCapacity( CompilerType compilerType )
    {
        var ctor = typeof(List<int>).GetConstructor( new[] { typeof(int) } )!;
        var newExpr = Expression.New( ctor, Expression.Constant( 10 ) );
        var capacity = Expression.Property( newExpr, "Capacity" );
        var lambda = Expression.Lambda<Func<int>>( capacity );
        var fn = lambda.Compile( compilerType );

        Assert.IsTrue( fn() >= 10 );
    }

    // --- New: then assign to variable and use ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void New_AssignToVariable_ThenReadProperty( CompilerType compilerType )
    {
        var obj = Expression.Variable( typeof(SimpleClass), "obj" );
        var body = Expression.Block(
            new[] { obj },
            Expression.Assign( obj, Expression.New( typeof(SimpleClass) ) ),
            Expression.Assign(
                Expression.Property( obj, "Value" ),
                Expression.Constant( 42 )
            ),
            Expression.Property( obj, "Value" )
        );
        var lambda = Expression.Lambda<Func<int>>( body );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 42, fn() );
    }

    // --- NewArrayInit ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void NewArrayInit_IntArray( CompilerType compilerType )
    {
        var array = Expression.NewArrayInit( typeof(int),
            Expression.Constant( 1 ),
            Expression.Constant( 2 ),
            Expression.Constant( 3 )
        );
        var lambda = Expression.Lambda<Func<int[]>>( array );
        var fn = lambda.Compile( compilerType );

        var result = fn();
        CollectionAssert.AreEqual( new[] { 1, 2, 3 }, result );
    }

    // --- NewArrayBounds ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void NewArrayBounds_IntArray( CompilerType compilerType )
    {
        var array = Expression.NewArrayBounds( typeof(int), Expression.Constant( 5 ) );
        var lambda = Expression.Lambda<Func<int[]>>( array );
        var fn = lambda.Compile( compilerType );

        var result = fn();
        Assert.AreEqual( 5, result.Length );
        Assert.AreEqual( 0, result[0] );
    }
}
