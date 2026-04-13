---
layout: default
title: Debug
parent: Expressions
nav_order: 9
---

# Debug

`DebugExpression` injects a debug callback into an expression tree. The delegate is called at runtime
when execution reaches the debug point, receiving the current argument values for inspection.

It is useful for tracing expression tree execution without modifying the surrounding code.

---

## Factory Methods

```csharp
using static Hyperbee.Expressions.ExpressionExtensions;
```

| Overload | Description |
|----------|-------------|
| `Debug( Delegate debugDelegate, Expression argument )` | Unconditional -- single argument |
| `Debug( Delegate debugDelegate, Expression[] arguments )` | Unconditional -- multiple arguments |
| `Debug( Delegate debugDelegate, Expression condition, Expression argument )` | Conditional -- single argument |
| `Debug( Delegate debugDelegate, Expression condition, Expression[] arguments )` | Conditional -- multiple arguments |

---

## Usage

### Unconditional Debug Point

```csharp
using static System.Linq.Expressions.Expression;
using static Hyperbee.Expressions.ExpressionExtensions;

Action<int> printer = value => Console.WriteLine( $"[debug] value={value}" );

var x = Variable( typeof(int), "x" );

var expr = Block(
    [x],
    Assign( x, Constant( 42 ) ),
    Debug( printer, x ),   // prints "[debug] value=42"
    x
);

var lambda = Lambda<Func<int>>( expr );
lambda.Compile()();
```

### Conditional Debug Point

```csharp
// Only fires when x > 10
Action<int> breakpoint = value => Console.WriteLine( $"[break] {value}" );

var expr = Block(
    [x],
    Assign( x, Constant( 15 ) ),
    Debug(
        breakpoint,
        GreaterThan( x, Constant( 10 ) ),  // condition
        x                                   // argument
    ),
    x
);
```

### Multiple Arguments

```csharp
Action<int, string> trace = (i, s) => Console.WriteLine( $"i={i} s={s}" );

var i = Variable( typeof(int), "i" );
var s = Variable( typeof(string), "s" );

var expr = Block(
    [i, s],
    Assign( i, Constant( 7 ) ),
    Assign( s, Constant( "hello" ) ),
    Debug( trace, [i, s] ),
    i
);
```

---

## Notes

- `DebugExpression` reduces to `void` -- it does not change the stack value of the surrounding block.
- The debug delegate is embedded as a constant in the expression tree and is not serializable.
- Conditional debug points evaluate the condition at runtime; the delegate is only called when `true`.
- In production builds, simply remove `Debug(...)` calls -- they have no effect on surrounding logic.
