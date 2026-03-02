using System.Linq.Expressions;
using Hyperbee.Expressions.Compiler.Tests.TestSupport;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Hyperbee.Expressions.Compiler.Tests.Expressions;

[TestClass]
public class BinaryTests
{
    // --- Add (int) ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Add_Int_BoundaryValues( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(int), "a" );
        var b = Expression.Parameter( typeof(int), "b" );
        var lambda = Expression.Lambda<Func<int, int, int>>( Expression.Add( a, b ), a, b );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 0, fn( 0, 0 ) );
        Assert.AreEqual( 1, fn( 0, 1 ) );
        Assert.AreEqual( -1, fn( 0, -1 ) );
        Assert.AreEqual( 2, fn( 1, 1 ) );
    }

    // --- Add (long) ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Add_Long_BoundaryValues( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(long), "a" );
        var b = Expression.Parameter( typeof(long), "b" );
        var lambda = Expression.Lambda<Func<long, long, long>>( Expression.Add( a, b ), a, b );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 0L, fn( 0L, 0L ) );
        Assert.AreEqual( 1L, fn( 0L, 1L ) );
        Assert.AreEqual( -1L, fn( 0L, -1L ) );
        Assert.AreEqual( long.MaxValue, fn( long.MaxValue - 1L, 1L ) );
    }

    // --- Add (float) ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Add_Float_BoundaryValues( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(float), "a" );
        var b = Expression.Parameter( typeof(float), "b" );
        var lambda = Expression.Lambda<Func<float, float, float>>( Expression.Add( a, b ), a, b );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 0f, fn( 0f, 0f ) );
        Assert.AreEqual( 1f, fn( 0f, 1f ) );
        Assert.AreEqual( -1f, fn( 0f, -1f ) );
        Assert.AreEqual( 2f, fn( 1f, 1f ) );
    }

    // --- Add (double) ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Add_Double_BoundaryValues( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(double), "a" );
        var b = Expression.Parameter( typeof(double), "b" );
        var lambda = Expression.Lambda<Func<double, double, double>>( Expression.Add( a, b ), a, b );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 0.0, fn( 0.0, 0.0 ) );
        Assert.AreEqual( 1.0, fn( 0.0, 1.0 ) );
        Assert.AreEqual( -1.0, fn( 0.0, -1.0 ) );
        Assert.AreEqual( double.MaxValue, fn( double.MaxValue - 1.0, 1.0 ) );
    }

    // --- Subtract (int) ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Subtract_Int( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(int), "a" );
        var b = Expression.Parameter( typeof(int), "b" );
        var lambda = Expression.Lambda<Func<int, int, int>>( Expression.Subtract( a, b ), a, b );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 0, fn( 0, 0 ) );
        Assert.AreEqual( 1, fn( 2, 1 ) );
        Assert.AreEqual( -1, fn( 0, 1 ) );
        Assert.AreEqual( int.MinValue, fn( int.MinValue, 0 ) );
    }

    // --- Multiply (int) ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Multiply_Int( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(int), "a" );
        var b = Expression.Parameter( typeof(int), "b" );
        var lambda = Expression.Lambda<Func<int, int, int>>( Expression.Multiply( a, b ), a, b );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 0, fn( 0, 0 ) );
        Assert.AreEqual( 0, fn( 0, 5 ) );
        Assert.AreEqual( 6, fn( 2, 3 ) );
        Assert.AreEqual( -6, fn( 2, -3 ) );
        Assert.AreEqual( 1, fn( 1, 1 ) );
    }

    // --- Divide (int) ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Divide_Int( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(int), "a" );
        var b = Expression.Parameter( typeof(int), "b" );
        var lambda = Expression.Lambda<Func<int, int, int>>( Expression.Divide( a, b ), a, b );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 0, fn( 0, 1 ) );
        Assert.AreEqual( 2, fn( 6, 3 ) );
        Assert.AreEqual( -2, fn( 6, -3 ) );
        Assert.AreEqual( 1, fn( 1, 1 ) );
    }

    // --- Modulo (int) ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Modulo_Int( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(int), "a" );
        var b = Expression.Parameter( typeof(int), "b" );
        var lambda = Expression.Lambda<Func<int, int, int>>( Expression.Modulo( a, b ), a, b );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 0, fn( 0, 3 ) );
        Assert.AreEqual( 1, fn( 7, 3 ) );
        Assert.AreEqual( 0, fn( 6, 3 ) );
        Assert.AreEqual( 2, fn( 2, 5 ) );
    }

    // --- AddChecked (int) — overflow throws ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void AddChecked_Int_Overflow( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(int), "a" );
        var b = Expression.Parameter( typeof(int), "b" );
        var lambda = Expression.Lambda<Func<int, int, int>>( Expression.AddChecked( a, b ), a, b );
        var fn = lambda.Compile( compilerType );

        // Normal addition should work
        Assert.AreEqual( 3, fn( 1, 2 ) );

        // Overflow should throw OverflowException
        var threw = false;
        try { fn( int.MaxValue, 1 ); }
        catch ( OverflowException ) { threw = true; }
        Assert.IsTrue( threw, "Expected OverflowException from AddChecked overflow." );
    }

    // --- SubtractChecked (int) — overflow throws ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void SubtractChecked_Int_Overflow( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(int), "a" );
        var b = Expression.Parameter( typeof(int), "b" );
        var lambda = Expression.Lambda<Func<int, int, int>>( Expression.SubtractChecked( a, b ), a, b );
        var fn = lambda.Compile( compilerType );

        // Normal subtraction should work
        Assert.AreEqual( 1, fn( 3, 2 ) );

        // Overflow should throw OverflowException
        var threw = false;
        try { fn( int.MinValue, 1 ); }
        catch ( OverflowException ) { threw = true; }
        Assert.IsTrue( threw, "Expected OverflowException from SubtractChecked overflow." );
    }

    // --- MultiplyChecked (int) — overflow throws ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void MultiplyChecked_Int_Overflow( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(int), "a" );
        var b = Expression.Parameter( typeof(int), "b" );
        var lambda = Expression.Lambda<Func<int, int, int>>( Expression.MultiplyChecked( a, b ), a, b );
        var fn = lambda.Compile( compilerType );

        // Normal multiplication should work
        Assert.AreEqual( 6, fn( 2, 3 ) );

        // Overflow should throw OverflowException
        var threw = false;
        try { fn( int.MaxValue, 2 ); }
        catch ( OverflowException ) { threw = true; }
        Assert.IsTrue( threw, "Expected OverflowException from MultiplyChecked overflow." );
    }

    // --- Decimal operator overload path (node.Method != null) ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Add_Decimal_OperatorOverload( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(decimal), "a" );
        var b = Expression.Parameter( typeof(decimal), "b" );

        // Expression.Add for decimal uses operator overload method; node.Method != null
        var node = Expression.Add( a, b );
        Assert.IsNotNull( node.Method, "Expected decimal Add to use an operator overload method." );

        var lambda = Expression.Lambda<Func<decimal, decimal, decimal>>( node, a, b );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 3.0m, fn( 1.0m, 2.0m ) );
        Assert.AreEqual( 0.0m, fn( 1.5m, -1.5m ) );
        Assert.AreEqual( decimal.MaxValue, fn( decimal.MaxValue - 1.0m, 1.0m ) );
    }

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Multiply_Decimal_OperatorOverload( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(decimal), "a" );
        var b = Expression.Parameter( typeof(decimal), "b" );

        var node = Expression.Multiply( a, b );
        Assert.IsNotNull( node.Method, "Expected decimal Multiply to use an operator overload method." );

        var lambda = Expression.Lambda<Func<decimal, decimal, decimal>>( node, a, b );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 6.0m, fn( 2.0m, 3.0m ) );
        Assert.AreEqual( 0.0m, fn( 0.0m, 999.0m ) );
    }
}
