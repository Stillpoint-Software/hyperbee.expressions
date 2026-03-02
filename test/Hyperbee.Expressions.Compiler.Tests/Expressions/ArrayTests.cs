using System.Linq.Expressions;
using Hyperbee.Expressions.Compiler.Tests.TestSupport;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Hyperbee.Expressions.Compiler.Tests.Expressions;

[TestClass]
public class ArrayTests
{
    // ================================================================
    // NewArrayInit
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public void NewArrayInit_IntArray_ReturnsCorrectArray( CompilerType compilerType )
    {
        // () => new int[] { 1, 2, 3 }
        var lambda = Expression.Lambda<Func<int[]>>(
            Expression.NewArrayInit( typeof( int ),
                Expression.Constant( 1 ),
                Expression.Constant( 2 ),
                Expression.Constant( 3 ) ) );

        var fn = lambda.Compile( compilerType );
        var result = fn();

        Assert.AreEqual( 3, result.Length );
        Assert.AreEqual( 1, result[0] );
        Assert.AreEqual( 2, result[1] );
        Assert.AreEqual( 3, result[2] );
    }

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public void NewArrayInit_StringArray_ReturnsCorrectArray( CompilerType compilerType )
    {
        // () => new string[] { "a", "b" }
        var lambda = Expression.Lambda<Func<string[]>>(
            Expression.NewArrayInit( typeof( string ),
                Expression.Constant( "a" ),
                Expression.Constant( "b" ) ) );

        var fn = lambda.Compile( compilerType );
        var result = fn();

        Assert.AreEqual( 2, result.Length );
        Assert.AreEqual( "a", result[0] );
        Assert.AreEqual( "b", result[1] );
    }

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public void NewArrayInit_EmptyArray( CompilerType compilerType )
    {
        // () => new int[0] {}
        var lambda = Expression.Lambda<Func<int[]>>(
            Expression.NewArrayInit( typeof( int ) ) );

        var fn = lambda.Compile( compilerType );
        var result = fn();

        Assert.AreEqual( 0, result.Length );
    }

    // ================================================================
    // NewArrayBounds (1D)
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public void NewArrayBounds_1D_CreatesArrayOfSpecifiedSize( CompilerType compilerType )
    {
        // () => new int[5]
        var lambda = Expression.Lambda<Func<int[]>>(
            Expression.NewArrayBounds( typeof( int ),
                Expression.Constant( 5 ) ) );

        var fn = lambda.Compile( compilerType );
        var result = fn();

        Assert.AreEqual( 5, result.Length );
        Assert.AreEqual( 0, result[0] ); // default int
    }

    // ================================================================
    // Array element access (ArrayIndex binary expression)
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public void ArrayIndex_ReadsElement( CompilerType compilerType )
    {
        // (int[] arr) => arr[1]
        var arr = Expression.Parameter( typeof( int[] ), "arr" );
        var lambda = Expression.Lambda<Func<int[], int>>(
            Expression.ArrayIndex( arr, Expression.Constant( 1 ) ),
            arr );

        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 20, fn( new[] { 10, 20, 30 } ) );
    }

    // ================================================================
    // Array length
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public void ArrayLength_ReturnsLength( CompilerType compilerType )
    {
        // (int[] arr) => arr.Length
        var arr = Expression.Parameter( typeof( int[] ), "arr" );
        var lambda = Expression.Lambda<Func<int[], int>>(
            Expression.ArrayLength( arr ),
            arr );

        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 0, fn( Array.Empty<int>() ) );
        Assert.AreEqual( 3, fn( new[] { 1, 2, 3 } ) );
        Assert.AreEqual( 5, fn( new int[5] ) );
    }

    // ================================================================
    // Index expression (array access via IndexExpression)
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public void IndexExpression_Array_ReadsElement( CompilerType compilerType )
    {
        // (int[] arr) => arr[2]
        var arr = Expression.Parameter( typeof( int[] ), "arr" );
        var lambda = Expression.Lambda<Func<int[], int>>(
            Expression.MakeIndex( arr, null, new[] { Expression.Constant( 2 ) } ),
            arr );

        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 30, fn( new[] { 10, 20, 30 } ) );
    }

    // ================================================================
    // Index expression (indexer property)
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public void IndexExpression_ListIndexer_ReadsElement( CompilerType compilerType )
    {
        // (List<int> list) => list[1]
        var list = Expression.Parameter( typeof( List<int> ), "list" );
        var indexer = typeof( List<int> ).GetProperty( "Item" )!;
        var lambda = Expression.Lambda<Func<List<int>, int>>(
            Expression.MakeIndex( list, indexer, new[] { Expression.Constant( 1 ) } ),
            list );

        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 20, fn( new List<int> { 10, 20, 30 } ) );
    }

    // ================================================================
    // Create array, set element, read element
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public void Array_CreateSetRead_RoundTrip( CompilerType compilerType )
    {
        // var arr = new int[3];
        // arr[1] = 42;
        // return arr[1];
        var arr = Expression.Variable( typeof( int[] ), "arr" );
        var body = Expression.Block(
            new[] { arr },
            Expression.Assign( arr, Expression.NewArrayBounds( typeof( int ), Expression.Constant( 3 ) ) ),
            Expression.Assign(
                Expression.ArrayAccess( arr, Expression.Constant( 1 ) ),
                Expression.Constant( 42 ) ),
            Expression.ArrayIndex( arr, Expression.Constant( 1 ) ) );

        var lambda = Expression.Lambda<Func<int>>( body );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 42, fn() );
    }
}
