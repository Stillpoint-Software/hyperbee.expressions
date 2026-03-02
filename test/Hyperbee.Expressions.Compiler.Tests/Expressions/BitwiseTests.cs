using System.Linq.Expressions;
using Hyperbee.Expressions.Compiler.Tests.TestSupport;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Hyperbee.Expressions.Compiler.Tests.Expressions;

[TestClass]
public class BitwiseTests
{
    // --- And (int) ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void And_Int( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(int), "a" );
        var b = Expression.Parameter( typeof(int), "b" );
        var lambda = Expression.Lambda<Func<int, int, int>>( Expression.And( a, b ), a, b );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 0, fn( 0, 0 ) );
        Assert.AreEqual( 0, fn( 0xFF, 0 ) );
        Assert.AreEqual( 0x0F, fn( 0xFF, 0x0F ) );
        Assert.AreEqual( unchecked((int) 0xFFFFFFFF), fn( -1, -1 ) );
        Assert.AreEqual( 1, fn( 0b1111, 0b0001 ) );
    }

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void And_Long( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(long), "a" );
        var b = Expression.Parameter( typeof(long), "b" );
        var lambda = Expression.Lambda<Func<long, long, long>>( Expression.And( a, b ), a, b );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 0L, fn( 0L, 0L ) );
        Assert.AreEqual( 0xFFL, fn( 0xFFL, 0xFFL ) );
        Assert.AreEqual( 0L, fn( 0xF0L, 0x0FL ) );
        Assert.AreEqual( -1L, fn( -1L, -1L ) );
    }

    // --- Or (int) ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Or_Int( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(int), "a" );
        var b = Expression.Parameter( typeof(int), "b" );
        var lambda = Expression.Lambda<Func<int, int, int>>( Expression.Or( a, b ), a, b );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 0, fn( 0, 0 ) );
        Assert.AreEqual( 0xFF, fn( 0xFF, 0 ) );
        Assert.AreEqual( 0xFF, fn( 0xF0, 0x0F ) );
        Assert.AreEqual( -1, fn( -1, 0 ) );
        Assert.AreEqual( 0b1111, fn( 0b1100, 0b0011 ) );
    }

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Or_Long( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(long), "a" );
        var b = Expression.Parameter( typeof(long), "b" );
        var lambda = Expression.Lambda<Func<long, long, long>>( Expression.Or( a, b ), a, b );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 0L, fn( 0L, 0L ) );
        Assert.AreEqual( 0xFFL, fn( 0xF0L, 0x0FL ) );
        Assert.AreEqual( -1L, fn( -1L, 0L ) );
    }

    // --- ExclusiveOr (int) ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Xor_Int( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(int), "a" );
        var b = Expression.Parameter( typeof(int), "b" );
        var lambda = Expression.Lambda<Func<int, int, int>>( Expression.ExclusiveOr( a, b ), a, b );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 0, fn( 0, 0 ) );
        Assert.AreEqual( 0xFF, fn( 0xFF, 0 ) );
        Assert.AreEqual( 0xFF, fn( 0xF0, 0x0F ) );
        Assert.AreEqual( 0, fn( 0xFF, 0xFF ) );
        Assert.AreEqual( 0, fn( -1, -1 ) );
        Assert.AreEqual( -1, fn( -1, 0 ) );
    }

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Xor_Long( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(long), "a" );
        var b = Expression.Parameter( typeof(long), "b" );
        var lambda = Expression.Lambda<Func<long, long, long>>( Expression.ExclusiveOr( a, b ), a, b );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 0L, fn( 0L, 0L ) );
        Assert.AreEqual( 0xFFL, fn( 0xF0L, 0x0FL ) );
        Assert.AreEqual( 0L, fn( -1L, -1L ) );
    }

    // --- LeftShift (int) ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void LeftShift_Int( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(int), "a" );
        var b = Expression.Parameter( typeof(int), "b" );
        var lambda = Expression.Lambda<Func<int, int, int>>( Expression.LeftShift( a, b ), a, b );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 0, fn( 0, 1 ) );
        Assert.AreEqual( 2, fn( 1, 1 ) );
        Assert.AreEqual( 4, fn( 1, 2 ) );
        Assert.AreEqual( 256, fn( 1, 8 ) );
        Assert.AreEqual( unchecked((int) 0x80000000), fn( 1, 31 ) );
        Assert.AreEqual( -2, fn( -1, 1 ) );
    }

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void LeftShift_Long( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(long), "a" );
        var b = Expression.Parameter( typeof(int), "b" );
        var lambda = Expression.Lambda<Func<long, int, long>>( Expression.LeftShift( a, b ), a, b );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 0L, fn( 0L, 1 ) );
        Assert.AreEqual( 2L, fn( 1L, 1 ) );
        Assert.AreEqual( 1L << 32, fn( 1L, 32 ) );
        Assert.AreEqual( long.MinValue, fn( 1L, 63 ) );
    }

    // --- RightShift (int) ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void RightShift_Int( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(int), "a" );
        var b = Expression.Parameter( typeof(int), "b" );
        var lambda = Expression.Lambda<Func<int, int, int>>( Expression.RightShift( a, b ), a, b );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 0, fn( 0, 1 ) );
        Assert.AreEqual( 0, fn( 1, 1 ) );
        Assert.AreEqual( 1, fn( 2, 1 ) );
        Assert.AreEqual( 1, fn( 256, 8 ) );
        // Arithmetic right shift: sign bit propagated
        Assert.AreEqual( -1, fn( -1, 1 ) );
        Assert.AreEqual( -1, fn( -2, 1 ) );
    }

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void RightShift_Long( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(long), "a" );
        var b = Expression.Parameter( typeof(int), "b" );
        var lambda = Expression.Lambda<Func<long, int, long>>( Expression.RightShift( a, b ), a, b );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 0L, fn( 0L, 1 ) );
        Assert.AreEqual( 1L, fn( 1L << 32, 32 ) );
        Assert.AreEqual( -1L, fn( -1L, 1 ) );
    }

    // --- Bitwise with uint / ulong ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void And_UInt( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(uint), "a" );
        var b = Expression.Parameter( typeof(uint), "b" );
        var lambda = Expression.Lambda<Func<uint, uint, uint>>( Expression.And( a, b ), a, b );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 0u, fn( 0u, 0u ) );
        Assert.AreEqual( 0xFFu, fn( 0xFFu, 0xFFu ) );
        Assert.AreEqual( uint.MaxValue, fn( uint.MaxValue, uint.MaxValue ) );
    }

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Or_ULong( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(ulong), "a" );
        var b = Expression.Parameter( typeof(ulong), "b" );
        var lambda = Expression.Lambda<Func<ulong, ulong, ulong>>( Expression.Or( a, b ), a, b );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 0ul, fn( 0ul, 0ul ) );
        Assert.AreEqual( 0xFFul, fn( 0xF0ul, 0x0Ful ) );
        Assert.AreEqual( ulong.MaxValue, fn( ulong.MaxValue, 0ul ) );
    }

    // --- Compound patterns ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void BitMask_ExtractBits( CompilerType compilerType )
    {
        // Extract bits 4-7: (value >> 4) & 0x0F
        var value = Expression.Parameter( typeof(int), "value" );
        var shifted = Expression.RightShift( value, Expression.Constant( 4 ) );
        var masked = Expression.And( shifted, Expression.Constant( 0x0F ) );
        var lambda = Expression.Lambda<Func<int, int>>( masked, value );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 0, fn( 0 ) );
        Assert.AreEqual( 0x0F, fn( 0xFF ) );
        Assert.AreEqual( 0x0A, fn( 0xAB ) );
    }

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Xor_Swap( CompilerType compilerType )
    {
        // XOR swap: a ^ b ^ a == b
        var a = Expression.Parameter( typeof(int), "a" );
        var b = Expression.Parameter( typeof(int), "b" );
        var xor1 = Expression.ExclusiveOr( a, b );
        var xor2 = Expression.ExclusiveOr( xor1, a );
        var lambda = Expression.Lambda<Func<int, int, int>>( xor2, a, b );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 42, fn( 7, 42 ) );
        Assert.AreEqual( 0, fn( 0, 0 ) );
        Assert.AreEqual( -1, fn( 0, -1 ) );
    }

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Not_Int_BitwiseComplement( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(int), "a" );
        var lambda = Expression.Lambda<Func<int, int>>( Expression.Not( a ), a );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( -1, fn( 0 ) );
        Assert.AreEqual( 0, fn( -1 ) );
        Assert.AreEqual( ~42, fn( 42 ) );
        Assert.AreEqual( ~int.MaxValue, fn( int.MaxValue ) );
    }

    // --- Boolean bitwise (non-short-circuit) ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void And_Bool_NonShortCircuit( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(bool), "a" );
        var b = Expression.Parameter( typeof(bool), "b" );
        var lambda = Expression.Lambda<Func<bool, bool, bool>>( Expression.And( a, b ), a, b );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( false, fn( false, false ) );
        Assert.AreEqual( false, fn( true, false ) );
        Assert.AreEqual( false, fn( false, true ) );
        Assert.AreEqual( true, fn( true, true ) );
    }

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Or_Bool_NonShortCircuit( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(bool), "a" );
        var b = Expression.Parameter( typeof(bool), "b" );
        var lambda = Expression.Lambda<Func<bool, bool, bool>>( Expression.Or( a, b ), a, b );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( false, fn( false, false ) );
        Assert.AreEqual( true, fn( true, false ) );
        Assert.AreEqual( true, fn( false, true ) );
        Assert.AreEqual( true, fn( true, true ) );
    }

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Xor_Bool( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(bool), "a" );
        var b = Expression.Parameter( typeof(bool), "b" );
        var lambda = Expression.Lambda<Func<bool, bool, bool>>( Expression.ExclusiveOr( a, b ), a, b );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( false, fn( false, false ) );
        Assert.AreEqual( true, fn( true, false ) );
        Assert.AreEqual( true, fn( false, true ) );
        Assert.AreEqual( false, fn( true, true ) );
    }
}
