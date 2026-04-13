---
layout: default
title: Dependency Injection
parent: Configuration
nav_order: 3
---

# Dependency Injection

`Hyperbee.Expressions` supports dependency injection through two mechanisms:

1. **Expression-level injection** -- `InjectExpression` and `ConfigurationExpression` resolve services
   at compile time by walking the expression tree and setting an `IServiceProvider`.

2. **Compiler-level injection** -- `IExpressionCompiler` is a DI-friendly interface for injectable
   compilation, with built-in implementations for the System compiler and HEC.

---

## Compiling with a Service Provider

The `Compile(serviceProvider)` extension method walks an expression tree, resolves all
`IDependencyInjectionExpression` nodes against the provider, and compiles the result.

```csharp
// Extension methods on LambdaExpression
public static TResult Compile<TResult>(
    this Expression<TResult> expression,
    IServiceProvider serviceProvider,
    bool preferInterpretation = false )

public static Delegate Compile(
    this LambdaExpression expression,
    IServiceProvider serviceProvider,
    bool preferInterpretation = false )
```

### Example

```csharp
using static System.Linq.Expressions.Expression;
using static Hyperbee.Expressions.ExpressionExtensions;

// Build the tree with DI-dependent expressions
var service = Inject<IGreetingService>();
var config  = ConfigurationValue<string>( "App:Greeting" );

var expr = Block(
    Call(
        typeof(Console).GetMethod("WriteLine", [typeof(string)])!,
        Call( service, typeof(IGreetingService).GetMethod("Greet")! )
    )
);

var lambda = Lambda<Action>( expr );

// Compile: Inject and ConfigurationValue nodes are resolved from the container
var fn = lambda.Compile( serviceProvider );
fn();
```

---

## IExpressionCompiler

`IExpressionCompiler` is a DI-friendly interface that abstracts the compilation act. Register an
implementation in the container to make the compiler injectable.

```csharp
public interface IExpressionCompiler
{
    Delegate Compile( LambdaExpression lambda );
    TDelegate Compile<TDelegate>( Expression<TDelegate> lambda ) where TDelegate : Delegate;
    Delegate? TryCompile( LambdaExpression lambda );
    TDelegate? TryCompile<TDelegate>( Expression<TDelegate> lambda ) where TDelegate : Delegate;
}
```

### Built-in Implementations

| Class | Package | Description |
|-------|---------|-------------|
| `SystemExpressionCompiler` | `Hyperbee.Expressions` | Wraps `LambdaExpression.Compile()` |
| `HyperbeeExpressionCompiler` | `Hyperbee.Expressions.Compiler` | Wraps `HyperbeeCompiler.Compile()` |

### Registering in DI

```csharp
// Use the System compiler (default)
services.AddSingleton<IExpressionCompiler>( SystemExpressionCompiler.Instance );

// Use the Hyperbee compiler
services.AddSingleton<IExpressionCompiler>( HyperbeeExpressionCompiler.Instance );
```

### Using IExpressionCompiler

```csharp
public class MyPipelineBuilder
{
    private readonly IExpressionCompiler _compiler;

    public MyPipelineBuilder( IExpressionCompiler compiler )
    {
        _compiler = compiler;
    }

    public Func<int> Build( Expression<Func<int>> lambda )
    {
        return (Func<int>) _compiler.Compile( lambda );
    }
}
```

---

## IDependencyInjectionExpression

Custom expression types that need an `IServiceProvider` implement `IDependencyInjectionExpression`:

```csharp
public interface IDependencyInjectionExpression
{
    void SetServiceProvider( IServiceProvider serviceProvider );
}
```

The `Compile(serviceProvider)` extension finds all nodes implementing this interface and calls
`SetServiceProvider` before compiling. Implement it in custom expressions to participate in the
same resolution pattern.

---

## Notes

- Service resolution happens at compile time (when `Compile(serviceProvider)` is called), not at
  runtime when the delegate is invoked. The resolved services are captured as closures.
- `SystemExpressionCompiler.Instance` and `HyperbeeExpressionCompiler.Instance` are singletons --
  safe to register as `Singleton` in the container.
- See [Inject](../expressions/inject.md) for `InjectExpression` factory methods.
- See [Configuration Value](../expressions/configuration-value.md) for `ConfigurationExpression`.
- See [Compiler](../compiler/compiler.md) for `HyperbeeExpressionCompiler` details.
