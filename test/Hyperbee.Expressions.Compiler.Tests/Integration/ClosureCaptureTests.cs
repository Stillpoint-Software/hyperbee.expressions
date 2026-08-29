using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using Hyperbee.Expressions.Compiler.Tests.TestSupport;
using static System.Linq.Expressions.Expression;
using static Hyperbee.Expressions.ExpressionExtensions;

namespace Hyperbee.Expressions.Compiler.Tests.Integration;

/// <summary>
/// Integration tests for variable capture.
///
/// A variable declared in an enclosing scope and referenced from inside a closure
/// boundary must be shared, not copied: writes on either side have to be visible on the
/// other. HEC shares such variables through a StrongBox.
///
/// Coroutine blocks are closure boundaries too. Their state-machine body becomes a lambda
/// during Reduce(), after capture analysis has run, so the block itself has to be treated
/// as the boundary.
/// </summary>
[TestClass]
public class ClosureCaptureTests
{
    private static async Task<int> EchoAsync( int value )
    {
        await Task.Yield();
        return value;
    }

    private static Expression EchoAsyncCall( Expression value ) =>
        Call( typeof( ClosureCaptureTests ), nameof( EchoAsync ), Type.EmptyTypes, value );

    // -----------------------------------------------------------------------
    // Coroutine blocks as closure boundaries
    // -----------------------------------------------------------------------

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public async Task AsyncBlock_ReadsEnclosingLambdaParameter( CompilerType compiler )
    {
        // Arrange
        var input = Parameter( typeof( int ), "input" );
        var result = Variable( typeof( int ), "result" );

        var block = BlockAsync(
            new[] { result },
            new Expression[] { Assign( result, Await( EchoAsyncCall( input ) ) ), result } );

        var lambda = Lambda<Func<int, Task<int>>>( block, input );
        var compiled = lambda.Compile( compiler );

        // Act
        var value = await compiled( 42 );

        // Assert
        Assert.AreEqual( 42, value );
    }

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public async Task AsyncBlock_ReadsEnclosingBlockVariable( CompilerType compiler )
    {
        // Arrange
        var outer = Variable( typeof( int ), "outer" );
        var result = Variable( typeof( int ), "result" );

        var block = Block(
            new[] { outer },
            Assign( outer, Constant( 42 ) ),
            BlockAsync(
                new[] { result },
                new Expression[] { Assign( result, Await( EchoAsyncCall( outer ) ) ), result } ) );

        var lambda = Lambda<Func<Task<int>>>( block );
        var compiled = lambda.Compile( compiler );

        // Act
        var value = await compiled();

        // Assert
        Assert.AreEqual( 42, value );
    }

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public void AsyncBlock_WritesEnclosingVariable( CompilerType compiler )
    {
        // Arrange: the capture must be shared, not copied — a write inside the block has
        // to be visible to the enclosing expression once the block completes.
        var outer = Variable( typeof( int ), "outer" );
        var task = Variable( typeof( Task<int> ), "task" );

        var block = Block(
            new[] { outer, task },
            Assign( outer, Constant( 1 ) ),
            Assign( task, BlockAsync( Assign( outer, Await( EchoAsyncCall( Constant( 5 ) ) ) ) ) ),
            Call( typeof( Task ), nameof( Task.WaitAll ), Type.EmptyTypes, NewArrayInit( typeof( Task ), task ) ),
            outer );

        var lambda = Lambda<Func<int>>( block );
        var compiled = lambda.Compile( compiler );

        // Act
        var value = compiled();

        // Assert
        Assert.AreEqual( 5, value );
    }

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public void EnumerableBlock_ReadsEnclosingLambdaParameter( CompilerType compiler )
    {
        // Arrange
        var input = Parameter( typeof( int ), "input" );

        var block = BlockEnumerable( YieldReturn( input ), YieldReturn( Add( input, Constant( 1 ) ) ) );

        var lambda = Lambda<Func<int, IEnumerable<int>>>( block, input );
        var compiled = lambda.Compile( compiler );

        // Act
        var result = compiled( 42 ).ToArray();

        // Assert
        CollectionAssert.AreEqual( new[] { 42, 43 }, result );
    }

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public void EnumerableBlock_WithNoCaptures( CompilerType compiler )
    {
        // Arrange: a coroutine block introduces a lambda during Reduce(). Capture analysis
        // must anticipate it even when the block captures nothing.
        var local = Variable( typeof( int ), "local" );

        var block = BlockEnumerable(
            new[] { local },
            new Expression[]
            {
                Assign( local, Constant( 7 ) ),
                YieldReturn( local ),
                YieldReturn( Add( local, Constant( 1 ) ) )
            } );

        var lambda = Lambda<Func<IEnumerable<int>>>( block );
        var compiled = lambda.Compile( compiler );

        // Act
        var result = compiled().ToArray();

        // Assert
        CollectionAssert.AreEqual( new[] { 7, 8 }, result );
    }

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public async Task AsyncBlock_DeclaringSameVariableAsEnclosingScope( CompilerType compiler )
    {
        // Arrange: the block re-declares the enclosing variable, so it shadows it rather
        // than capturing it.
        var value = Variable( typeof( int ), "value" );

        var block = Block(
            new[] { value },
            Assign( value, Constant( 1 ) ),
            BlockAsync(
                new[] { value },
                new Expression[] { Assign( value, Await( EchoAsyncCall( Constant( 99 ) ) ) ), value } ) );

        var lambda = Lambda<Func<Task<int>>>( block );
        var compiled = lambda.Compile( compiler );

        // Act
        var result = await compiled();

        // Assert
        Assert.AreEqual( 99, result );
    }

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public async Task AsyncBlock_NestedInsideLambda_ReadsEnclosingVariable( CompilerType compiler )
    {
        // Arrange: two boundaries deep — an async block inside a nested lambda
        var input = Parameter( typeof( int ), "input" );
        var result = Variable( typeof( int ), "result" );

        var inner = Lambda<Func<Task<int>>>(
            BlockAsync(
                new[] { result },
                new Expression[] { Assign( result, Await( EchoAsyncCall( input ) ) ), result } ) );

        var lambda = Lambda<Func<int, Task<int>>>( Invoke( inner ), input );
        var compiled = lambda.Compile( compiler );

        // Act
        var value = await compiled( 42 );

        // Assert
        Assert.AreEqual( 42, value );
    }

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public async Task AsyncBlock_NestedAsyncBlocks_ReadEnclosingVariable( CompilerType compiler )
    {
        // Arrange
        var input = Parameter( typeof( int ), "input" );
        var outerResult = Variable( typeof( int ), "outerResult" );
        var innerResult = Variable( typeof( int ), "innerResult" );

        var innerBlock = BlockAsync(
            new[] { innerResult },
            new Expression[] { Assign( innerResult, Await( EchoAsyncCall( input ) ) ), innerResult } );

        var block = BlockAsync(
            new[] { outerResult },
            new Expression[] { Assign( outerResult, Await( innerBlock ) ), outerResult } );

        var lambda = Lambda<Func<int, Task<int>>>( block, input );
        var compiled = lambda.Compile( compiler );

        // Act
        var value = await compiled( 42 );

        // Assert
        Assert.AreEqual( 42, value );
    }

