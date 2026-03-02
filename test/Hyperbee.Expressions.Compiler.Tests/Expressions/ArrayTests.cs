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

    // ================================================================
    // NewArrayBounds — 2D array
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void NewArrayBounds_2D_CreatesCorrectDimensions( CompilerType compilerType )
    {
        // new int[3, 4]
        var lambda = Expression.Lambda<Func<int[,]>>(
            Expression.NewArrayBounds( typeof( int ),
                Expression.Constant( 3 ),
                Expression.Constant( 4 ) ) );

        var fn = lambda.Compile( compilerType );
        var result = fn();

        Assert.AreEqual( 3, result.GetLength( 0 ) );
        Assert.AreEqual( 4, result.GetLength( 1 ) );
        Assert.AreEqual( 12, result.Length );
    }

    // ================================================================
    // 2D array element read via ArrayAccess
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void ArrayAccess_2D_ReadElement( CompilerType compilerType )
    {
        // (int[,] arr) => arr[1, 2]
        var arr = Expression.Parameter( typeof( int[,] ), "arr" );
        var access = Expression.ArrayAccess( arr, Expression.Constant( 1 ), Expression.Constant( 2 ) );
        var lambda = Expression.Lambda<Func<int[,], int>>( access, arr );
        var fn = lambda.Compile( compilerType );

        var matrix = new int[3, 3];
        matrix[1, 2] = 99;
        Assert.AreEqual( 99, fn( matrix ) );
        Assert.AreEqual( 0, fn( new int[3, 3] ) );
    }

    // ================================================================
    // 2D array element write via ArrayAccess assignment
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void ArrayAccess_2D_WriteElement( CompilerType compilerType )
    {
        // var arr = new int[2, 2]; arr[0, 1] = 77; return arr[0, 1];
        var arr = Expression.Variable( typeof( int[,] ), "arr" );
        var access = Expression.ArrayAccess( arr, Expression.Constant( 0 ), Expression.Constant( 1 ) );

        var body = Expression.Block(
            new[] { arr },
            Expression.Assign( arr, Expression.NewArrayBounds( typeof( int ), Expression.Constant( 2 ), Expression.Constant( 2 ) ) ),
            Expression.Assign( access, Expression.Constant( 77 ) ),
            access );

        var lambda = Expression.Lambda<Func<int>>( body );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 77, fn() );
    }

    // ================================================================
    // Jagged array — create and access
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void JaggedArray_CreateAndAccess( CompilerType compilerType )
    {
        // (int[][] arr) => arr[1][0]
        var arr = Expression.Parameter( typeof( int[][] ), "arr" );
        var inner = Expression.ArrayIndex( arr, Expression.Constant( 1 ) );
        var element = Expression.ArrayIndex( inner, Expression.Constant( 0 ) );

        var lambda = Expression.Lambda<Func<int[][], int>>( element, arr );
        var fn = lambda.Compile( compilerType );

        var jagged = new[] { new[] { 1, 2 }, new[] { 10, 20 } };
        Assert.AreEqual( 10, fn( jagged ) );
    }

    // ================================================================
    // NewArrayInit — bool array
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void NewArrayInit_BoolArray( CompilerType compilerType )
    {
        var lambda = Expression.Lambda<Func<bool[]>>(
            Expression.NewArrayInit( typeof( bool ),
                Expression.Constant( true ),
                Expression.Constant( false ),
                Expression.Constant( true ) ) );

        var fn = lambda.Compile( compilerType );
        var result = fn();

        Assert.AreEqual( 3, result.Length );
        Assert.IsTrue( result[0] );
        Assert.IsFalse( result[1] );
        Assert.IsTrue( result[2] );
    }

    // ================================================================
    // NewArrayInit — double array
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void NewArrayInit_DoubleArray( CompilerType compilerType )
    {
        var lambda = Expression.Lambda<Func<double[]>>(
            Expression.NewArrayInit( typeof( double ),
                Expression.Constant( 1.5 ),
                Expression.Constant( 2.5 ),
                Expression.Constant( 3.0 ) ) );

        var fn = lambda.Compile( compilerType );
        var result = fn();

        Assert.AreEqual( 3, result.Length );
        Assert.AreEqual( 1.5, result[0], 1e-9 );
        Assert.AreEqual( 2.5, result[1], 1e-9 );
        Assert.AreEqual( 3.0, result[2], 1e-9 );
    }

    // ================================================================
    // Array length of empty array
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void ArrayLength_EmptyArray_IsZero( CompilerType compilerType )
    {
        var arr = Expression.Parameter( typeof( string[] ), "arr" );
        var lambda = Expression.Lambda<Func<string[], int>>( Expression.ArrayLength( arr ), arr );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 0, fn( [] ) );
    }

    // ================================================================
    // Array read inside loop
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void ArrayAccess_ReadInsideLoop( CompilerType compilerType )
    {
        // Find max element in array via loop
        var arr = Expression.Parameter( typeof( int[] ), "arr" );
        var max = Expression.Variable( typeof( int ), "max" );
        var i = Expression.Variable( typeof( int ), "i" );
        var breakLabel = Expression.Label( "break" );
        var lengthProp = typeof( int[] ).GetProperty( "Length" )!;

        var loop = Expression.Loop(
            Expression.Block(
                Expression.IfThen(
                    Expression.GreaterThanOrEqual( i, Expression.Property( arr, lengthProp ) ),
                    Expression.Break( breakLabel ) ),
                Expression.IfThen(
                    Expression.GreaterThan( Expression.ArrayIndex( arr, i ), max ),
                    Expression.Assign( max, Expression.ArrayIndex( arr, i ) ) ),
                Expression.PostIncrementAssign( i ) ),
            breakLabel );

        var body = Expression.Block(
            new[] { max, i },
            Expression.Assign( max, Expression.Constant( int.MinValue ) ),
            Expression.Assign( i, Expression.Constant( 0 ) ),
            loop,
            max );

        var lambda = Expression.Lambda<Func<int[], int>>( body, arr );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 9, fn( [3, 9, 1, 7, 2] ) );
        Assert.AreEqual( 1, fn( [1] ) );
    }

    // ================================================================
    // 3D array — create and read
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void ArrayAccess_3D_ReadElement( CompilerType compilerType )
    {
        // (int[,,] arr) => arr[1, 0, 2]
        var arr = Expression.Parameter( typeof( int[,,] ), "arr" );
        var access = Expression.ArrayAccess( arr,
            Expression.Constant( 1 ),
            Expression.Constant( 0 ),
            Expression.Constant( 2 ) );

        var lambda = Expression.Lambda<Func<int[,,], int>>( access, arr );
        var fn = lambda.Compile( compilerType );

        var cube = new int[2, 2, 3];
        cube[1, 0, 2] = 55;
        Assert.AreEqual( 55, fn( cube ) );
    }

    // ================================================================
    // Array element assignment inside try/catch
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void ArrayAccess_AssignInsideTryCatch( CompilerType compilerType )
    {
        // var arr = new int[3];
        // try { arr[0]=10; arr[1]=20; arr[2]=30; } catch { }
        // return arr[1];
        var arr = Expression.Variable( typeof(int[]), "arr" );
        var lambda = Expression.Lambda<Func<int>>(
            Expression.Block(
                new[] { arr },
                Expression.Assign( arr, Expression.NewArrayBounds( typeof(int), Expression.Constant( 3 ) ) ),
                Expression.TryCatch(
                    Expression.Block(
                        Expression.Assign( Expression.ArrayAccess( arr, Expression.Constant( 0 ) ), Expression.Constant( 10 ) ),
                        Expression.Assign( Expression.ArrayAccess( arr, Expression.Constant( 1 ) ), Expression.Constant( 20 ) ),
                        Expression.Assign( Expression.ArrayAccess( arr, Expression.Constant( 2 ) ), Expression.Constant( 30 ) ),
                        Expression.Constant( 0 ) ),
                    Expression.Catch( typeof(Exception), Expression.Constant( -1 ) ) ),
                Expression.ArrayIndex( arr, Expression.Constant( 1 ) ) ) );

        var fn = lambda.Compile( compilerType );
        Assert.AreEqual( 20, fn() );
    }
}
