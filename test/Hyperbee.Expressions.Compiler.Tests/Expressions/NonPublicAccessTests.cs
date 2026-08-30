using System.Reflection;
using Hyperbee.Expressions.Compiler.Tests.TestSupport;
using static System.Linq.Expressions.Expression;

namespace Hyperbee.Expressions.Compiler.Tests.Expressions;

/// <summary>
/// An expression tree may reference members it could not reference from source. Both
/// compilers emit into a <see cref="System.Reflection.Emit.DynamicMethod"/> created with
/// visibility checks skipped, so a private method or field is callable.
/// </summary>
/// <remarks>
/// This is load-bearing and was previously unpinned. Emitting a body into a MethodBuilder
/// instead -- which a state machine would do to make MoveNext its own method -- does not get
/// that exemption, so anything that changes where a body is emitted has to keep these
/// passing or fall back for the trees that need it.
/// </remarks>
[TestClass]
public class NonPublicAccessTests
{
    private static int PrivateAdd( int left, int right ) => left + right;

    private int _privateField = 11;

    private int PrivateInstanceDouble( int value ) => value * 2;

    internal static int InternalTriple( int value ) => value * 3;

    private sealed class PrivateType
    {
        public int Value { get; init; }
    }

    private static PrivateType MakePrivateType() => new() { Value = 7 };

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public void PrivateStaticMethod_IsCallable( CompilerType compiler )
    {
        var method = typeof( NonPublicAccessTests )
            .GetMethod( nameof( PrivateAdd ), BindingFlags.NonPublic | BindingFlags.Static )!;

        var lambda = Lambda<Func<int>>( Call( method, Constant( 40 ), Constant( 2 ) ) );

        Assert.AreEqual( 42, lambda.Compile( compiler )() );
    }

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public void InternalStaticMethod_IsCallable( CompilerType compiler )
    {
        var method = typeof( NonPublicAccessTests )
            .GetMethod( nameof( InternalTriple ), BindingFlags.NonPublic | BindingFlags.Static )!;

        var lambda = Lambda<Func<int>>( Call( method, Constant( 14 ) ) );

        Assert.AreEqual( 42, lambda.Compile( compiler )() );
    }

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public void PrivateInstanceMethod_IsCallable( CompilerType compiler )
    {
        var method = typeof( NonPublicAccessTests )
            .GetMethod( nameof( PrivateInstanceDouble ), BindingFlags.NonPublic | BindingFlags.Instance )!;

        var lambda = Lambda<Func<int>>( Call( Constant( this ), method, Constant( 21 ) ) );

        Assert.AreEqual( 42, lambda.Compile( compiler )() );
    }

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public void PrivateField_IsReadable( CompilerType compiler )
    {
        var field = typeof( NonPublicAccessTests )
            .GetField( nameof( _privateField ), BindingFlags.NonPublic | BindingFlags.Instance )!;

        var lambda = Lambda<Func<int>>( Field( Constant( this ), field ) );

        Assert.AreEqual( 11, lambda.Compile( compiler )() );
    }

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public void ConstantOfAPrivateType_IsUsable( CompilerType compiler )
    {
        // The constant's declared type is the private type, so the emitted body has to name
        // it. This is the shape that decides whether a cast can target the declared type.
        var value = MakePrivateType();

        var lambda = Lambda<Func<int>>(
            Property( Constant( value, typeof( PrivateType ) ), nameof( PrivateType.Value ) ) );

        Assert.AreEqual( 7, lambda.Compile( compiler )() );
    }

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public void ConstantDeclaredPublic_WithNonPublicRuntimeType( CompilerType compiler )
    {
        // Declared as Task<int>; the runtime type is a type internal to the BCL. A cast to
        // the runtime type would name something the emitting context may not see.
        var task = Task.Delay( 1 ).ContinueWith( _ => 42 );

        var lambda = Lambda<Func<int>>(
            Property( Constant( task, typeof( Task<int> ) ), nameof( Task<int>.Result ) ) );

        Assert.AreEqual( 42, lambda.Compile( compiler )() );
    }
}
