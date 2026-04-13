---
layout: default
title: Configuration Value
parent: Expressions
nav_order: 12
---

# Configuration Value

`ConfigurationExpression` reads a typed value from `IConfiguration` within an expression tree.
It is the expression tree equivalent of `IConfiguration.GetValue<T>("key")`.

Like `InjectExpression`, the configuration source is injected at compile time via
`lambda.Compile(serviceProvider)`, which resolves `IConfiguration` from the container and binds it
to all `ConfigurationExpression` nodes before compilation.

---

## Factory Methods

```csharp
using static Hyperbee.Expressions.ExpressionExtensions;
```

| Overload | Description |
|----------|-------------|
| `ConfigurationValue( Type type, string key )` | Typed read -- provider supplied at compile time |
| `ConfigurationValue( Type type, IConfiguration config, string key )` | Typed read with explicit configuration |
| `ConfigurationValue<T>( string key )` | Generic read -- provider supplied at compile time |
| `ConfigurationValue<T>( IConfiguration config, string key )` | Generic read with explicit configuration |

---

## Usage

### Read a Value at Compile Time

```csharp
using static System.Linq.Expressions.Expression;
using static Hyperbee.Expressions.ExpressionExtensions;

// Build the tree (IConfiguration resolved later from DI)
var timeout = ConfigurationValue<int>( "App:TimeoutMs" );

var expr = Block(
    Call(
        typeof(Console).GetMethod("WriteLine", [typeof(int)])!,
        timeout
    )
);

var lambda = Lambda<Action>( expr );

// serviceProvider must have IConfiguration registered
var fn = lambda.Compile( serviceProvider );
fn();   // prints the value of App:TimeoutMs
```

### With an Explicit IConfiguration

```csharp
IConfiguration config = new ConfigurationBuilder()
    .AddInMemoryCollection( new Dictionary<string, string?> { ["Name"] = "Hyperbee" } )
    .Build();

var name = ConfigurationValue<string>( config, "Name" );

var lambda = Lambda<Func<string>>( name );
var fn = lambda.Compile();
Console.WriteLine( fn() );  // "Hyperbee"
```

### Read Multiple Keys

```csharp
var host = ConfigurationValue<string>( "Database:Host" );
var port = ConfigurationValue<int>( "Database:Port" );

var expr = Block(
    StringFormat( Constant( "{0}:{1}" ), [host, port] )
);
```

---

## Notes

- `ConfigurationExpression` implements `IDependencyInjectionExpression`. The `Compile(serviceProvider)`
  extension resolves `IConfiguration` from the container and sets it on every `ConfigurationExpression`
  in the tree.
- If no configuration is available and the key is missing, the value defaults to the type's default
  (`null` for reference types, `0` for numeric types).
- See [Inject](inject.md) for service resolution.
- See [Dependency Injection](../configuration/dependency-injection.md) for the full DI pattern.
