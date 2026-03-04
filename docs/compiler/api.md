---
layout: default
title: API Reference
parent: Compiler
nav_order: 2
---

# API Reference

---

## HyperbeeCompiler

`HyperbeeCompiler` is a static class — the primary entry point for all compilation operations.

```csharp
using Hyperbee.Expressions.Compiler;
```

### Compile

```csharp
static TDelegate Compile<TDelegate>( Expression<TDelegate> lambda, CompilerDiagnostics? diagnostics = null )
    where TDelegate : Delegate

static Delegate Compile( LambdaExpression lambda, CompilerDiagnostics? diagnostics = null )
```

Compiles the expression tree and returns a delegate. Throws on unsupported patterns.

```csharp
var fn = HyperbeeCompiler.Compile<Func<int, int>>(
    x => Expression.Add( x, Expression.Constant( 1 ) )
);
Console.WriteLine( fn( 41 ) );  // 42
```

### TryCompile

```csharp
static TDelegate? TryCompile<TDelegate>( Expression<TDelegate> lambda ) where TDelegate : Delegate
static Delegate?  TryCompile( LambdaExpression lambda )
```

Compiles and returns `null` on failure instead of throwing.

```csharp
var fn = HyperbeeCompiler.TryCompile( lambda );
if ( fn is null )
    Console.WriteLine( "Compilation failed" );
```

### CompileWithFallback

```csharp
static TDelegate CompileWithFallback<TDelegate>( Expression<TDelegate> lambda ) where TDelegate : Delegate
static Delegate  CompileWithFallback( LambdaExpression lambda )
```

Attempts HEC compilation; falls back to `lambda.Compile()` (System compiler) on failure.
Use during migration when some expressions may not yet be supported.

```csharp
var fn = HyperbeeCompiler.CompileWithFallback( lambda );
```

### UseAsDefault / ClearDefault

```csharp
static ICoroutineDelegateBuilder? UseAsDefault()
static ICoroutineDelegateBuilder? ClearDefault()
```

Sets or clears HEC as the process-wide default builder for `AsyncBlockExpression` reductions.
Call at application startup to make HEC compile all `BlockAsync` state machines, even when the
outer lambda is compiled by another compiler.

```csharp
// In Program.cs or AssemblyInitialize
HyperbeeCompiler.UseAsDefault();

// In test teardown
HyperbeeCompiler.ClearDefault();
```

### CompileToMethod

```csharp
static void CompileToMethod( LambdaExpression lambda, MethodBuilder method )
static bool TryCompileToMethod( LambdaExpression lambda, MethodBuilder method )
```

Emits the expression tree directly into a `MethodBuilder`. The method must be `static` and its
parameter signature must match the lambda.

Non-embeddable constants (object references, delegates, nested lambdas) are not permitted —
all constants must be embeddable IL values (primitives, `Type` tokens, `null`).

```csharp
var typeBuilder = moduleBuilder.DefineType( "MyType" );
var methodBuilder = typeBuilder.DefineMethod(
    "Add",
    MethodAttributes.Public | MethodAttributes.Static,
    typeof(int), [typeof(int), typeof(int)]
);

var a = Parameter( typeof(int), "a" );
var b = Parameter( typeof(int), "b" );
var lambda = Lambda( Add( a, b ), a, b );

HyperbeeCompiler.CompileToMethod( lambda, methodBuilder );

var type = typeBuilder.CreateType();
var method = type.GetMethod("Add")!;
Console.WriteLine( method.Invoke( null, [10, 32] ) );  // 42
```

### CompileToInstanceMethod

```csharp
static void CompileToInstanceMethod( LambdaExpression lambda, MethodBuilder method )
static bool TryCompileToInstanceMethod( LambdaExpression lambda, MethodBuilder method )
```

Like `CompileToMethod` but without the static-method requirement. For instance methods on value
types (e.g., `IAsyncStateMachine.MoveNext()`), the lambda's first parameter maps to IL `arg.0`
(the implicit `this` managed pointer).

---

## HyperbeeExpressionCompiler

`HyperbeeExpressionCompiler` implements `IExpressionCompiler` as a DI-friendly singleton wrapper
around `HyperbeeCompiler`.

```csharp
public sealed class HyperbeeExpressionCompiler : IExpressionCompiler
{
    public static readonly IExpressionCompiler Instance;

    public Delegate  Compile( LambdaExpression lambda );
    public TDelegate Compile<TDelegate>( Expression<TDelegate> lambda ) where TDelegate : Delegate;
    public Delegate?  TryCompile( LambdaExpression lambda );
    public TDelegate? TryCompile<TDelegate>( Expression<TDelegate> lambda ) where TDelegate : Delegate;

    public static ICoroutineDelegateBuilder? UseAsDefault();
}
```

### DI Registration

```csharp
// Register HEC as the IExpressionCompiler implementation
services.AddSingleton<IExpressionCompiler>( HyperbeeExpressionCompiler.Instance );
```

### With UseAsDefault

`HyperbeeExpressionCompiler.UseAsDefault()` is a convenience passthrough to
`HyperbeeCompiler.UseAsDefault()`. Call it at startup to make HEC compile all `BlockAsync`
state machines, even in expressions compiled by the System compiler.

```csharp
// In Program.cs
HyperbeeExpressionCompiler.UseAsDefault();
```

---

## HyperbeeCompilerExtensions

Extension methods for `LambdaExpression`:

```csharp
// In namespace Hyperbee.Expressions.Compiler
public static TDelegate CompileHyperbee<TDelegate>( this Expression<TDelegate> lambda )
    where TDelegate : Delegate
```

```csharp
using Hyperbee.Expressions.Compiler;

var fn = lambda.CompileHyperbee();
```

---

## Notes

- All `HyperbeeCompiler` methods are thread-safe — there is no shared mutable state.
- `CompileToMethod` and `CompileToInstanceMethod` do not support closures. Use `Compile()` for
  expressions with captured variables or non-embeddable constants.
- See [Diagnostics](diagnostics.md) for `CompilerDiagnostics` and IR capture.
- See [Performance](performance.md) for benchmark comparisons.
