using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using Hyperbee.Expressions.Compiler.Tests.TestSupport;
using static System.Linq.Expressions.Expression;
using static Hyperbee.Expressions.ExpressionExtensions;

namespace Hyperbee.Expressions.Compiler.Tests.Expressions;

/// <summary>
/// Awaits custom awaitables across the shapes C# accepts: value or reference type, for both
/// the awaitable and its awaiter, and a result type that is not a generic argument.
/// </summary>
/// <remarks>
/// Every awaitable in the suite was a class whose awaiter was itself, generic, and also a
/// class. That left the value-type shapes unexercised -- and a struct awaitable is the
/// ordinary case, not the exotic one: Task.Yield() returns one, and so does ConfigureAwait.
/// </remarks>
[TestClass]
public class CustomAwaitableShapeTests
{
    // Awaitable is a struct, awaiter is a struct

    public readonly struct StructAwaitable( int value )
    {
        public StructAwaiter GetAwaiter() => new( value );
    }

    public readonly struct StructAwaiter( int value ) : ICriticalNotifyCompletion
    {
        public bool IsCompleted => true;
        public int GetResult() => value;
        public void OnCompleted( Action continuation ) => continuation();
        public void UnsafeOnCompleted( Action continuation ) => continuation();
    }

    public static StructAwaitable StructAwaitableOf( int value ) => new( value );

    // Awaitable is a class, awaiter is a struct

    public sealed class ClassAwaitable( int value )
    {
        public StructAwaiter GetAwaiter() => new( value );
    }

    public static ClassAwaitable ClassAwaitableOf( int value ) => new( value );

    // Awaitable and awaiter are both classes, and the awaiter is not generic

    public sealed class ClassAwaitableClassAwaiter( int value )
    {
        public ClassAwaiter GetAwaiter() => new( value );
    }

    public sealed class ClassAwaiter( int value ) : ICriticalNotifyCompletion
    {
        public bool IsCompleted => true;
        public int GetResult() => value;
        public void OnCompleted( Action continuation ) => continuation();
        public void UnsafeOnCompleted( Action continuation ) => continuation();
    }

    public static ClassAwaitableClassAwaiter ClassAwaiterOf( int value ) => new( value );

    // Offers ConfigureAwait, and the awaitable it returns yields the same awaiter type.
    // The configured form reports a different value so the two paths are distinguishable.

    public sealed class ConfigurableAwaitable( int value )
    {
        public StructAwaiter GetAwaiter() => new( value );

        public ConfiguredForm ConfigureAwait( bool continueOnCapturedContext ) => new( value + 1 );
    }

    public readonly struct ConfiguredForm( int value )
    {
        public StructAwaiter GetAwaiter() => new( value );
    }

    public static ConfigurableAwaitable ConfigurableOf( int value ) => new( value );

    // Offers ConfigureAwait, but the configured form yields a different awaiter, so
    // configureAwait cannot be honored and is ignored.

    public sealed class MismatchedConfigurable( int value )
    {
        public StructAwaiter GetAwaiter() => new( value );

        public Task<int> ConfigureAwait( bool continueOnCapturedContext ) => Task.FromResult( value + 1 );
    }

    public static MismatchedConfigurable MismatchedOf( int value ) => new( value );

    // A struct whose GetAwaiter is an extension method, so the awaitable is passed by value
    // rather than by pointer

    public readonly struct ExtensionAwaitable( int value )
    {
        public int Value => value;
    }

    public static ExtensionAwaitable ExtensionAwaitableOf( int value ) => new( value );

    private static Expression<Func<Task<int>>> AwaitOne( string factory, bool configureAwait = false )
    {
        var awaitable = Call( typeof( CustomAwaitableShapeTests ), factory, Type.EmptyTypes, Constant( 42 ) );

        return Lambda<Func<Task<int>>>( BlockAsync( Await( awaitable, configureAwait ) ) );
    }

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public async Task StructAwaitable_StructAwaiter( CompilerType compiler )
    {
        Assert.AreEqual( 42, await AwaitOne( nameof( StructAwaitableOf ) ).Compile( compiler )() );
    }

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public async Task ClassAwaitable_StructAwaiter( CompilerType compiler )
    {
        Assert.AreEqual( 42, await AwaitOne( nameof( ClassAwaitableOf ) ).Compile( compiler )() );
    }

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public async Task ClassAwaitable_ClassAwaiter_NonGenericResult( CompilerType compiler )
    {
        Assert.AreEqual( 42, await AwaitOne( nameof( ClassAwaiterOf ) ).Compile( compiler )() );
    }

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public async Task TaskYield_Suspends( CompilerType compiler )
    {
        // Task.Yield returns a struct awaitable whose awaiter is a struct -- the shape the
        // suite never had, reached the way anyone would reach it.
        var yieldMethod = typeof( Task ).GetMethod( nameof( Task.Yield ), Type.EmptyTypes )!;

        var local = Variable( typeof( int ), "local" );

        var lambda = Lambda<Func<Task<int>>>(
            BlockAsync(
                new[] { local },
                new Expression[]
                {
                    Assign( local, Constant( 0 ) ),
                    Await( Call( yieldMethod ) ),
                    Assign( local, Add( local, Constant( 42 ) ) ),
                    local
                } ) );

        Assert.AreEqual( 42, await lambda.Compile( compiler )() );
    }

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public async Task ConfigureAwait_False_UsesTheConfiguredAwaitable( CompilerType compiler )
    {
        // Arrange & Act
        var result = await AwaitOne( nameof( ConfigurableOf ), configureAwait: false ).Compile( compiler )();

        // Assert: the configured form reports value + 1
        Assert.AreEqual( 43, result );
    }

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public async Task ConfigureAwait_True_UsesTheAwaitableItself( CompilerType compiler )
    {
        // Arrange & Act
        var result = await AwaitOne( nameof( ConfigurableOf ), configureAwait: true ).Compile( compiler )();

        // Assert
        Assert.AreEqual( 42, result );
    }

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public async Task ConfigureAwait_IsIgnored_WhenTheConfiguredFormHasAnotherAwaiter( CompilerType compiler )
    {
        // Arrange & Act
        var result = await AwaitOne( nameof( MismatchedOf ), configureAwait: false ).Compile( compiler )();

        // Assert: honoring it is not expressible, so the awaitable is used as-is
        Assert.AreEqual( 42, result );
    }

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public async Task StructAwaitable_ExtensionGetAwaiter( CompilerType compiler )
    {
        Assert.AreEqual( 42, await AwaitOne( nameof( ExtensionAwaitableOf ) ).Compile( compiler )() );
    }
}

public static class ExtensionAwaitableExtensions
{
    public static CustomAwaitableShapeTests.StructAwaiter GetAwaiter(
        this CustomAwaitableShapeTests.ExtensionAwaitable awaitable ) => new( awaitable.Value );
}
