using Hyperbee.Expressions.CompilerServices;

namespace Hyperbee.Expressions.Tests;

// These six types moved from Hyperbee.Expressions to Hyperbee.Expressions.Abstractions in
// 1.7.0. Assemblies compiled before the move, such as Hyperbee.Expressions.Compiler 1.6.x,
// still reference them in Hyperbee.Expressions and bind only through the forwards in
// src/Hyperbee.Expressions/AbstractionsForwards.cs. Removing one breaks those binaries at
// runtime and nothing else would notice, so each forward is pinned here.

[TestClass]
public class AbstractionsForwardsTests
{
    private static readonly Type[] MovedTypes =
    [
        typeof( ICoroutineDelegateBuilder ),
        typeof( ICoroutineMethodBuilder ),
        typeof( CoroutineBuilderContext ),
        typeof( CoroutineClosureRewriter ),
        typeof( IExpressionCompiler ),
        typeof( SystemExpressionCompiler )
    ];

    [TestMethod]
    public void MovedTypes_ShouldLiveInAbstractions_AndBeForwardedFromExpressions()
    {
        var forwarded = typeof( ExpressionRuntimeOptions ).Assembly.GetForwardedTypes();

        foreach ( var type in MovedTypes )
        {
            Assert.AreEqual( "Hyperbee.Expressions.Abstractions", type.Assembly.GetName().Name, type.FullName );
            CollectionAssert.Contains( forwarded, type, $"{type.FullName} is not forwarded" );
        }
    }

    [TestMethod]
    public void MovedTypes_ShouldResolveByTheirPreMoveAssemblyQualifiedName()
    {
        // This is the lookup an assembly compiled before 1.7.0 performs at runtime.
        foreach ( var type in MovedTypes )
        {
            var resolved = Type.GetType( $"{type.FullName}, Hyperbee.Expressions", throwOnError: true );
            Assert.AreSame( type, resolved, type.FullName );
        }
    }
}
