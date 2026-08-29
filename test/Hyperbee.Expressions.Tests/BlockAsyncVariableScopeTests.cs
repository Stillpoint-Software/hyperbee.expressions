using Hyperbee.Expressions.Tests.TestSupport;
using static System.Linq.Expressions.Expression;
using static Hyperbee.Expressions.ExpressionExtensions;

namespace Hyperbee.Expressions.Tests;

// Lowering hoists local variables onto state-machine fields. A ParameterExpression is
// identified by instance, never by name: distinct instances may share a name, and a name
// may be null. These tests cover the cases where names collide or are absent.

[TestClass]
public class BlockAsyncVariableScopeTests
{
    [TestMethod]
    [DataRow( CompleterType.Immediate, CompilerType.Fast )]
    [DataRow( CompleterType.Immediate, CompilerType.System )]
    [DataRow( CompleterType.Immediate, CompilerType.Interpret )]
    [DataRow( CompleterType.Deferred, CompilerType.Fast )]
    [DataRow( CompleterType.Deferred, CompilerType.System )]
    [DataRow( CompleterType.Deferred, CompilerType.Interpret )]
    public async Task AsyncBlock_ShouldNotConflateVariables_WithLambdaParameterOfSameName( CompleterType completer, CompilerType compiler )
    {
        // Arrange: an outer lambda parameter and a nested block local share the name "value"
        var input = Parameter( typeof( int ), "value" );
        var local = Variable( typeof( int ), "value" );
        var result = Variable( typeof( int ), "result" );

        var nestedBlock = Block(
            [local],
            Assign( local, Add( input, Constant( 5 ) ) ),
            Assign( result, Await( AsyncHelper.Completer(
                Constant( completer ),
                Add( local, Constant( 1 ) )
            ) ) )
        );

        var block = BlockAsync( [result], nestedBlock, result );
        var lambda = Lambda<Func<int, Task<int>>>( block, input );
        var compiledLambda = lambda.Compile( compiler );

        // Act
        var value = await compiledLambda( 10 );

        // Assert
        Assert.AreEqual( 16, value );
    }

    [TestMethod]
    [DataRow( CompleterType.Immediate, CompilerType.Fast )]
    [DataRow( CompleterType.Immediate, CompilerType.System )]
    [DataRow( CompleterType.Immediate, CompilerType.Interpret )]
    [DataRow( CompleterType.Deferred, CompilerType.Fast )]
    [DataRow( CompleterType.Deferred, CompilerType.System )]
    [DataRow( CompleterType.Deferred, CompilerType.Interpret )]
    public async Task AsyncBlock_ShouldNotConflateVariables_InSiblingBlocksWithSameName( CompleterType completer, CompilerType compiler )
    {
        // Arrange: two distinct variables in sibling blocks share the name "v"
        var first = Variable( typeof( int ), "v" );
        var second = Variable( typeof( int ), "v" );
        var result = Variable( typeof( int ), "result" );

        var block = BlockAsync(
            [result],
            Block(
                [first],
                Assign( first, Constant( 1 ) ),
                Assign( result, Await( AsyncHelper.Completer( Constant( completer ), first ) ) )
            ),
            Block(
                [second],
                Assign( second, Constant( 10 ) ),
                Assign( result, Await( AsyncHelper.Completer( Constant( completer ), Add( result, second ) ) ) )
            ),
            result
        );

        var lambda = Lambda<Func<Task<int>>>( block );
        var compiledLambda = lambda.Compile( compiler );

        // Act
        var value = await compiledLambda();

        // Assert
        Assert.AreEqual( 11, value );
    }

