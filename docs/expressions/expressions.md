---
layout: default
title: Expressions
has_children: true
nav_order: 2
---

# Expressions

`Hyperbee.Expressions` provides custom expression types that extend the standard .NET expression tree model
with language constructs not natively available in `System.Linq.Expressions`.

All types reduce to standard expressions via `Expression.Reduce()`, making them compatible with any
expression visitor or compiler that accepts `LambdaExpression`.

---

## Factory Methods

All factory methods are static members of `ExpressionExtensions`. Import them with:

```csharp
using static Hyperbee.Expressions.ExpressionExtensions;
```

---

## Expression Types

| Expression | Factory | Description |
|------------|---------|-------------|
| [Async Block](async-block.md) | `BlockAsync(...)` | Async code block with state machine |
| [Enumerable Block](enumerable-block.md) | `BlockEnumerable(...)` | Yield-returning enumerable block |
| [Await](await.md) | `Await(...)` | Await a task or awaitable |
| [Yield](yield.md) | `YieldReturn(...)` / `YieldBreak()` | Yield a value or end enumeration |
| [For](for.md) | `For(...)` | `for` loop |
| [ForEach](foreach.md) | `ForEach(...)` | `foreach` loop |
| [While](while.md) | `While(...)` | `while` loop |
| [Using](using.md) | `Using(...)` | Scoped resource disposal |
| [String Format](string-format.md) | `StringFormat(...)` | Formatted string construction |
| [Debug](debug.md) | `Debug(...)` | Debug callback |
| [Inject](inject.md) | `Inject(...)` | Service resolution via `IServiceProvider` |
| [Configuration Value](configuration-value.md) | `ConfigurationValue(...)` | `IConfiguration` value access |
