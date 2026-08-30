using System.Linq.Expressions;
using Hyperbee.Expressions.Compiler.Tests.TestSupport;
using static System.Linq.Expressions.Expression;
using static Hyperbee.Expressions.ExpressionExtensions;

namespace Hyperbee.Expressions.Compiler.Tests.Integration;

/// <summary>
/// State machine types can be scoped to a provider instead of the process.
/// </summary>
[TestClass]
public class CollectibleModuleTests
{
    public static Task<int> Echo( int value ) => Task.FromResult( value );

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public async Task AsyncBlock_RunsFromACollectibleAssembly( CompilerType compiler )
    {
        // Arrange
        var provider = new CollectibleModuleBuilderProvider();
        var local = Variable( typeof( int ), "local" );

        var lambda = Lambda<Func<Task<int>>>(
            BlockAsync(
                new[] { local },
                new Expression[]
                {
                    Assign( local, Await( Call( typeof( CollectibleModuleTests ), nameof( Echo ), Type.EmptyTypes, Constant( 42 ) ) ) ),
                    local
                },
                new ExpressionRuntimeOptions { ModuleBuilderProvider = provider } ) );

        // Act
        var compiled = lambda.Compile( compiler );

        // Assert
        // The async machine is not handed back, so only that it runs can be checked here.
        // The enumerable case below asserts the assembly directly.
        Assert.AreEqual( 42, await compiled() );
    }

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public void EnumerableBlock_RunsFromACollectibleAssembly( CompilerType compiler )
    {
        // Arrange
        var provider = new CollectibleModuleBuilderProvider();
        var local = Variable( typeof( int ), "local" );

        var lambda = Lambda<Func<IEnumerable<int>>>(
            BlockEnumerable(
                new[] { local },
                new Expression[]
                {
                    Assign( local, Constant( 7 ) ),
                    YieldReturn( local ),
                    YieldReturn( Add( local, Constant( 1 ) ) )
                },
                new ExpressionRuntimeOptions { ModuleBuilderProvider = provider } ) );

        // Act
        var sequence = lambda.Compile( compiler )();

        // Assert
        CollectionAssert.AreEqual( new[] { 7, 8 }, sequence.ToArray() );
        Assert.IsTrue( sequence.GetType().Assembly.IsCollectible,
            "the state machine type should come from the collectible assembly" );
    }

    [TestMethod]
    public void DefaultProvider_IsNotCollectible()
    {
        // Arrange: the process-wide provider trades collection for sharing, on purpose
        var local = Variable( typeof( int ), "local" );

        var lambda = Lambda<Func<IEnumerable<int>>>(
            BlockEnumerable( new[] { local }, new Expression[] { YieldReturn( Constant( 1 ) ) } ) );

        // Act
        var sequence = lambda.Compile( CompilerType.Hyperbee )();

        // Assert
        Assert.IsFalse( sequence.GetType().Assembly.IsCollectible );
    }
}
