using System.Linq.Expressions;
using System.Reflection;
using Hyperbee.Expressions.Compiler.Tests.TestSupport;
using Hyperbee.Expressions.CompilerServices;
using static System.Linq.Expressions.Expression;
using static Hyperbee.Expressions.ExpressionExtensions;

namespace Hyperbee.Expressions.Compiler.Tests.Integration;

/// <summary>
/// The enumerable state machine defines MoveNext as its own method when the ambient builder
/// can emit into a MethodBuilder, rather than holding a delegate in a field and invoking it
/// on every resume.
/// </summary>
/// <remarks>
/// The machine is the object handed back, so which form it took is a question about its
/// type: the delegate form carries a <c>__moveNextDelegate&lt;&gt;</c> field and the emitted
/// form does not. Asserting on the type rather than on timing is what keeps this honest --
/// the async benchmark spent a while measuring two tiers that had both quietly declined.
/// </remarks>
[TestClass]
public class EmittedMoveNextEnumerableTests
{
    private const string DelegateField = "__moveNextDelegate<>";

    private static int PrivateEcho( int value ) => value;

    private static bool HoldsDelegate( IEnumerable<int> stateMachine ) =>
        stateMachine.GetType().GetField( DelegateField ) != null;

    private static Func<int, IEnumerable<int>> Compile(
        CompilerType compiler,
        Expression body,
        bool emitIntoType,
        ParameterExpression input )
    {
        var options = new ExpressionRuntimeOptions { EmitMoveNextIntoType = emitIntoType };

        return Lambda<Func<int, IEnumerable<int>>>(
            BlockEnumerable( new[] { body, YieldReturn( Add( input, Constant( 1 ) ) ) }, options ),
            input ).Compile( compiler );
    }

    [TestMethod]
    public void PublicBody_BecomesTheMachinesOwnMethod()
    {
        // Arrange
        var input = Parameter( typeof( int ), "input" );

        // Act
        var machine = Compile( CompilerType.Hyperbee, YieldReturn( input ), true, input )( 7 );

        // Assert
        Assert.IsFalse( HoldsDelegate( machine ),
            $"expected MoveNext emitted into {machine.GetType().Name}, but it holds a delegate" );

        CollectionAssert.AreEqual( new[] { 7, 8 }, machine.ToArray() );
    }

    [TestMethod]
    public void NonPublicReference_KeepsTheDelegate()
    {
        // Arrange: the body calls a private method, which only a DynamicMethod may do
        var input = Parameter( typeof( int ), "input" );

        var privateEcho = typeof( EmittedMoveNextEnumerableTests )
            .GetMethod( nameof( PrivateEcho ), BindingFlags.NonPublic | BindingFlags.Static )!;

        // Act
        var machine = Compile( CompilerType.Hyperbee, YieldReturn( Call( privateEcho, input ) ), true, input )( 7 );

        // Assert
        Assert.IsTrue( HoldsDelegate( machine ),
            "expected the delegate form for a body reaching a private member" );

        CollectionAssert.AreEqual( new[] { 7, 8 }, machine.ToArray() );
    }

    [TestMethod]
    public void EmitMoveNextIntoType_False_KeepsTheDelegate()
    {
        // Arrange
        var input = Parameter( typeof( int ), "input" );

        // Act
        var machine = Compile( CompilerType.Hyperbee, YieldReturn( input ), false, input )( 7 );

        // Assert
        Assert.IsTrue( HoldsDelegate( machine ), "expected the switch to force the delegate form" );

        CollectionAssert.AreEqual( new[] { 7, 8 }, machine.ToArray() );
    }

    [TestMethod]
    public void SystemCompiler_KeepsTheDelegate()
    {
        // Arrange: the System compiler cannot emit into a MethodBuilder at all
        var input = Parameter( typeof( int ), "input" );

        // Act
        var machine = Compile( CompilerType.System, YieldReturn( input ), true, input )( 7 );

        // Assert
        Assert.IsTrue( HoldsDelegate( machine ), "expected the delegate form under the System compiler" );

        CollectionAssert.AreEqual( new[] { 7, 8 }, machine.ToArray() );
    }

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public void BothMoveNextForms_AgreeOnTheSequence( CompilerType compiler )
    {
        // Arrange
        var input = Parameter( typeof( int ), "input" );

        // Act
        var emitted = Compile( compiler, YieldReturn( input ), true, input )( 7 ).ToArray();
        var delegated = Compile( compiler, YieldReturn( input ), false, input )( 7 ).ToArray();

        // Assert
        CollectionAssert.AreEqual( new[] { 7, 8 }, emitted );
        CollectionAssert.AreEqual( emitted, delegated );
    }

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public void EmittedMoveNext_RunsALoop( CompilerType compiler )
    {
        // Arrange
        var input = Parameter( typeof( int ), "input" );
        var index = Variable( typeof( int ), "index" );
        var breakLabel = Label( "break" );

        var lambda = Lambda<Func<int, IEnumerable<int>>>(
            BlockEnumerable(
                new[] { index },
                new Expression[]
                {
                    Assign( index, Constant( 0 ) ),
                    Loop(
                        IfThenElse(
                            LessThan( index, input ),
                            Block(
                                YieldReturn( index ),
                                PostIncrementAssign( index ) ),
                            Break( breakLabel ) ),
                        breakLabel )
                } ),
            input );

        // Act & Assert
        CollectionAssert.AreEqual( new[] { 0, 1, 2, 3 }, lambda.Compile( compiler )( 4 ).ToArray() );
    }
}
