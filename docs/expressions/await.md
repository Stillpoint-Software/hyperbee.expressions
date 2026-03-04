---
layout: default
title: Await
parent: Expressions
nav_order: 3
---

# Await

`AwaitExpression` represents an `await` operation inside an `AsyncBlockExpression`. It suspends
execution until the awaitable completes, then resumes with the result.

Any awaitable type is supported — `Task`, `Task<T>`, `ValueTask`, `ValueTask<T>`, or any type
that provides a `GetAwaiter()` method returning an `INotifyCompletion` implementation.

---

## Factory Method

```csharp
using static Hyperbee.Expressions.ExpressionExtensions;
```

```csharp
AwaitExpression Await( Expression expression, bool configureAwait = false )
```

| Parameter | Type | Description |
|-----------|------|-------------|
| `expression` | `Expression` | An expression that produces an awaitable (`Task`, `Task<T>`, `ValueTask<T>`, etc.) |
| `configureAwait` | `bool` | Whether to call `.ConfigureAwait(false)`. Default: `false` |

---

## Usage

### Await a Task

```csharp
using static System.Linq.Expressions.Expression;
using static Hyperbee.Expressions.ExpressionExtensions;

var result = Variable( typeof(string), "result" );

var asyncBlock = BlockAsync(
    [result],
    Assign( result, Await( Call( typeof(MyService).GetMethod("FetchAsync") ) ) ),
    result
);

var lambda = Lambda<Func<Task<string>>>( asyncBlock );
```

### Await with ConfigureAwait(false)

```csharp
var asyncBlock = BlockAsync(
    Await(
        Call( typeof(Task).GetMethod("Delay", [typeof(int)]), Constant( 500 ) ),
        configureAwait: true   // emits .ConfigureAwait(false)
    )
);
```

### Await ValueTask

```csharp
// Awaiting ValueTask<int>
var asyncBlock = BlockAsync(
    [result],
    Assign( result, Await( Call( typeof(MyService).GetMethod("GetValueTaskAsync") ) ) )
);
```

---

## Type

The `Type` property returns the result type of the awaitable:
- `Task` → `void`
- `Task<T>` → `T`
- `ValueTask<T>` → `T`

---

## Notes

- `Await` must be used inside an `AsyncBlockExpression`. Using it elsewhere causes a compile-time error.
- The enclosing `BlockAsync` wraps all awaited continuations in the generated state machine.
- See [Async Block](async-block.md) for the enclosing block type.
