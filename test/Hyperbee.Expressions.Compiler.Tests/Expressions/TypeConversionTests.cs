using System.Linq.Expressions;
using Hyperbee.Expressions.Compiler.Tests.TestSupport;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Hyperbee.Expressions.Compiler.Tests.Expressions;

[TestClass]
public class TypeConversionTests
{
    // --- Convert: int -> long (widening) ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Convert_IntToLong( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(int), "a" );
        var convert = Expression.Convert( a, typeof(long) );
        var lambda = Expression.Lambda<Func<int, long>>( convert, a );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 0L, fn( 0 ) );
        Assert.AreEqual( 42L, fn( 42 ) );
        Assert.AreEqual( -1L, fn( -1 ) );
        Assert.AreEqual( (long) int.MaxValue, fn( int.MaxValue ) );
    }

    // --- Convert: long -> int (narrowing) ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Convert_LongToInt( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(long), "a" );
        var convert = Expression.Convert( a, typeof(int) );
        var lambda = Expression.Lambda<Func<long, int>>( convert, a );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 0, fn( 0L ) );
        Assert.AreEqual( 42, fn( 42L ) );
        Assert.AreEqual( -1, fn( -1L ) );
    }

    // --- Convert: int -> double ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Convert_IntToDouble( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(int), "a" );
        var convert = Expression.Convert( a, typeof(double) );
        var lambda = Expression.Lambda<Func<int, double>>( convert, a );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 0.0, fn( 0 ) );
        Assert.AreEqual( 42.0, fn( 42 ) );
        Assert.AreEqual( -1.0, fn( -1 ) );
    }

    // --- Convert: double -> int (truncation) ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Convert_DoubleToInt( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(double), "a" );
        var convert = Expression.Convert( a, typeof(int) );
        var lambda = Expression.Lambda<Func<double, int>>( convert, a );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 0, fn( 0.0 ) );
        Assert.AreEqual( 42, fn( 42.9 ) );
        Assert.AreEqual( -1, fn( -1.5 ) );
    }

    // --- Convert: int -> float ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Convert_IntToFloat( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(int), "a" );
        var convert = Expression.Convert( a, typeof(float) );
        var lambda = Expression.Lambda<Func<int, float>>( convert, a );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 0f, fn( 0 ) );
        Assert.AreEqual( 42f, fn( 42 ) );
    }

    // --- Convert: byte -> int (unsigned widening) ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Convert_ByteToInt( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(byte), "a" );
        var convert = Expression.Convert( a, typeof(int) );
        var lambda = Expression.Lambda<Func<byte, int>>( convert, a );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 0, fn( 0 ) );
        Assert.AreEqual( 255, fn( 255 ) );
        Assert.AreEqual( 128, fn( 128 ) );
    }

    // --- Convert: int -> byte (narrowing) ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Convert_IntToByte( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(int), "a" );
        var convert = Expression.Convert( a, typeof(byte) );
        var lambda = Expression.Lambda<Func<int, byte>>( convert, a );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( (byte) 0, fn( 0 ) );
        Assert.AreEqual( (byte) 255, fn( 255 ) );
        Assert.AreEqual( (byte) 42, fn( 42 ) );
    }

    // --- Convert: int -> short ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Convert_IntToShort( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(int), "a" );
        var convert = Expression.Convert( a, typeof(short) );
        var lambda = Expression.Lambda<Func<int, short>>( convert, a );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( (short) 0, fn( 0 ) );
        Assert.AreEqual( (short) 42, fn( 42 ) );
        Assert.AreEqual( (short) -1, fn( -1 ) );
    }

    // --- ConvertChecked: int -> byte (overflow) ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void ConvertChecked_IntToByte_Overflow( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(int), "a" );
        var convert = Expression.ConvertChecked( a, typeof(byte) );
        var lambda = Expression.Lambda<Func<int, byte>>( convert, a );
        var fn = lambda.Compile( compilerType );

        // Normal range works
        Assert.AreEqual( (byte) 42, fn( 42 ) );
        Assert.AreEqual( (byte) 0, fn( 0 ) );
        Assert.AreEqual( (byte) 255, fn( 255 ) );

        // Overflow throws
        var threw = false;
        try { fn( 256 ); } catch ( OverflowException ) { threw = true; }
        Assert.IsTrue( threw, "Expected OverflowException for 256 -> byte." );

        threw = false;
        try { fn( -1 ); } catch ( OverflowException ) { threw = true; }
        Assert.IsTrue( threw, "Expected OverflowException for -1 -> byte." );
    }

    // --- ConvertChecked: long -> int (overflow) ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void ConvertChecked_LongToInt_Overflow( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(long), "a" );
        var convert = Expression.ConvertChecked( a, typeof(int) );
        var lambda = Expression.Lambda<Func<long, int>>( convert, a );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 42, fn( 42L ) );
        var threw = false;
        try { fn( (long) int.MaxValue + 1 ); } catch ( OverflowException ) { threw = true; }
        Assert.IsTrue( threw, "Expected OverflowException for long > int.MaxValue -> int." );
    }

    // --- TypeAs: object -> string (reference type) ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void TypeAs_ObjectToString( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(object), "a" );
        var typeAs = Expression.TypeAs( a, typeof(string) );
        var lambda = Expression.Lambda<Func<object, string?>>( typeAs, a );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( "hello", fn( "hello" ) );
        Assert.IsNull( fn( 42 ) );
        Assert.IsNull( fn( null! ) );
    }

    // --- TypeIs: object is string ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void TypeIs_ObjectIsString( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(object), "a" );
        var typeIs = Expression.TypeIs( a, typeof(string) );
        var lambda = Expression.Lambda<Func<object, bool>>( typeIs, a );
        var fn = lambda.Compile( compilerType );

        Assert.IsTrue( fn( "hello" ) );
        Assert.IsFalse( fn( 42 ) );
        Assert.IsFalse( fn( null! ) );
    }

    // --- Convert: int -> object (boxing) ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Convert_Boxing_IntToObject( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(int), "a" );
        var convert = Expression.Convert( a, typeof(object) );
        var lambda = Expression.Lambda<Func<int, object>>( convert, a );
        var fn = lambda.Compile( compilerType );

        var result = fn( 42 );
        Assert.AreEqual( 42, result );
        Assert.IsInstanceOfType<int>( result );
    }

    // --- Convert: object -> int (unboxing) ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Convert_Unboxing_ObjectToInt( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(object), "a" );
        var convert = Expression.Convert( a, typeof(int) );
        var lambda = Expression.Lambda<Func<object, int>>( convert, a );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 42, fn( 42 ) );
        Assert.AreEqual( 0, fn( 0 ) );
    }

    // --- Convert: string -> object (reference upcast, no-op) ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Convert_ReferenceUpcast_StringToObject( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(string), "a" );
        var convert = Expression.Convert( a, typeof(object) );
        var lambda = Expression.Lambda<Func<string, object>>( convert, a );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( "hello", fn( "hello" ) );
    }

    // --- Convert: object -> string (reference downcast) ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Convert_ReferenceDowncast_ObjectToString( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(object), "a" );
        var convert = Expression.Convert( a, typeof(string) );
        var lambda = Expression.Lambda<Func<object, string>>( convert, a );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( "hello", fn( "hello" ) );
        var threw = false;
        try { fn( 42 ); } catch ( InvalidCastException ) { threw = true; }
        Assert.IsTrue( threw, "Expected InvalidCastException for int -> string." );
    }

    // --- Convert: int -> enum ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Convert_IntToEnum( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(int), "a" );
        var convert = Expression.Convert( a, typeof(DayOfWeek) );
        var lambda = Expression.Lambda<Func<int, DayOfWeek>>( convert, a );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( DayOfWeek.Monday, fn( 1 ) );
        Assert.AreEqual( DayOfWeek.Sunday, fn( 0 ) );
    }

    // --- Convert: enum -> int ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Convert_EnumToInt( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(DayOfWeek), "a" );
        var convert = Expression.Convert( a, typeof(int) );
        var lambda = Expression.Lambda<Func<DayOfWeek, int>>( convert, a );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 1, fn( DayOfWeek.Monday ) );
        Assert.AreEqual( 0, fn( DayOfWeek.Sunday ) );
    }

    // --- Convert with operator overload: explicit operator ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Convert_ExplicitOperator( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(double), "a" );
        var convert = Expression.Convert( a, typeof(decimal) );
        var lambda = Expression.Lambda<Func<double, decimal>>( convert, a );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 1.5m, fn( 1.5 ) );
        Assert.AreEqual( 0m, fn( 0.0 ) );
    }

    // --- Convert: nullable int -> int (null throws) ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Convert_NullableIntToInt( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(int?), "a" );
        var convert = Expression.Convert( a, typeof(int) );
        var lambda = Expression.Lambda<Func<int?, int>>( convert, a );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 42, fn( 42 ) );
        Assert.AreEqual( 0, fn( 0 ) );
        var threw = false;
        try { fn( null ); } catch ( InvalidOperationException ) { threw = true; }
        Assert.IsTrue( threw, "Expected InvalidOperationException for null int? -> int." );
    }

    // --- Convert: int -> nullable int ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Convert_IntToNullableInt( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(int), "a" );
        var convert = Expression.Convert( a, typeof(int?) );
        var lambda = Expression.Lambda<Func<int, int?>>( convert, a );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 42, fn( 42 ) );
        Assert.AreEqual( 0, fn( 0 ) );
    }

    // --- Convert: char -> int ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Convert_CharToInt( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(char), "a" );
        var convert = Expression.Convert( a, typeof(int) );
        var lambda = Expression.Lambda<Func<char, int>>( convert, a );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 65, fn( 'A' ) );
        Assert.AreEqual( 97, fn( 'a' ) );
        Assert.AreEqual( 48, fn( '0' ) );
        Assert.AreEqual( 0, fn( '\0' ) );
    }

    // --- Convert: int -> char ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Convert_IntToChar( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(int), "a" );
        var convert = Expression.Convert( a, typeof(char) );
        var lambda = Expression.Lambda<Func<int, char>>( convert, a );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 'A', fn( 65 ) );
        Assert.AreEqual( 'a', fn( 97 ) );
        Assert.AreEqual( '0', fn( 48 ) );
    }

    // --- Convert: char -> long ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Convert_CharToLong( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(char), "a" );
        var convert = Expression.Convert( a, typeof(long) );
        var lambda = Expression.Lambda<Func<char, long>>( convert, a );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 65L, fn( 'A' ) );
        Assert.AreEqual( 0L, fn( '\0' ) );
    }

    // --- Convert: char -> ushort ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Convert_CharToUShort( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(char), "a" );
        var convert = Expression.Convert( a, typeof(ushort) );
        var lambda = Expression.Lambda<Func<char, ushort>>( convert, a );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( (ushort) 65, fn( 'A' ) );
        Assert.AreEqual( (ushort) 0, fn( '\0' ) );
    }

    // --- Convert: int -> uint ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Convert_IntToUInt( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(int), "a" );
        var convert = Expression.Convert( a, typeof(uint) );
        var lambda = Expression.Lambda<Func<int, uint>>( convert, a );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( (uint) 42, fn( 42 ) );
        Assert.AreEqual( (uint) 0, fn( 0 ) );
        // Negative values wrap around (unchecked)
        Assert.AreEqual( uint.MaxValue, fn( -1 ) );
    }

    // --- Convert: uint -> int ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Convert_UIntToInt( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(uint), "a" );
        var convert = Expression.Convert( a, typeof(int) );
        var lambda = Expression.Lambda<Func<uint, int>>( convert, a );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 42, fn( 42u ) );
        Assert.AreEqual( int.MaxValue, fn( (uint) int.MaxValue ) );
        // Large uint wraps to negative int (unchecked)
        Assert.AreEqual( -1, fn( uint.MaxValue ) );
    }

    // --- Convert: ulong -> long ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Convert_ULongToLong( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(ulong), "a" );
        var convert = Expression.Convert( a, typeof(long) );
        var lambda = Expression.Lambda<Func<ulong, long>>( convert, a );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 42L, fn( 42UL ) );
        Assert.AreEqual( long.MaxValue, fn( (ulong) long.MaxValue ) );
    }

    // --- Convert: short -> int ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Convert_ShortToInt( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(short), "a" );
        var convert = Expression.Convert( a, typeof(int) );
        var lambda = Expression.Lambda<Func<short, int>>( convert, a );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 1000, fn( 1000 ) );
        Assert.AreEqual( -1, fn( -1 ) );
        Assert.AreEqual( short.MaxValue, fn( short.MaxValue ) );
    }

    // --- Convert: int -> decimal ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Convert_IntToDecimal( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(int), "a" );
        var convert = Expression.Convert( a, typeof(decimal) );
        var lambda = Expression.Lambda<Func<int, decimal>>( convert, a );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 42m, fn( 42 ) );
        Assert.AreEqual( 0m, fn( 0 ) );
        Assert.AreEqual( -1m, fn( -1 ) );
    }

    // --- Convert: decimal -> int (truncation) ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Convert_DecimalToInt( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(decimal), "a" );
        var convert = Expression.Convert( a, typeof(int) );
        var lambda = Expression.Lambda<Func<decimal, int>>( convert, a );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 42, fn( 42.9m ) );
        Assert.AreEqual( 0, fn( 0m ) );
        Assert.AreEqual( -1, fn( -1.5m ) );
    }

    // --- Convert: double -> decimal ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Convert_DoubleToDecimal( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(double), "a" );
        var convert = Expression.Convert( a, typeof(decimal) );
        var lambda = Expression.Lambda<Func<double, decimal>>( convert, a );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 3.14m, fn( 3.14 ) );
        Assert.AreEqual( 0m, fn( 0.0 ) );
    }

    // --- Convert: decimal -> double ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Convert_DecimalToDouble( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(decimal), "a" );
        var convert = Expression.Convert( a, typeof(double) );
        var lambda = Expression.Lambda<Func<decimal, double>>( convert, a );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 3.14, fn( 3.14m ) );
        Assert.AreEqual( 0.0, fn( 0m ) );
    }

    // --- Convert: int -> nullable decimal ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Convert_IntToNullableDecimal( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(int), "a" );
        var convert = Expression.Convert( a, typeof(decimal?) );
        var lambda = Expression.Lambda<Func<int, decimal?>>( convert, a );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 42m, fn( 42 ) );
        Assert.AreEqual( 0m, fn( 0 ) );
    }

    // --- TypeIs: object is IEnumerable<int> (interface check) ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void TypeIs_InterfaceType( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(object), "a" );
        var typeIs = Expression.TypeIs( a, typeof(System.Collections.IEnumerable) );
        var lambda = Expression.Lambda<Func<object, bool>>( typeIs, a );
        var fn = lambda.Compile( compilerType );

        Assert.IsTrue( fn( "hello" ) );
        Assert.IsTrue( fn( new int[] { 1, 2, 3 } ) );
        Assert.IsFalse( fn( 42 ) );
        Assert.IsFalse( fn( null! ) );
    }

    // --- TypeAs: object as IEnumerable (interface TypeAs) ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void TypeAs_InterfaceType( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(object), "a" );
        var typeAs = Expression.TypeAs( a, typeof(System.Collections.IEnumerable) );
        var lambda = Expression.Lambda<Func<object, System.Collections.IEnumerable?>>( typeAs, a );
        var fn = lambda.Compile( compilerType );

        Assert.IsNotNull( fn( "hello" ) );
        Assert.IsNotNull( fn( new int[] { 1, 2, 3 } ) );
        Assert.IsNull( fn( 42 ) );
        Assert.IsNull( fn( null! ) );
    }

    // --- TypeIs: null is string -> false ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void TypeIs_NullIsString_ReturnsFalse( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(object), "a" );
        var typeIs = Expression.TypeIs( a, typeof(string) );
        var lambda = Expression.Lambda<Func<object, bool>>( typeIs, a );
        var fn = lambda.Compile( compilerType );

        Assert.IsFalse( fn( null! ) );
    }

    // --- Convert: long -> ulong (unchecked, bit reinterpretation) ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Convert_LongToULong( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(long), "a" );
        var convert = Expression.Convert( a, typeof(ulong) );
        var lambda = Expression.Lambda<Func<long, ulong>>( convert, a );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( (ulong) 42L, fn( 42L ) );
        Assert.AreEqual( ulong.MaxValue, fn( -1L ) ); // unchecked bit reinterpretation
    }

    // --- Convert: float -> long ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Convert_FloatToLong( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(float), "a" );
        var convert = Expression.Convert( a, typeof(long) );
        var lambda = Expression.Lambda<Func<float, long>>( convert, a );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 42L, fn( 42.9f ) );   // truncates toward zero
        Assert.AreEqual( 0L, fn( 0.0f ) );
        Assert.AreEqual( -1L, fn( -1.5f ) );   // truncates toward zero (not floor)
    }
}
