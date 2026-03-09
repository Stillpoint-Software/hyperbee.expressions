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

    // --- Subtract (long) ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Subtract_Long( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(long), "a" );
        var b = Expression.Parameter( typeof(long), "b" );
        var lambda = Expression.Lambda<Func<long, long, long>>( Expression.Subtract( a, b ), a, b );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 3L, fn( 10L, 7L ) );
        Assert.AreEqual( -1L, fn( 0L, 1L ) );
        Assert.AreEqual( long.MinValue, fn( long.MinValue, 0L ) );
    }

    // --- Multiply (long) ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Multiply_Long( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(long), "a" );
        var b = Expression.Parameter( typeof(long), "b" );
        var lambda = Expression.Lambda<Func<long, long, long>>( Expression.Multiply( a, b ), a, b );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 6L, fn( 2L, 3L ) );
        Assert.AreEqual( -6L, fn( -2L, 3L ) );
        Assert.AreEqual( 0L, fn( 0L, 100L ) );
    }

    // --- Add (uint) ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Add_UInt( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(uint), "a" );
        var b = Expression.Parameter( typeof(uint), "b" );
        var lambda = Expression.Lambda<Func<uint, uint, uint>>( Expression.Add( a, b ), a, b );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 3u, fn( 1u, 2u ) );
        Assert.AreEqual( 0u, fn( 0u, 0u ) );
        Assert.AreEqual( uint.MaxValue, fn( uint.MaxValue - 1u, 1u ) );
    }

    // --- Add (ulong) ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Add_ULong( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(ulong), "a" );
        var b = Expression.Parameter( typeof(ulong), "b" );
        var lambda = Expression.Lambda<Func<ulong, ulong, ulong>>( Expression.Add( a, b ), a, b );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 3UL, fn( 1UL, 2UL ) );
        Assert.AreEqual( 0UL, fn( 0UL, 0UL ) );
        Assert.AreEqual( ulong.MaxValue, fn( ulong.MaxValue - 1UL, 1UL ) );
    }

    // --- Subtract (uint) ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Subtract_UInt( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(uint), "a" );
        var b = Expression.Parameter( typeof(uint), "b" );
        var lambda = Expression.Lambda<Func<uint, uint, uint>>( Expression.Subtract( a, b ), a, b );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 3u, fn( 5u, 2u ) );
        Assert.AreEqual( 0u, fn( 0u, 0u ) );
        Assert.AreEqual( uint.MaxValue, fn( uint.MaxValue, 0u ) );
    }

    // --- Subtract (ulong) ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Subtract_ULong( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(ulong), "a" );
        var b = Expression.Parameter( typeof(ulong), "b" );
        var lambda = Expression.Lambda<Func<ulong, ulong, ulong>>( Expression.Subtract( a, b ), a, b );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 7UL, fn( 10UL, 3UL ) );
        Assert.AreEqual( 0UL, fn( 5UL, 5UL ) );
    }

    // --- Multiply (uint) ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Multiply_UInt( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(uint), "a" );
        var b = Expression.Parameter( typeof(uint), "b" );
        var lambda = Expression.Lambda<Func<uint, uint, uint>>( Expression.Multiply( a, b ), a, b );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 6u, fn( 2u, 3u ) );
        Assert.AreEqual( 0u, fn( 0u, 5u ) );
        Assert.AreEqual( 1u, fn( 1u, 1u ) );
    }

    // --- Multiply (ulong) ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Multiply_ULong( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(ulong), "a" );
        var b = Expression.Parameter( typeof(ulong), "b" );
        var lambda = Expression.Lambda<Func<ulong, ulong, ulong>>( Expression.Multiply( a, b ), a, b );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 12UL, fn( 3UL, 4UL ) );
        Assert.AreEqual( 0UL, fn( 0UL, 100UL ) );
    }

    // --- Divide (long) ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Divide_Long( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(long), "a" );
        var b = Expression.Parameter( typeof(long), "b" );
        var lambda = Expression.Lambda<Func<long, long, long>>( Expression.Divide( a, b ), a, b );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 3L, fn( 9L, 3L ) );
        Assert.AreEqual( -3L, fn( 9L, -3L ) );
        Assert.AreEqual( 0L, fn( 0L, 5L ) );
    }

    // --- Divide (uint) ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Divide_UInt( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(uint), "a" );
        var b = Expression.Parameter( typeof(uint), "b" );
        var lambda = Expression.Lambda<Func<uint, uint, uint>>( Expression.Divide( a, b ), a, b );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 3u, fn( 9u, 3u ) );
        Assert.AreEqual( 0u, fn( 2u, 3u ) );
        Assert.AreEqual( 1u, fn( 5u, 5u ) );
    }

    // --- Divide (ulong) ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Divide_ULong( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(ulong), "a" );
        var b = Expression.Parameter( typeof(ulong), "b" );
        var lambda = Expression.Lambda<Func<ulong, ulong, ulong>>( Expression.Divide( a, b ), a, b );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 4UL, fn( 8UL, 2UL ) );
        Assert.AreEqual( 0UL, fn( 3UL, 4UL ) );
    }

    // --- Modulo (long) ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Modulo_Long( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(long), "a" );
        var b = Expression.Parameter( typeof(long), "b" );
        var lambda = Expression.Lambda<Func<long, long, long>>( Expression.Modulo( a, b ), a, b );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 1L, fn( 7L, 3L ) );
        Assert.AreEqual( 0L, fn( 6L, 3L ) );
        Assert.AreEqual( -1L, fn( -7L, 3L ) );
    }

    // --- Modulo (uint) ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Modulo_UInt( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(uint), "a" );
        var b = Expression.Parameter( typeof(uint), "b" );
        var lambda = Expression.Lambda<Func<uint, uint, uint>>( Expression.Modulo( a, b ), a, b );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 1u, fn( 7u, 3u ) );
        Assert.AreEqual( 0u, fn( 6u, 3u ) );
        Assert.AreEqual( 2u, fn( 2u, 5u ) );
    }

    // --- Modulo (ulong) ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Modulo_ULong( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(ulong), "a" );
        var b = Expression.Parameter( typeof(ulong), "b" );
        var lambda = Expression.Lambda<Func<ulong, ulong, ulong>>( Expression.Modulo( a, b ), a, b );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 1UL, fn( 10UL, 3UL ) );
        Assert.AreEqual( 0UL, fn( 9UL, 3UL ) );
    }

    // --- AddChecked (long) — overflow throws ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void AddChecked_Long_Overflow( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(long), "a" );
        var b = Expression.Parameter( typeof(long), "b" );
        var lambda = Expression.Lambda<Func<long, long, long>>( Expression.AddChecked( a, b ), a, b );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 3L, fn( 1L, 2L ) );

        var threw = false;
        try { fn( long.MaxValue, 1L ); } catch ( OverflowException ) { threw = true; }
        Assert.IsTrue( threw, "Expected OverflowException from AddChecked long overflow." );
    }

    // --- AddChecked (uint) — unsigned overflow throws ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void AddChecked_UInt_Overflow( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(uint), "a" );
        var b = Expression.Parameter( typeof(uint), "b" );
        var lambda = Expression.Lambda<Func<uint, uint, uint>>( Expression.AddChecked( a, b ), a, b );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 3u, fn( 1u, 2u ) );

        var threw = false;
        try { fn( uint.MaxValue, 1u ); } catch ( OverflowException ) { threw = true; }
        Assert.IsTrue( threw, "Expected OverflowException from AddChecked uint overflow." );
    }

    // --- AddChecked (ulong) — unsigned overflow throws ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void AddChecked_ULong_Overflow( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(ulong), "a" );
        var b = Expression.Parameter( typeof(ulong), "b" );
        var lambda = Expression.Lambda<Func<ulong, ulong, ulong>>( Expression.AddChecked( a, b ), a, b );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 3UL, fn( 1UL, 2UL ) );

        var threw = false;
        try { fn( ulong.MaxValue, 1UL ); } catch ( OverflowException ) { threw = true; }
        Assert.IsTrue( threw, "Expected OverflowException from AddChecked ulong overflow." );
    }

    // --- MultiplyChecked (uint) — unsigned overflow throws ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void MultiplyChecked_UInt_Overflow( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(uint), "a" );
        var b = Expression.Parameter( typeof(uint), "b" );
        var lambda = Expression.Lambda<Func<uint, uint, uint>>( Expression.MultiplyChecked( a, b ), a, b );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 6u, fn( 2u, 3u ) );

        var threw = false;
        try { fn( uint.MaxValue, 2u ); } catch ( OverflowException ) { threw = true; }
        Assert.IsTrue( threw, "Expected OverflowException from MultiplyChecked uint overflow." );
    }

    // --- SubtractChecked (long) — overflow throws ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void SubtractChecked_Long_Overflow( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(long), "a" );
        var b = Expression.Parameter( typeof(long), "b" );
        var lambda = Expression.Lambda<Func<long, long, long>>( Expression.SubtractChecked( a, b ), a, b );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 1L, fn( 3L, 2L ) );

        var threw = false;
        try { fn( long.MinValue, 1L ); } catch ( OverflowException ) { threw = true; }
        Assert.IsTrue( threw, "Expected OverflowException from SubtractChecked long overflow." );
    }

    // --- MultiplyChecked (long) — overflow throws ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void MultiplyChecked_Long_Overflow( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(long), "a" );
        var b = Expression.Parameter( typeof(long), "b" );
        var lambda = Expression.Lambda<Func<long, long, long>>( Expression.MultiplyChecked( a, b ), a, b );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 6L, fn( 2L, 3L ) );

        var threw = false;
        try { fn( long.MaxValue, 2L ); } catch ( OverflowException ) { threw = true; }
        Assert.IsTrue( threw, "Expected OverflowException from MultiplyChecked long overflow." );
    }

    // --- Add (double) — special floating-point values ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Add_Double_SpecialValues( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(double), "a" );
        var b = Expression.Parameter( typeof(double), "b" );
        var lambda = Expression.Lambda<Func<double, double, double>>( Expression.Add( a, b ), a, b );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( double.PositiveInfinity, fn( double.MaxValue, double.MaxValue ) );
        Assert.IsTrue( double.IsNaN( fn( double.NaN, 1.0 ) ) );
        Assert.IsTrue( double.IsNaN( fn( 1.0, double.NaN ) ) );
        Assert.AreEqual( double.PositiveInfinity, fn( double.PositiveInfinity, 1.0 ) );
    }

    // ================================================================
    // Add — byte
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Add_Byte( CompilerType compilerType )
    {
        // byte arithmetic requires widening to int first (Expression API design)
        var a = Expression.Parameter( typeof( byte ), "a" );
        var b = Expression.Parameter( typeof( byte ), "b" );
        var add = Expression.Convert(
            Expression.Add( Expression.Convert( a, typeof( int ) ), Expression.Convert( b, typeof( int ) ) ),
            typeof( byte ) );
        var lambda = Expression.Lambda<Func<byte, byte, byte>>( add, a, b );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( (byte) 3, fn( 1, 2 ) );
        Assert.AreEqual( (byte) 255, fn( 200, 55 ) );
    }

    // ================================================================
    // Subtract — byte
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Subtract_Byte( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof( byte ), "a" );
        var b = Expression.Parameter( typeof( byte ), "b" );
        var sub = Expression.Convert(
            Expression.Subtract( Expression.Convert( a, typeof( int ) ), Expression.Convert( b, typeof( int ) ) ),
            typeof( byte ) );
        var lambda = Expression.Lambda<Func<byte, byte, byte>>( sub, a, b );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( (byte) 5, fn( 10, 5 ) );
        Assert.AreEqual( (byte) 0, fn( 42, 42 ) );
    }

    // ================================================================
    // Add — short
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Add_Short( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof( short ), "a" );
        var b = Expression.Parameter( typeof( short ), "b" );
        var add = Expression.Convert(
            Expression.Add( Expression.Convert( a, typeof( int ) ), Expression.Convert( b, typeof( int ) ) ),
            typeof( short ) );
        var lambda = Expression.Lambda<Func<short, short, short>>( add, a, b );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( (short) 300, fn( 100, 200 ) );
        Assert.AreEqual( (short) -100, fn( -50, -50 ) );
    }

    // ================================================================
    // Subtract — float
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Subtract_Float( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof( float ), "a" );
        var b = Expression.Parameter( typeof( float ), "b" );
        var lambda = Expression.Lambda<Func<float, float, float>>( Expression.Subtract( a, b ), a, b );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 1.0f, fn( 3.0f, 2.0f ), 1e-6f );
        Assert.AreEqual( -2.5f, fn( 0.5f, 3.0f ), 1e-6f );
    }

    // ================================================================
    // Multiply — float
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Multiply_Float( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof( float ), "a" );
        var b = Expression.Parameter( typeof( float ), "b" );
        var lambda = Expression.Lambda<Func<float, float, float>>( Expression.Multiply( a, b ), a, b );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 6.0f, fn( 2.0f, 3.0f ), 1e-6f );
        Assert.AreEqual( -4.0f, fn( 2.0f, -2.0f ), 1e-6f );
    }

    // ================================================================
    // Divide — float
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Divide_Float( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof( float ), "a" );
        var b = Expression.Parameter( typeof( float ), "b" );
        var lambda = Expression.Lambda<Func<float, float, float>>( Expression.Divide( a, b ), a, b );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 2.0f, fn( 6.0f, 3.0f ), 1e-6f );
        Assert.AreEqual( 0.5f, fn( 1.0f, 2.0f ), 1e-6f );
    }

    // ================================================================
    // Divide — double
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Divide_Double( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof( double ), "a" );
        var b = Expression.Parameter( typeof( double ), "b" );
        var lambda = Expression.Lambda<Func<double, double, double>>( Expression.Divide( a, b ), a, b );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 2.5, fn( 5.0, 2.0 ), 1e-9 );
        Assert.AreEqual( -1.0, fn( 3.0, -3.0 ), 1e-9 );
    }

    // ================================================================
    // Subtract — double
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Subtract_Double( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof( double ), "a" );
        var b = Expression.Parameter( typeof( double ), "b" );
        var lambda = Expression.Lambda<Func<double, double, double>>( Expression.Subtract( a, b ), a, b );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 1.5, fn( 3.0, 1.5 ), 1e-9 );
        Assert.AreEqual( -5.0, fn( -2.0, 3.0 ), 1e-9 );
    }

    // ================================================================
    // Multiply — double
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Multiply_Double( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof( double ), "a" );
        var b = Expression.Parameter( typeof( double ), "b" );
        var lambda = Expression.Lambda<Func<double, double, double>>( Expression.Multiply( a, b ), a, b );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 6.28, fn( 3.14, 2.0 ), 1e-9 );
        Assert.AreEqual( -0.5, fn( 0.5, -1.0 ), 1e-9 );
    }

    // ================================================================
    // Modulo — float
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Modulo_Float( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof( float ), "a" );
        var b = Expression.Parameter( typeof( float ), "b" );
        var lambda = Expression.Lambda<Func<float, float, float>>( Expression.Modulo( a, b ), a, b );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 1.0f, fn( 7.0f, 3.0f ), 1e-6f );
        Assert.AreEqual( 0.0f, fn( 6.0f, 2.0f ), 1e-6f );
    }

    // ================================================================
    // Add — ushort
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Add_UShort( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof( ushort ), "a" );
        var b = Expression.Parameter( typeof( ushort ), "b" );
        var add = Expression.Convert(
            Expression.Add( Expression.Convert( a, typeof( int ) ), Expression.Convert( b, typeof( int ) ) ),
            typeof( ushort ) );
        var lambda = Expression.Lambda<Func<ushort, ushort, ushort>>( add, a, b );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( (ushort) 500, fn( 200, 300 ) );
        Assert.AreEqual( (ushort) 0, fn( 0, 0 ) );
    }

    // ================================================================
    // Subtract — short
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Subtract_Short( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof( short ), "a" );
        var b = Expression.Parameter( typeof( short ), "b" );
        var sub = Expression.Convert(
            Expression.Subtract( Expression.Convert( a, typeof( int ) ), Expression.Convert( b, typeof( int ) ) ),
            typeof( short ) );
        var lambda = Expression.Lambda<Func<short, short, short>>( sub, a, b );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( (short) 100, fn( 300, 200 ) );
        Assert.AreEqual( (short) -50, fn( 50, 100 ) );
    }

    // ================================================================
    // Multiply — short
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Multiply_Short( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof( short ), "a" );
        var b = Expression.Parameter( typeof( short ), "b" );
        var mul = Expression.Convert(
            Expression.Multiply( Expression.Convert( a, typeof( int ) ), Expression.Convert( b, typeof( int ) ) ),
            typeof( short ) );
        var lambda = Expression.Lambda<Func<short, short, short>>( mul, a, b );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( (short) 6, fn( 2, 3 ) );
        Assert.AreEqual( (short) -100, fn( 10, -10 ) );
    }

    // ================================================================
    // Divide — decimal
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Divide_Decimal( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof( decimal ), "a" );
        var b = Expression.Parameter( typeof( decimal ), "b" );
        var lambda = Expression.Lambda<Func<decimal, decimal, decimal>>( Expression.Divide( a, b ), a, b );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 2.5m, fn( 5m, 2m ) );
        Assert.AreEqual( -3m, fn( 9m, -3m ) );
    }

    // ================================================================
    // Modulo — double
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Modulo_Double( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof( double ), "a" );
        var b = Expression.Parameter( typeof( double ), "b" );
        var lambda = Expression.Lambda<Func<double, double, double>>( Expression.Modulo( a, b ), a, b );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 1.0, fn( 7.0, 3.0 ), 1e-9 );
        Assert.AreEqual( 0.5, fn( 5.5, 2.5 ), 1e-9 );
    }

    // ================================================================
    // Add — sbyte
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Add_SByte( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof( sbyte ), "a" );
        var b = Expression.Parameter( typeof( sbyte ), "b" );
        var add = Expression.Convert(
            Expression.Add( Expression.Convert( a, typeof( int ) ), Expression.Convert( b, typeof( int ) ) ),
            typeof( sbyte ) );
        var lambda = Expression.Lambda<Func<sbyte, sbyte, sbyte>>( add, a, b );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( (sbyte) 3, fn( 1, 2 ) );
        Assert.AreEqual( (sbyte) -1, fn( -3, 2 ) );
    }

    // ================================================================
    // Subtract — decimal
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Subtract_Decimal( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof( decimal ), "a" );
        var b = Expression.Parameter( typeof( decimal ), "b" );
        var lambda = Expression.Lambda<Func<decimal, decimal, decimal>>( Expression.Subtract( a, b ), a, b );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 1.5m, fn( 3.5m, 2.0m ) );
        Assert.AreEqual( -10m, fn( 5m, 15m ) );
    }

    // ================================================================
    // Multiply — byte
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Multiply_Byte( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof( byte ), "a" );
        var b = Expression.Parameter( typeof( byte ), "b" );
        var mul = Expression.Convert(
            Expression.Multiply( Expression.Convert( a, typeof( int ) ), Expression.Convert( b, typeof( int ) ) ),
            typeof( byte ) );
        var lambda = Expression.Lambda<Func<byte, byte, byte>>( mul, a, b );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( (byte) 6, fn( 2, 3 ) );
        Assert.AreEqual( (byte) 0, fn( 0, 100 ) );
    }

    // ================================================================
    // Divide — byte
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Divide_Byte( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof( byte ), "a" );
        var b = Expression.Parameter( typeof( byte ), "b" );
        var div = Expression.Convert(
            Expression.Divide( Expression.Convert( a, typeof( int ) ), Expression.Convert( b, typeof( int ) ) ),
            typeof( byte ) );
        var lambda = Expression.Lambda<Func<byte, byte, byte>>( div, a, b );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( (byte) 5, fn( 10, 2 ) );
        Assert.AreEqual( (byte) 1, fn( 7, 4 ) );
    }

    // ================================================================
    // Modulo — byte
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Modulo_Byte( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof( byte ), "a" );
        var b = Expression.Parameter( typeof( byte ), "b" );
        var mod = Expression.Convert(
            Expression.Modulo( Expression.Convert( a, typeof( int ) ), Expression.Convert( b, typeof( int ) ) ),
            typeof( byte ) );
        var lambda = Expression.Lambda<Func<byte, byte, byte>>( mod, a, b );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( (byte) 1, fn( 7, 3 ) );
        Assert.AreEqual( (byte) 0, fn( 10, 2 ) );
    }
}
