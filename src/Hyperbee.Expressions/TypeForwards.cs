using System.Runtime.CompilerServices;
using Hyperbee.Expressions;
using Hyperbee.Expressions.CompilerServices;

// These six types moved from Hyperbee.Expressions into Hyperbee.Expressions.Abstractions
// so a compiler can implement the coroutine contract without taking a dependency on this
// library and the four packages it brings with it.
//
// Their namespaces did not change, so source that uses them still compiles untouched. An
// assembly already compiled against them, however, carries a reference to this assembly
// for each one, and would fail to load at runtime without these forwards. The CLR follows
// a forward transparently, so an old binary binds to the type in its new home.
//
// Remove a forward only in a major version, and only after the type has lived in
// Abstractions long enough that nothing in the wild still references it here.

[assembly: TypeForwardedTo( typeof( ICoroutineDelegateBuilder ) )]
[assembly: TypeForwardedTo( typeof( ICoroutineMethodBuilder ) )]
[assembly: TypeForwardedTo( typeof( CoroutineBuilderContext ) )]
[assembly: TypeForwardedTo( typeof( CoroutineClosureRewriter ) )]
[assembly: TypeForwardedTo( typeof( IExpressionCompiler ) )]
[assembly: TypeForwardedTo( typeof( SystemExpressionCompiler ) )]
