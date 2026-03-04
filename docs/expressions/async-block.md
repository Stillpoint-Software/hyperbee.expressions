---
layout: default
title: Async Block
parent: Expressions
nav_order: 1
---

# Async Block

`AsyncBlockExpression` represents an asynchronous code block. When compiled, it automatically generates
a `IAsyncStateMachine` state machine that executes `AwaitExpression` nodes asynchronously, suspending
and resuming across `await` points.

The block returns `Task` (for void result) or `Task<T>` (when the last expression produces a value).

---

## Factory Methods

```csharp
using static Hyperbee.Expressions.ExpressionExtensions;
```

| Overload | Description |
|----------|-------------|
| `BlockAsync( params Expression[] expressions )` | Block with no local variables |
| `BlockAsync( ParameterExpression[] variables, params Expression[] expressions )` | Block with local variables |
| `BlockAsync( Expression[] expressions, ExpressionRuntimeOptions options )` | Block with runtime options |
| `BlockAsync( ParameterExpression[] variables, Expression[] expressions, ExpressionRuntimeOptions options )` | Block with variables and options |

---

## Usage

### Basic Async Block

```csharp
using static System.Linq.Expressions.Expression;
using static Hyperbee.Expressions.ExpressionExtensions;

// An async block that awaits two tasks and returns the sum
var a = Variable( typeof(int), "a" );
var b = Variable( typeof(int), "b" );

var asyncBlock = BlockAsync(
    [a, b],
    Assign( a, Await( Constant( Task.FromResult( 10 ) ) ) ),
    Assign( b, Await( Constant( Task.FromResult( 32 ) ) ) ),
    Add( a, b )   // return value: Task<int> with value 42
);

var lambda = Lambda<Func<Task<int>>>( asyncBlock );
var fn = lambda.Compile();
var result = await fn();  // result == 42
```

### Void Async Block

```csharp
// A void async block: returns Task (no result value)
var asyncBlock = BlockAsync(
    Await( Call( typeof(Task).GetMethod("Delay", [typeof(int)]), Constant( 100 ) ) ),
    Call( typeof(Console).GetMethod("WriteLine", [typeof(string)]), Constant( "done" ) )
);

var lambda = Lambda<Func<Task>>( asyncBlock );
var fn = lambda.Compile();
await fn();
```

### Using with Compiler Options

```csharp
var options = new ExpressionRuntimeOptions { Optimize = true };

var asyncBlock = BlockAsync(
    [a],
    Assign( a, Await( someTask ) ),
    a,
    options
);
```

---

## Type

The `Type` property returns `Task` when the last expression in the block is `void`, or `Task<T>` when
the last expression produces a value of type `T`.

---

## Notes

- `AwaitExpression` nodes must appear directly inside an `AsyncBlockExpression`. Awaiting outside an
  async block is not supported.
- Variables declared in the block are hoisted to state machine fields to survive suspension points.
- Nested `AsyncBlockExpression` blocks are supported — each generates its own state machine.
- See [ExpressionRuntimeOptions](../configuration/runtime-options.md) for configuration options.
- See [Await](await.md) for the `Await` factory method.
