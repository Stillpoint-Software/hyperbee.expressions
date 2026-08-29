using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;
using static System.Linq.Expressions.Expression;

namespace Hyperbee.Expressions.Compiler.Tests.Expressions;

/// <summary>
/// Emitting a lambda into an instance method whose type carries the lambda's non-embeddable
/// constants in a field.
/// </summary>
/// <remarks>
/// A static method has nowhere to keep an object constant, which is why CompileToMethod
/// rejects them. An instance method has <c>this</c>, so they can live on the type. This is
/// what lets a coroutine body be its state machine's own method instead of a delegate held
/// in a field.
/// </remarks>
[TestClass]
public class CompileToInstanceMethodConstantsTests
{
    private const string ConstantsField = "__constants<>";

    public sealed class Probe
    {
        public string Text { get; init; } = "";
        public int Value { get; init; }

        public int Add( int other ) => Value + other;
    }

    private static TypeBuilder DefineType( string name )
    {
        var assembly = AssemblyBuilder.DefineDynamicAssembly(
            new AssemblyName( $"InstanceMethodConstants_{name}" ), AssemblyBuilderAccess.Run );

        return assembly.DefineDynamicModule( "Main" ).DefineType( name, TypeAttributes.Public );
    }

    [TestMethod]
    public void ObjectConstant_IsReachedThroughTheField()
    {
        // Arrange: (self) => ((Probe)constant).Text
        var probe = new Probe { Text = "hello", Value = 7 };

        var typeBuilder = DefineType( "ReadsObjectConstant" );
        var constantsField = typeBuilder.DefineField( ConstantsField, typeof( object[] ), FieldAttributes.Public );

        var self = Parameter( typeBuilder, "self" );
        var body = Property( Constant( probe ), nameof( Probe.Text ) );

        var method = typeBuilder.DefineMethod(
            "Run", MethodAttributes.Public, typeof( string ), Type.EmptyTypes );

        var constants = HyperbeeCompiler.CompileToInstanceMethod(
            [self], body, typeof( string ), method, constantsField );

        var type = typeBuilder.CreateType();
        var instance = Activator.CreateInstance( type )!;

        type.GetField( ConstantsField )!.SetValue( instance, constants );

        // Act
        var result = type.GetMethod( "Run" )!.Invoke( instance, null );

        // Assert
        Assert.AreEqual( "hello", result );
    }

    [TestMethod]
    public void InstanceCallOnObjectConstant_Works()
    {
        // Arrange: (self) => ((Probe)constant).Add( 5 )
        var probe = new Probe { Value = 37 };

        var typeBuilder = DefineType( "CallsObjectConstant" );
        var constantsField = typeBuilder.DefineField( ConstantsField, typeof( object[] ), FieldAttributes.Public );

        var self = Parameter( typeBuilder, "self" );
        var body = Call( Constant( probe ), typeof( Probe ).GetMethod( nameof( Probe.Add ) )!, Constant( 5 ) );

        var method = typeBuilder.DefineMethod(
            "Run", MethodAttributes.Public, typeof( int ), Type.EmptyTypes );

        var constants = HyperbeeCompiler.CompileToInstanceMethod(
            [self], body, typeof( int ), method, constantsField );

        var type = typeBuilder.CreateType();
        var instance = Activator.CreateInstance( type )!;

        type.GetField( ConstantsField )!.SetValue( instance, constants );

        // Act
        var result = type.GetMethod( "Run" )!.Invoke( instance, null );

        // Assert
        Assert.AreEqual( 42, result );
    }

    [TestMethod]
    public void InstanceFields_AreReachableFromTheBody()
    {
        // Arrange: the lambda's first parameter is the instance, so its fields are in reach.
        var typeBuilder = DefineType( "ReadsOwnField" );
        var constantsField = typeBuilder.DefineField( ConstantsField, typeof( object[] ), FieldAttributes.Public );
        var stateField = typeBuilder.DefineField( "state", typeof( int ), FieldAttributes.Public );

        var self = Parameter( typeBuilder, "self" );
        var probe = new Probe { Value = 100 };

        // self.state + ((Probe)constant).Value
        var body = Add(
            Field( self, stateField ),
            Property( Constant( probe ), nameof( Probe.Value ) ) );

        var method = typeBuilder.DefineMethod(
            "Run", MethodAttributes.Public, typeof( int ), Type.EmptyTypes );

        var constants = HyperbeeCompiler.CompileToInstanceMethod(
            [self], body, typeof( int ), method, constantsField );

        var type = typeBuilder.CreateType();
        var instance = Activator.CreateInstance( type )!;

        type.GetField( ConstantsField )!.SetValue( instance, constants );
        type.GetField( "state" )!.SetValue( instance, 5 );

        // Act
        var result = type.GetMethod( "Run" )!.Invoke( instance, null );

        // Assert
        Assert.AreEqual( 105, result );
    }

    [TestMethod]
    public void EmbeddableConstants_StillEmitInline()
    {
        // Arrange: a body with only primitive constants needs no field access
        var typeBuilder = DefineType( "EmbeddableOnly" );
        var constantsField = typeBuilder.DefineField( ConstantsField, typeof( object[] ), FieldAttributes.Public );

        var self = Parameter( typeBuilder, "self" );
        var body = Add( Constant( 40 ), Constant( 2 ) );

        var method = typeBuilder.DefineMethod(
            "Run", MethodAttributes.Public, typeof( int ), Type.EmptyTypes );

        var constants = HyperbeeCompiler.CompileToInstanceMethod(
            [self], body, typeof( int ), method, constantsField );

        var type = typeBuilder.CreateType();
        var instance = Activator.CreateInstance( type )!;

        type.GetField( ConstantsField )!.SetValue( instance, constants );

        // Act
        var result = type.GetMethod( "Run" )!.Invoke( instance, null );

        // Assert
        Assert.AreEqual( 42, result );
        Assert.AreEqual( 0, constants.Length );
    }
}
