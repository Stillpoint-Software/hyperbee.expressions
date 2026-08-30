using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;
using Hyperbee.Expressions.Compiler.Tests.TestSupport;
using Hyperbee.Expressions.CompilerServices;
using static System.Linq.Expressions.Expression;
using static Hyperbee.Expressions.ExpressionExtensions;

namespace Hyperbee.Expressions.Compiler.Tests.Integration;

/// <summary>
/// A coroutine block builds a state machine type when it reduces, and the compilation
/// pipeline reduces a node more than once. The reduction has to be cached or each pass emits
/// its own type and compiles its own MoveNext.
/// </summary>
/// <remarks>
/// BlockEnumerable did not cache, so a single compile built three state machine types and
/// used the last. That was most of what compiling one cost, and the discarded types stay in
/// the module for the life of the process.
/// </remarks>
[TestClass]
public class StateMachineReuseTests
{
    private sealed class IsolatedModuleProvider : IModuleBuilderProvider
    {
        private readonly ModuleBuilder _module = AssemblyBuilder
            .DefineDynamicAssembly( new AssemblyName( $"Reuse{Guid.NewGuid():N}" ), AssemblyBuilderAccess.Run )
            .DefineDynamicModule( "Reuse" );

        public ModuleBuilder GetModuleBuilder( ModuleKind kind ) => _module;

        public int TypeCount => _module.GetTypes().Length;
    }

    public static Task<int> Echo( int value ) => Task.FromResult( value );

    private static EnumerableBlockExpression EnumerableBlock( ExpressionRuntimeOptions options )
    {
        var local = Variable( typeof( int ), "local" );

        return BlockEnumerable(
            new[] { local },
            new Expression[]
            {
                Assign( local, Constant( 7 ) ),
                YieldReturn( local ),
                YieldReturn( Add( local, Constant( 1 ) ) )
            },
            options );
    }

    private static AsyncBlockExpression AsyncBlock( ExpressionRuntimeOptions options )
    {
        var local = Variable( typeof( int ), "local" );

        return BlockAsync(
            new[] { local },
            new Expression[]
            {
                Assign( local, Constant( 7 ) ),
                Assign( local, Await( Call( typeof( StateMachineReuseTests ), nameof( Echo ), Type.EmptyTypes, local ) ) ),
                local
            },
            options );
    }

    [TestMethod]
    public void EnumerableBlock_ReducesOnce()
    {
        // Arrange
        var block = EnumerableBlock( null );

        // Act
        var first = block.Reduce();
        var second = block.Reduce();

        // Assert
        Assert.AreSame( first, second );
    }

    [TestMethod]
    public void AsyncBlock_ReducesOnce()
    {
        // Arrange
        var block = AsyncBlock( null );

        // Act
        var first = block.Reduce();
        var second = block.Reduce();

        // Assert
        Assert.AreSame( first, second );
    }

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public void EnumerableBlock_EmitsOneStateMachineType( CompilerType compiler )
    {
        // Arrange
        var provider = new IsolatedModuleProvider();

        var lambda = Lambda<Func<IEnumerable<int>>>(
            EnumerableBlock( new ExpressionRuntimeOptions { ModuleBuilderProvider = provider } ) );

        // Act
        var compiled = lambda.Compile( compiler );

        // Assert
        Assert.AreEqual( 1, provider.TypeCount, "one compile should emit one state machine type" );
        CollectionAssert.AreEqual( new[] { 7, 8 }, compiled().ToArray() );
    }

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public void EnumerableBlock_ReadingAnEnclosingVariable_EmitsOneStateMachineType( CompilerType compiler )
    {
        // Arrange: the body reads the enclosing lambda's parameter
        var provider = new IsolatedModuleProvider();
        var input = Parameter( typeof( int ), "input" );

        var lambda = Lambda<Func<int, IEnumerable<int>>>(
            BlockEnumerable(
                new Expression[] { YieldReturn( input ), YieldReturn( Add( input, Constant( 1 ) ) ) },
                new ExpressionRuntimeOptions { ModuleBuilderProvider = provider } ),
            input );

        // Act
        var compiled = lambda.Compile( compiler );

        // Assert
        Assert.AreEqual( 1, provider.TypeCount, "one compile should emit one state machine type" );
        CollectionAssert.AreEqual( new[] { 7, 8 }, compiled( 7 ).ToArray() );
    }

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public async Task AsyncBlock_EmitsOneStateMachineType( CompilerType compiler )
    {
        // Arrange
        var provider = new IsolatedModuleProvider();

        var lambda = Lambda<Func<Task<int>>>(
            AsyncBlock( new ExpressionRuntimeOptions { ModuleBuilderProvider = provider } ) );

        // Act
        var compiled = lambda.Compile( compiler );

        // Assert
        Assert.AreEqual( 1, provider.TypeCount, "one compile should emit one state machine type" );
        Assert.AreEqual( 7, await compiled() );
    }
}