    // -----------------------------------------------------------------------
    // Ordinary nested-lambda captures, in node types the scan must reach
    // -----------------------------------------------------------------------

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public void NestedLambda_CapturedInsideLoop( CompilerType compiler )
    {
        // Arrange
        var input = Parameter( typeof( int ), "input" );
        var sum = Variable( typeof( int ), "sum" );
        var index = Variable( typeof( int ), "index" );
        var breakLabel = Label( typeof( int ), "break" );
        var inner = Lambda<Func<int>>( input );

        var block = Block(
            new[] { sum, index },
            Assign( sum, Constant( 0 ) ),
            Assign( index, Constant( 0 ) ),
            Loop(
                IfThenElse(
                    LessThan( index, Constant( 3 ) ),
                    Block(
                        Assign( sum, Add( sum, Invoke( inner ) ) ),
                        Assign( index, Add( index, Constant( 1 ) ) ) ),
                    Break( breakLabel, sum ) ),
                breakLabel ) );

        var lambda = Lambda<Func<int, int>>( block, input );

        // Act & Assert
        Assert.AreEqual( 15, lambda.Compile( compiler )( 5 ) );
    }

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public void NestedLambda_CapturedInsideSwitchCase( CompilerType compiler )
    {
        // Arrange
        var input = Parameter( typeof( int ), "input" );
        var inner = Lambda<Func<int>>( Multiply( input, Constant( 10 ) ) );

        var block = Switch(
            input,
            Constant( -1 ),
            SwitchCase( Invoke( inner ), Constant( 5 ) ) );

        var lambda = Lambda<Func<int, int>>( block, input );

        // Act & Assert
        Assert.AreEqual( 50, lambda.Compile( compiler )( 5 ) );
    }

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public void NestedLambda_CapturedInsideNewArray( CompilerType compiler )
    {
        // Arrange
        var input = Parameter( typeof( int ), "input" );
        var inner = Lambda<Func<int>>( input );

        var block = ArrayIndex(
            NewArrayInit( typeof( int ), Invoke( inner ), Constant( 0 ) ),
            Constant( 0 ) );

        var lambda = Lambda<Func<int, int>>( block, input );

        // Act & Assert
        Assert.AreEqual( 7, lambda.Compile( compiler )( 7 ) );
    }

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public void NestedLambda_CapturedInsideListInit( CompilerType compiler )
    {
        // Arrange
        var input = Parameter( typeof( int ), "input" );
        var inner = Lambda<Func<int>>( input );

        var listInit = ListInit(
            New( typeof( List<int> ) ),
            typeof( List<int> ).GetMethod( nameof( List<int>.Add ) )!,
            Invoke( inner ) );

        var block = MakeIndex(
            listInit,
            typeof( List<int> ).GetProperty( "Item" ),
            [Constant( 0 )] );

        var lambda = Lambda<Func<int, int>>( block, input );

        // Act & Assert
        Assert.AreEqual( 7, lambda.Compile( compiler )( 7 ) );
    }

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public void NestedLambda_CapturesVariableDeclaredInsideLoop( CompilerType compiler )
    {
        // Arrange: the declaration site is inside a loop, which the scan must reach
        var outer = Variable( typeof( int ), "outer" );
        var breakLabel = Label( typeof( int ), "break" );
        var inner = Lambda<Func<int>>( outer );

        var block = Loop(
            Block(
                new[] { outer },
                Assign( outer, Constant( 9 ) ),
                Break( breakLabel, Invoke( inner ) ) ),
            breakLabel );

        var lambda = Lambda<Func<int>>( block );

        // Act & Assert
        Assert.AreEqual( 9, lambda.Compile( compiler )() );
    }

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public void NestedLambda_ShadowsEnclosingVariable( CompilerType compiler )
    {
        // Arrange: the inner lambda declares the same variable instance, so it is not a
        // capture and the enclosing value must be untouched.
        var value = Variable( typeof( int ), "value" );

        var inner = Lambda<Func<int>>(
            Block( new[] { value }, Assign( value, Constant( 99 ) ), value ) );

        var block = Block(
            new[] { value },
            Assign( value, Constant( 1 ) ),
            Invoke( inner ),
            value );

        var lambda = Lambda<Func<int>>( block );

        // Act & Assert
        Assert.AreEqual( 1, lambda.Compile( compiler )() );
    }

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public void NestedLambda_WritesCapturedVariable( CompilerType compiler )
    {
        // Arrange
        var value = Variable( typeof( int ), "value" );
        var inner = Lambda<Action>( Assign( value, Constant( 42 ) ) );

        var block = Block(
            new[] { value },
            Assign( value, Constant( 1 ) ),
            Invoke( inner ),
            value );

        var lambda = Lambda<Func<int>>( block );

        // Act & Assert
        Assert.AreEqual( 42, lambda.Compile( compiler )() );
    }

