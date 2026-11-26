---
layout: default
title: Hyperbee Expressions
nav_order: 1
---

# Hyperbee Expressions

`Hyperbee.Expressions` extends the .NET expression tree model with language constructs that the standard
`System.Linq.Expressions` library does not support: asynchronous workflows, enumerable state machines,
structured loops, resource disposal, string formatting, and dependency injection.

All custom expression types reduce to standard expression trees, so they work with any compiler that
accepts `LambdaExpression` -- including the System compiler, [FastExpressionCompiler](https://github.com/dadhi/FastExpressionCompiler),
and the [Hyperbee Expression Compiler](https://github.com/Stillpoint-Software/hyperbee.expressions.compiler),
which these expressions are designed to pair with.

---

## Packages

| Package | Description | NuGet |
|---------|-------------|-------|
| `Hyperbee.Expressions` | Core expression extensions | [![NuGet](https://img.shields.io/nuget/v/Hyperbee.Expressions.svg)](https://www.nuget.org/packages/Hyperbee.Expressions) |
| `Hyperbee.Expressions.Abstractions` | Compiler and coroutine contracts | [![NuGet](https://img.shields.io/nuget/v/Hyperbee.Expressions.Abstractions.svg)](https://www.nuget.org/packages/Hyperbee.Expressions.Abstractions) |
| `Hyperbee.Expressions.Lab` | Experimental expressions (fetch, JSON, map/reduce) | [![NuGet](https://img.shields.io/nuget/v/Hyperbee.Expressions.Lab.svg)](https://www.nuget.org/packages/Hyperbee.Expressions.Lab) |

The expression compiler is built and documented in its own repository,
[hyperbee.expressions.compiler](https://github.com/Stillpoint-Software/hyperbee.expressions.compiler).

---

## Getting Started

```
dotnet add package Hyperbee.Expressions
```

All factory methods live in `ExpressionExtensions`. Import them with:

```csharp
using static Hyperbee.Expressions.ExpressionExtensions;
```

---

## Quick Example

```csharp
using static System.Linq.Expressions.Expression;
using static Hyperbee.Expressions.ExpressionExtensions;

// Build an async expression that awaits two tasks sequentially
var result1 = Variable( typeof(int), "result1" );
var result2 = Variable( typeof(int), "result2" );

var expr = BlockAsync(
    [result1, result2],
    Assign( result1, Await( Call( typeof(MyService).GetMethod("GetValueAsync") ) ) ),
    Assign( result2, Await( Call( typeof(MyService).GetMethod("GetOtherAsync"), result1 ) ) ),
    result2
);

var lambda = Lambda<Func<Task<int>>>( expr );
var fn = lambda.Compile();
var value = await fn();
```

---

## Expression Types

### Async

| Type | Factory Method | Description |
|------|----------------|-------------|
| [`AsyncBlockExpression`](expressions/async-block.md) | `BlockAsync(...)` | Async code block with generated state machine |
| [`AwaitExpression`](expressions/await.md) | `Await(...)` | Await a task or awaitable |

### Enumerable / Yield

| Type | Factory Method | Description |
|------|----------------|-------------|
| [`EnumerableBlockExpression`](expressions/enumerable-block.md) | `BlockEnumerable(...)` | Enumerable block with generated state machine |
| [`YieldExpression`](expressions/yield.md) | `YieldReturn(...)` / `YieldBreak()` | Yield a value or break from enumeration |

### Loops

| Type | Factory Method | Description |
|------|----------------|-------------|
| [`ForExpression`](expressions/for.md) | `For(...)` | `for` loop with init / test / iteration |
| [`ForEachExpression`](expressions/foreach.md) | `ForEach(...)` | `foreach` loop over any `IEnumerable` |
| [`WhileExpression`](expressions/while.md) | `While(...)` | `while` loop |

### Resource Management

| Type | Factory Method | Description |
|------|----------------|-------------|
| [`UsingExpression`](expressions/using.md) | `Using(...)` | `using` block for `IDisposable` |

### Utilities

| Type | Factory Method | Description |
|------|----------------|-------------|
| [`StringFormatExpression`](expressions/string-format.md) | `StringFormat(...)` | `string.Format` in an expression |
| [`DebugExpression`](expressions/debug.md) | `Debug(...)` | Debug callback for expression trees |
| [`InjectExpression`](expressions/inject.md) | `Inject(...)` | Resolve a service from `IServiceProvider` |
| [`ConfigurationExpression`](expressions/configuration-value.md) | `ConfigurationValue(...)` | Read a value from `IConfiguration` |

---

## Compiler

These expressions are designed to pair with the
[Hyperbee Expression Compiler](https://github.com/Stillpoint-Software/hyperbee.expressions.compiler) (HEC),
an IR-based compiler that emits IL directly in place of `Expression.Compile()`.

Speed is the reason. HEC compiles 9.3-48x faster than the System compiler and allocates 29-57%
less. Coroutines gain most: `BlockAsync` and `BlockEnumerable` compile about twice as fast and
the compiled state machines run 16-24x faster, because HEC emits the `MoveNext` body into the
state machine type rather than leaving a delegate to invoke on each resume.

It is optional. HEC plugs in through the contracts in `Hyperbee.Expressions.Abstractions`, so
nothing here depends on it.

```
dotnet add package Hyperbee.Expressions.Compiler
```

Its documentation, benchmarks and architecture live in
[its own repository](https://github.com/Stillpoint-Software/hyperbee.expressions.compiler).

---

## Lab

`Hyperbee.Expressions.Lab` provides experimental expression types for HTTP fetch, JSON parsing, and
collection map/reduce operations.

```
dotnet add package Hyperbee.Expressions.Lab
```

See [Lab](lab/lab.md) for details.

---

## Credits

Special thanks to:

- Sergey Tepliakov -- [Dissecting the async methods in C#](https://devblogs.microsoft.com/premier-developer/dissecting-the-async-methods-in-c/)
- [Fast Expression Compiler](https://github.com/dadhi/FastExpressionCompiler) for improved performance
- [Just The Docs](https://github.com/just-the-docs/just-the-docs) for the documentation theme

## Contributing

We welcome contributions! Please see our [Contributing Guide](https://github.com/Stillpoint-Software/.github/blob/main/.github/CONTRIBUTING.md)
for more details.