    [TestMethod]
    [DataRow( CompleterType.Immediate, CompilerType.Fast )]
    [DataRow( CompleterType.Immediate, CompilerType.System )]
    [DataRow( CompleterType.Immediate, CompilerType.Interpret )]
    [DataRow( CompleterType.Deferred, CompilerType.Fast )]
    [DataRow( CompleterType.Deferred, CompilerType.System )]
    [DataRow( CompleterType.Deferred, CompilerType.Interpret )]
    public async Task AsyncBlock_ShouldNotConflateVariables_InNestedBlocksWithSameName( CompleterType completer, CompilerType compiler )
    {
        // Arrange: an inner block shadows an outer block variable of the same name
        var outer = Variable( typeof( int ), "v" );
        var inner = Variable( typeof( int ), "v" );
        var result = Variable( typeof( int ), "result" );

        var block = BlockAsync(
            [result],
            Block(
                [outer],
                Assign( outer, Constant( 1 ) ),
                Block(
                    [inner],
                    Assign( inner, Constant( 10 ) ),
                    Assign( result, Await( AsyncHelper.Completer( Constant( completer ), inner ) ) )
                ),
                Assign( result, Add( result, outer ) )
            ),
            result
        );

        var lambda = Lambda<Func<Task<int>>>( block );
        var compiledLambda = lambda.Compile( compiler );

        // Act
        var value = await compiledLambda();

        // Assert
        Assert.AreEqual( 11, value );
    }

    [TestMethod]
    [DataRow( CompleterType.Immediate, CompilerType.Fast )]
    [DataRow( CompleterType.Immediate, CompilerType.System )]
    [DataRow( CompleterType.Immediate, CompilerType.Interpret )]
    [DataRow( CompleterType.Deferred, CompilerType.Fast )]
    [DataRow( CompleterType.Deferred, CompilerType.System )]
    [DataRow( CompleterType.Deferred, CompilerType.Interpret )]
    public async Task AsyncBlock_ShouldNotConflateVariables_WithoutNames( CompleterType completer, CompilerType compiler )
    {
        // Arrange: two unnamed variables in a nested block
        var first = Variable( typeof( int ) );
        var second = Variable( typeof( int ) );
        var result = Variable( typeof( int ), "result" );

        var block = BlockAsync(
            [result],
            Block(
                [first, second],
                Assign( first, Constant( 1 ) ),
                Assign( second, Constant( 10 ) ),
                Assign( result, Await( AsyncHelper.Completer( Constant( completer ), Add( first, second ) ) ) )
            ),
            result
        );

        var lambda = Lambda<Func<Task<int>>>( block );
        var compiledLambda = lambda.Compile( compiler );

        // Act
        var value = await compiledLambda();

        // Assert
        Assert.AreEqual( 11, value );
    }

    [TestMethod]
    [DataRow( CompleterType.Immediate, CompilerType.Fast )]
    [DataRow( CompleterType.Immediate, CompilerType.System )]
    [DataRow( CompleterType.Immediate, CompilerType.Interpret )]
    [DataRow( CompleterType.Deferred, CompilerType.Fast )]
    [DataRow( CompleterType.Deferred, CompilerType.System )]
    [DataRow( CompleterType.Deferred, CompilerType.Interpret )]
    public async Task AsyncBlock_ShouldScopeVariable_WhenNestedBlockRedeclaresIt( CompleterType completer, CompilerType compiler )
    {
        // Arrange: a nested block re-declares an async block variable. The declaration
        // opens a new scope, so the assignment must not reach the outer variable.
        var value = Variable( typeof( int ), "x" );
        var result = Variable( typeof( int ), "result" );

        var block = BlockAsync(
            [value, result],
            Assign( value, Constant( 1 ) ),
            Block( [value], Assign( value, Constant( 99 ) ) ),
            Assign( result, Await( AsyncHelper.Completer( Constant( completer ), value ) ) ),
            result
        );

        var lambda = Lambda<Func<Task<int>>>( block );
        var compiledLambda = lambda.Compile( compiler );

        // Act
        var actual = await compiledLambda();

        // Assert
        Assert.AreEqual( 1, actual );
    }

    [TestMethod]
    [DataRow( CompleterType.Immediate, CompilerType.Fast )]
    [DataRow( CompleterType.Immediate, CompilerType.System )]
    [DataRow( CompleterType.Immediate, CompilerType.Interpret )]
    [DataRow( CompleterType.Deferred, CompilerType.Fast )]
    [DataRow( CompleterType.Deferred, CompilerType.System )]
    [DataRow( CompleterType.Deferred, CompilerType.Interpret )]
    public async Task AsyncBlock_ShouldNotConflateVariables_WithInnerLambdaParameterOfSameName( CompleterType completer, CompilerType compiler )
    {
        // Arrange: a lambda invoked inside the async block has a parameter named "x",
        // matching an async block variable
        var value = Variable( typeof( int ), "x" );
        var parameter = Parameter( typeof( int ), "x" );
        var result = Variable( typeof( int ), "result" );

        var innerLambda = Lambda<Func<int, int>>( Multiply( parameter, Constant( 2 ) ), parameter );

        var block = BlockAsync(
            [value, result],
            Assign( value, Constant( 5 ) ),
            Assign( result, Await( AsyncHelper.Completer(
                Constant( completer ),
                Invoke( innerLambda, Constant( 3 ) )
            ) ) ),
            Add( result, value )
        );

        var lambda = Lambda<Func<Task<int>>>( block );
        var compiledLambda = lambda.Compile( compiler );

        // Act
        var actual = await compiledLambda();

        // Assert
        Assert.AreEqual( 11, actual );
    }

