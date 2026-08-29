using System.Linq.Expressions;
using System.Reflection;
using Hyperbee.Expressions.Compiler.Tests.TestSupport;
using Hyperbee.Expressions.CompilerServices;
using static System.Linq.Expressions.Expression;
using static Hyperbee.Expressions.ExpressionExtensions;

namespace Hyperbee.Expressions.Compiler.Tests.Integration;

/// <summary>
/// When the ambient builder can emit into a MethodBuilder, the state machine defines MoveNext
/// as its own method instead of holding a delegate in a field and invoking it on every resume.
/// </summary>
/// <remarks>
/// The optimization declines for a body that reaches a non-public member. Only a
/// DynamicMethod is created with visibility checks skipped, so such a body has to keep the
/// delegate form -- emitting into the type must never narrow what compiles.
/// </remarks>
[TestClass]
public class EmittedMoveNextTests
{
    private const string DelegateField = "__moveNextDelegate<>";

    private static int PrivateEcho( int value ) => value;

    public static Task<int> Echo( int value ) => Task.FromResult( value );

    private static Expression EchoCall( Expression value ) =>
        Call( typeof( EmittedMoveNextTests ), nameof( Echo ), Type.EmptyTypes, value );

    private static string Reduce( Expression<Func<Task<int>>> lambda, CompilerType compiler, bool emitIntoType = true )
    {
        // The state machine expression is captured as it is built, which shows whether the
        // body became the machine's own method or a delegate in a field.
        var source = "";

        var options = new ExpressionRuntimeOptions
        {
            SourceHandler = text => source = text,
            EmitMoveNextIntoType = emitIntoType
        };

        var block = (AsyncBlockExpression) lambda.Body;

        var captured = Lambda<Func<Task<int>>>(
            BlockAsync( block.Variables.ToArray(), block.Expressions.ToArray(), options ) );

        captured.Compile( compiler );

        return source;
    }

    [TestMethod]
    public void PublicBody_BecomesTheMachinesOwnMethod()
    {
        // Arrange
        var lambda = Lambda<Func<Task<int>>>( BlockAsync( Await( EchoCall( Constant( 42 ) ) ) ) );

        // Act
        var source = Reduce( lambda, CompilerType.Hyperbee );

        // Assert
        Assert.IsFalse( source.Contains( DelegateField ),
            $"expected MoveNext to be emitted into the type, but the machine still holds a delegate:\n{source}" );
    }

    [TestMethod]
    public void NonPublicReference_KeepsTheDelegate()
    {
        // Arrange: the body calls a private method, which only a DynamicMethod may do
        var privateEcho = typeof( EmittedMoveNextTests )
            .GetMethod( nameof( PrivateEcho ), BindingFlags.NonPublic | BindingFlags.Static )!;

        var lambda = Lambda<Func<Task<int>>>(
            BlockAsync( Await( EchoCall( Call( privateEcho, Constant( 42 ) ) ) ) ) );

        // Act
        var source = Reduce( lambda, CompilerType.Hyperbee );

        // Assert
        Assert.IsTrue( source.Contains( DelegateField ),
            $"expected the delegate form for a body reaching a private member:\n{source}" );
    }

    [TestMethod]
    public void SystemCompiler_KeepsTheDelegate()
    {
        // Arrange: the System compiler cannot emit into a MethodBuilder at all
        var lambda = Lambda<Func<Task<int>>>( BlockAsync( Await( EchoCall( Constant( 42 ) ) ) ) );

        // Act
        var source = Reduce( lambda, CompilerType.System );

        // Assert
        Assert.IsTrue( source.Contains( DelegateField ),
            $"expected the delegate form under the System compiler:\n{source}" );
    }

    [TestMethod]
    public void EmitMoveNextIntoType_False_KeepsTheDelegate()
    {
        // Arrange
        var lambda = Lambda<Func<Task<int>>>( BlockAsync( Await( EchoCall( Constant( 42 ) ) ) ) );

        // Act
        var source = Reduce( lambda, CompilerType.Hyperbee, emitIntoType: false );

        // Assert
        Assert.IsTrue( source.Contains( DelegateField ),
            $"expected the switch to force the delegate form:\n{source}" );
    }

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public async Task NonPublicReference_StillRuns( CompilerType compiler )
    {
        // Arrange
        var privateEcho = typeof( EmittedMoveNextTests )
            .GetMethod( nameof( PrivateEcho ), BindingFlags.NonPublic | BindingFlags.Static )!;

        var lambda = Lambda<Func<Task<int>>>(
            BlockAsync( Await( EchoCall( Call( privateEcho, Constant( 42 ) ) ) ) ) );

        // Act & Assert
        Assert.AreEqual( 42, await lambda.Compile( compiler )() );
    }

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public async Task EmittedMoveNext_RunsSeveralAwaits( CompilerType compiler )
    {
        // Arrange
        var value = Variable( typeof( int ), "value" );

        var lambda = Lambda<Func<Task<int>>>(
            BlockAsync(
                [value],
                Assign( value, Await( EchoCall( Constant( 20 ) ) ) ),
                Assign( value, Add( value, Await( EchoCall( Constant( 22 ) ) ) ) ),
                value ) );

        // Act & Assert
        Assert.AreEqual( 42, await lambda.Compile( compiler )() );
    }

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public async Task BothMoveNextForms_AgreeOnTheAnswer( CompilerType compiler )
    {
        // Arrange: the same body, once each way
        var value = Variable( typeof( int ), "value" );

        Expression Body( ExpressionRuntimeOptions options ) => BlockAsync(
            new[] { value },
            new Expression[]
            {
                Assign( value, Await( EchoCall( Constant( 20 ) ) ) ),
                Assign( value, Add( value, Await( EchoCall( Constant( 22 ) ) ) ) ),
                value
            },
            options );

        var emitted = Lambda<Func<Task<int>>>( Body( null ) );
        var delegated = Lambda<Func<Task<int>>>(
            Body( new ExpressionRuntimeOptions { EmitMoveNextIntoType = false } ) );

        // Act
        var fromEmitted = await emitted.Compile( compiler )();
        var fromDelegate = await delegated.Compile( compiler )();

        // Assert
        Assert.AreEqual( 42, fromEmitted );
        Assert.AreEqual( fromEmitted, fromDelegate );
    }
}
