---
layout: default
title: Runtime Options
parent: Configuration
nav_order: 1
---

# Runtime Options

`ExpressionRuntimeOptions` configures how `AsyncBlockExpression` and `EnumerableBlockExpression`
generate their state machines. Pass an instance to the `BlockAsync(...)` or `BlockEnumerable(...)`
factory methods.

---

## Properties

```csharp
public class ExpressionRuntimeOptions
{
    public IModuleBuilderProvider ModuleBuilderProvider { get; init; }
    public bool Optimize { get; init; } = true;
    public Action<string>? ExpressionCapture { get; init; }
}
```

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `ModuleBuilderProvider` | `IModuleBuilderProvider` | `DefaultModuleBuilderProvider.Instance` | Controls where the generated dynamic type is emitted |
| `Optimize` | `bool` | `true` | Enables state graph optimizations (goto elimination, dead state removal). Set to `false` to preserve the raw lowered graph for debugging |
| `ExpressionCapture` | `Action<string>?` | `null` | When set, receives the `DebugView` string of the generated state machine expression before compilation |

---

## Usage

### Default (No Options)

```csharp
// Options are optional -- defaults are suitable for production use
var asyncBlock = BlockAsync(
    Await( someTask )
);
```

### Disable Optimization

```csharp
var options = new ExpressionRuntimeOptions { Optimize = false };

var asyncBlock = BlockAsync(
    [result],
    Assign( result, Await( someTask ) ),
    result,
    options
);
```

Disable optimization when you need to inspect the raw state machine structure, or when debugging
unexpected compilation behavior.

### Capture the State Machine Expression

```csharp
var options = new ExpressionRuntimeOptions
{
    ExpressionCapture = debugView => File.WriteAllText( "statemachine.txt", debugView )
};

var asyncBlock = BlockAsync(
    [result],
    Assign( result, Await( someTask ) ),
    result,
    options
);

// Compiling this block writes the state machine DebugView to disk
var lambda = Lambda<Func<Task<int>>>( asyncBlock );
lambda.Compile();
```

### Collectible Assembly

```csharp
// Use CollectibleModuleBuilderProvider if the generated type must be unloadable
var options = new ExpressionRuntimeOptions
{
    ModuleBuilderProvider = CollectibleModuleBuilderProvider.Instance
};
```

See [Module Providers](module-providers.md) for details.

---

## Notes

- `ExpressionRuntimeOptions` uses `init` properties -- create a new instance per-block; do not share
  mutable state across blocks.
- The `ExpressionCapture` callback fires once per `Reduce()` call, which occurs at compile time.
- Optimization is a state graph pass (`StateOptimizer`) that runs after lowering, not an IR pass.
  It is unrelated to `Hyperbee.Expressions.Compiler` IR optimizations.