    // Enclosing-scope capture
    //
    // A variable owned by an enclosing scope and referenced inside the block must be
    // shared, not copied: a write on either side has to be visible on the other.

    [TestMethod]
    [DataRow( CompleterType.Immediate, CompilerType.Fast )]
    [DataRow( CompleterType.Immediate, CompilerType.System )]
    [DataRow( CompleterType.Immediate, CompilerType.Interpret )]
    [DataRow( CompleterType.Deferred, CompilerType.Fast )]
    [DataRow( CompleterType.Deferred, CompilerType.System )]
    [DataRow( CompleterType.Deferred, CompilerType.Interpret )]
    public async Task AsyncBlock_ShouldReadEnclosingLambdaParameter( CompleterType completer, CompilerType compiler )
    {
        // Arrange
        var input = Parameter( typeof( int ), "input" );
        var result = Variable( typeof( int ), "result" );

        var block = BlockAsync(
            [result],
            Assign( result, Await( AsyncHelper.Completer( Constant( completer ), input ) ) ),
            result );

        var lambda = Lambda<Func<int, Task<int>>>( block, input );
        var compiledLambda = lambda.Compile( compiler );

        // Act
        var value = await compiledLambda( 42 );

        // Assert
        Assert.AreEqual( 42, value );
    }

    [TestMethod]
    [DataRow( CompleterType.Immediate, CompilerType.Fast )]
    [DataRow( CompleterType.Immediate, CompilerType.System )]
    [DataRow( CompleterType.Immediate, CompilerType.Interpret )]
    [DataRow( CompleterType.Deferred, CompilerType.Fast )]
    [DataRow( CompleterType.Deferred, CompilerType.System )]
    [DataRow( CompleterType.Deferred, CompilerType.Interpret )]
    public async Task AsyncBlock_ShouldReadEnclosingBlockVariable( CompleterType completer, CompilerType compiler )
    {
        // Arrange
        var outer = Variable( typeof( int ), "outer" );
        var result = Variable( typeof( int ), "result" );

        var block = Block(
            [outer],
            Assign( outer, Constant( 42 ) ),
            BlockAsync(
                [result],
                Assign( result, Await( AsyncHelper.Completer( Constant( completer ), outer ) ) ),
                result ) );

        var lambda = Lambda<Func<Task<int>>>( block );
        var compiledLambda = lambda.Compile( compiler );

        // Act
        var value = await compiledLambda();

        // Assert
        Assert.AreEqual( 42, value );
    }

    [TestMethod]
    [DataRow( CompleterType.Immediate, CompilerType.Fast )]
    [DataRow( CompleterType.Immediate, CompilerType.System )]
    [DataRow( CompleterType.Immediate, CompilerType.Interpret )]
    public void AsyncBlock_ShouldWriteEnclosingBlockVariable( CompleterType completer, CompilerType compiler )
    {
        // Arrange: the capture is shared, so the write is visible after the block completes
        var outer = Variable( typeof( int ), "outer" );
        var task = Variable( typeof( Task<int> ), "task" );

        var block = Block(
            [outer, task],
            Assign( outer, Constant( 1 ) ),
            Assign( task, BlockAsync(
                Assign( outer, Await( AsyncHelper.Completer( Constant( completer ), Constant( 5 ) ) ) ) ) ),
            Call( typeof( Task ), nameof( Task.WaitAll ), Type.EmptyTypes, NewArrayInit( typeof( Task ), task ) ),
            outer );

        var lambda = Lambda<Func<int>>( block );
        var compiledLambda = lambda.Compile( compiler );

        // Act
        var value = compiledLambda();

        // Assert
        Assert.AreEqual( 5, value );
    }
}
