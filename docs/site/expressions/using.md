---
layout: default
title: Using
parent: Expressions
nav_order: 8
---

# Using

`UsingExpression` represents a `using` statement that acquires an `IDisposable` resource, executes
a body, and guarantees disposal in a `try/finally` block regardless of exceptions. It is equivalent to:

```csharp
using ( var resource = disposable )
    body;
```

---

## Factory Methods

```csharp
using static Hyperbee.Expressions.ExpressionExtensions;
```

| Overload | Description |
|----------|-------------|
| `Using( ParameterExpression variable, Expression disposable, Expression body )` | Named variable, disposable expression, and body |
| `Using( Expression disposable, Expression body )` | Anonymous disposable -- no variable binding |

---

## Usage

### Dispose a Resource

```csharp
using static System.Linq.Expressions.Expression;
using static Hyperbee.Expressions.ExpressionExtensions;

// Equivalent to: using (var conn = new SqlConnection(connString)) { ... }
var conn = Variable( typeof(SqlConnection), "conn" );

var usingExpr = Using(
    conn,
    New( typeof(SqlConnection).GetConstructor([typeof(string)])!, Constant( connectionString ) ),
    Call( conn, typeof(SqlConnection).GetMethod("Open")! )
);

var lambda = Lambda<Action>( usingExpr );
var fn = lambda.Compile();
fn();   // connection is opened and then disposed
```

### Anonymous Using (No Variable)

```csharp
// When you don't need to reference the resource inside the body
var usingExpr = Using(
    New( typeof(MyResource).GetConstructor(Type.EmptyTypes)! ),  // disposable
    Call( typeof(Console).GetMethod("WriteLine", [typeof(string)])!, Constant( "working" ) )
);
```

### Nested Using

```csharp
var outer = Variable( typeof(OuterResource), "outer" );
var inner = Variable( typeof(InnerResource), "inner" );

var usingExpr = Using(
    outer,
    New( typeof(OuterResource).GetConstructor(Type.EmptyTypes)! ),
    Using(
        inner,
        Call( outer, typeof(OuterResource).GetMethod("CreateInner")! ),
        Call( inner, typeof(InnerResource).GetMethod("Execute")! )
    )
);
```

### Inside an Async Block

```csharp
var reader = Variable( typeof(StreamReader), "reader" );

var asyncBlock = BlockAsync(
    [reader],
    Using(
        reader,
        New( typeof(StreamReader).GetConstructor([typeof(string)])!, Constant( "data.txt" ) ),
        Await( Call( reader, typeof(StreamReader).GetMethod("ReadToEndAsync") ) )
    )
);
```

---

## Notes

- Disposal is guaranteed even if the body throws an exception (wrapped in `try/finally`).
- If `disposable` evaluates to `null`, no exception is thrown -- null is checked before calling `Dispose()`.
- The `variable` parameter (when provided) is bound to the value of `disposable` and is accessible
  inside `body`. It must match the type of `disposable`.
- Both `IDisposable` and `IAsyncDisposable` are supported.