    [TestMethod]
    public async Task AsyncBlock_CompiledByBothCompilers_StaysCorrect()
    {
        // A coroutine block caches its reduction, so the compiler that reduces it first
        // decides how the state-machine body is emitted. Either order has to work.

        // Arrange
        var input = Parameter( typeof( int ), "input" );
        var result = Variable( typeof( int ), "result" );

        var block = BlockAsync(
            new[] { result },
            new Expression[] { Assign( result, Await( EchoAsyncCall( input ) ) ), result } );

        var lambda = Lambda<Func<int, Task<int>>>( block, input );

        // Act
        var system = lambda.Compile( CompilerType.System );
        var hyperbee = lambda.Compile( CompilerType.Hyperbee );

        // Assert
        Assert.AreEqual( 42, await system( 42 ) );
        Assert.AreEqual( 42, await hyperbee( 42 ) );
    }

    [TestMethod]
    public async Task AsyncBlock_CompiledByBothCompilers_HyperbeeFirst_StaysCorrect()
    {
        // Arrange
        var input = Parameter( typeof( int ), "input" );
        var result = Variable( typeof( int ), "result" );

        var block = BlockAsync(
            new[] { result },
            new Expression[] { Assign( result, Await( EchoAsyncCall( input ) ) ), result } );

        var lambda = Lambda<Func<int, Task<int>>>( block, input );

        // Act
        var hyperbee = lambda.Compile( CompilerType.Hyperbee );
        var system = lambda.Compile( CompilerType.System );

        // Assert
        Assert.AreEqual( 42, await hyperbee( 42 ) );
        Assert.AreEqual( 42, await system( 42 ) );
    }

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public void RuntimeVariables_StillResolve( CompilerType compiler )
    {
        // Arrange
        var value = Variable( typeof( int ), "value" );
        var runtime = Variable( typeof( IRuntimeVariables ), "runtime" );

        var block = Block(
            new[] { value, runtime },
            Assign( value, Constant( 42 ) ),
            Assign( runtime, RuntimeVariables( value ) ),
            Convert( Property( runtime, "Item", Constant( 0 ) ), typeof( int ) ) );

        var lambda = Lambda<Func<int>>( block );

        // Act & Assert
        Assert.AreEqual( 42, lambda.Compile( compiler )() );
    }
}
