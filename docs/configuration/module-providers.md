---
layout: default
title: Module Providers
parent: Configuration
nav_order: 2
---

# Module Providers

`IModuleBuilderProvider` controls where the dynamic type generated for `AsyncBlockExpression` and
`EnumerableBlockExpression` state machines is emitted. Two built-in implementations are provided.

---

## Interface

```csharp
public interface IModuleBuilderProvider
{
    ModuleBuilder GetModuleBuilder( ModuleKind kind );
}

public enum ModuleKind
{
    Async,
    Enumerable
}
```

---

## Built-in Providers

### DefaultModuleBuilderProvider

Emits state machine types into a long-lived, non-collectible dynamic assembly. This is the default.

```csharp
public sealed class DefaultModuleBuilderProvider : IModuleBuilderProvider
{
    public static readonly IModuleBuilderProvider Instance;
}
```

- **Assembly lifetime:** application lifetime
- **Unloadable:** no
- **Thread-safe:** yes
- **Suitable for:** production use, most scenarios

### CollectibleModuleBuilderProvider

Emits state machine types into a collectible `AssemblyLoadContext`. Types can be unloaded when all
references are released, allowing memory to be reclaimed.

```csharp
public sealed class CollectibleModuleBuilderProvider : IModuleBuilderProvider
{
    public static readonly IModuleBuilderProvider Instance;
}
```

- **Assembly lifetime:** until all references are released
- **Unloadable:** yes (via `AssemblyLoadContext`)
- **Thread-safe:** yes
- **Suitable for:** scenarios with many short-lived lambdas, plugin systems, test isolation

---

## Usage

### Default (Implicit)

```csharp
// DefaultModuleBuilderProvider is used automatically -- no configuration needed
var asyncBlock = BlockAsync( Await( someTask ) );
```

### Collectible Assembly

```csharp
var options = new ExpressionRuntimeOptions
{
    ModuleBuilderProvider = CollectibleModuleBuilderProvider.Instance
};

var asyncBlock = BlockAsync(
    [result],
    Assign( result, Await( someTask ) ),
    result,
    options
);
```

### Custom Provider

Implement `IModuleBuilderProvider` to emit types into a specific assembly or to integrate with a
custom `AssemblyLoadContext`:

```csharp
public class MyModuleBuilderProvider : IModuleBuilderProvider
{
    private readonly ModuleBuilder _module;

    public MyModuleBuilderProvider( AssemblyBuilder assembly )
    {
        _module = assembly.DefineDynamicModule( "StateMachines" );
    }

    public ModuleBuilder GetModuleBuilder( ModuleKind kind ) => _module;
}
```

```csharp
var options = new ExpressionRuntimeOptions
{
    ModuleBuilderProvider = new MyModuleBuilderProvider( myAssemblyBuilder )
};
```

---

## Notes

- Each call to `GetModuleBuilder` may return the same or a new `ModuleBuilder` depending on the
  implementation. The default implementations return the same module for all calls.
- In test projects, use `CollectibleModuleBuilderProvider` when generating many lambda compilations
  to avoid `OutOfMemoryException` from accumulated dynamic types.
- `ModuleKind` is passed to the provider to allow routing `Async` and `Enumerable` state machines
  to different modules if needed.
