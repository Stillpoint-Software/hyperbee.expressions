---
layout: default
title: Inject
parent: Expressions
nav_order: 11
---

# Inject

`InjectExpression` resolves a service from an `IServiceProvider` at runtime. It is the expression tree
equivalent of constructor injection or `IServiceProvider.GetRequiredService<T>()`.

At compile time the expression tree captures the service type. At runtime the service provider is
injected by calling `Compile(serviceProvider)` on the lambda, which replaces all `InjectExpression`
nodes with their resolved values before compilation.

---

## Factory Methods

```csharp
using static Hyperbee.Expressions.ExpressionExtensions;
```

| Overload | Description |
|----------|-------------|
| `Inject( Type type, IServiceProvider sp, string key = null, Expression defaultValue = null )` | Resolve by type with provider |
| `Inject( Type type, string key = null, Expression defaultValue = null )` | Resolve by type -- provider supplied at compile time |
| `Inject<T>( IServiceProvider sp, string key = null, Expression defaultValue = null )` | Generic resolve with provider |
| `Inject<T>( string key = null, Expression defaultValue = null )` | Generic resolve -- provider supplied at compile time |

---

## Usage

### Inject at Compile Time

The idiomatic pattern is to supply the `IServiceProvider` when compiling, not when building the tree.
This allows the tree to be built once and compiled with different containers.

```csharp
using static System.Linq.Expressions.Expression;
using static Hyperbee.Expressions.ExpressionExtensions;

// Build the tree (no IServiceProvider yet)
var injected = Inject<IMyService>();

var expr = Block(
    Call( injected, typeof(IMyService).GetMethod("Execute")! )
);

var lambda = Lambda<Action>( expr );

// Compile with the service provider -- Inject nodes are resolved here
var fn = lambda.Compile( serviceProvider );
fn();
```

### Inject with a Named Key

```csharp
// Keyed services (requires .NET 8 / IKeyedServiceProvider)
var service = Inject<IMyService>( key: "primary" );
```

### Inject with a Default Value

```csharp
// Falls back to defaultValue if the service is not registered
var service = Inject<IMyService>(
    defaultValue: Constant( new NoopMyService() )
);
```

### Inject by Type

```csharp
var service = Inject( typeof(IMyService) );
```

---

## Compiling with a Service Provider

```csharp
// Extension method on LambdaExpression
public static TResult Compile<TResult>(
    this Expression<TResult> expression,
    IServiceProvider serviceProvider,
    bool preferInterpretation = false )
```

```csharp
var fn = lambda.Compile( serviceProvider );
```

This walks the expression tree, finds all `IDependencyInjectionExpression` nodes (including `InjectExpression`
and `ConfigurationExpression`), sets the provider on each, then compiles the result.

---

## Notes

- `InjectExpression` implements `IDependencyInjectionExpression`, which is the marker interface used
  by `Compile(serviceProvider)` to find all nodes that need a provider.
- If no `IServiceProvider` is set and no `defaultValue` is provided, accessing the service at runtime
  throws `InvalidOperationException`.
- See [Configuration Value](configuration-value.md) for `IConfiguration` access.
- See [Dependency Injection](../configuration/dependency-injection.md) for the full DI compilation pattern.
