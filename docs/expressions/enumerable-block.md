---
layout: default
title: Enumerable Block
parent: Expressions
nav_order: 2
---

# Enumerable Block

`EnumerableBlockExpression` represents a yield-returning code block. When compiled, it automatically
generates an `IEnumerable<T>` state machine that executes `YieldExpression` nodes, producing a lazy
sequence that callers can iterate.

The element type `T` is inferred from the `YieldReturn` expressions in the block.

---

## Factory Methods

```csharp
using static Hyperbee.Expressions.ExpressionExtensions;
```

| Overload | Description |
|----------|-------------|
| `BlockEnumerable( params Expression[] expressions )` | Block with no local variables |
| `BlockEnumerable( ParameterExpression[] variables, params Expression[] expressions )` | Block with local variables |
| `BlockEnumerable( Expression[] expressions, ExpressionRuntimeOptions options )` | Block with runtime options |
| `BlockEnumerable( ParameterExpression[] variables, Expression[] expressions, ExpressionRuntimeOptions options )` | Block with variables and options |

---

## Usage

### Yield a Range

```csharp
using static System.Linq.Expressions.Expression;
using static Hyperbee.Expressions.ExpressionExtensions;

var i = Variable( typeof(int), "i" );

var enumBlock = BlockEnumerable(
    [i],
    For(
        Assign( i, Constant( 0 ) ),
        LessThan( i, Constant( 5 ) ),
        PostIncrementAssign( i ),
        YieldReturn( i )
    )
);

var lambda = Lambda<Func<IEnumerable<int>>>( enumBlock );
var fn = lambda.Compile();

foreach ( var value in fn() )
    Console.WriteLine( value );  // 0 1 2 3 4
```

### Yield with a Condition

```csharp
var i = Variable( typeof(int), "i" );

var enumBlock = BlockEnumerable(
    [i],
    For(
        Assign( i, Constant( 0 ) ),
        LessThan( i, Constant( 10 ) ),
        PostIncrementAssign( i ),
        IfThenElse(
            Equal( Modulo( i, Constant( 2 ) ), Constant( 0 ) ),
            YieldReturn( i ),      // yield even numbers
            Empty()
        )
    )
);
```

### Early Exit with YieldBreak

```csharp
var enumBlock = BlockEnumerable(
    YieldReturn( Constant( 1 ) ),
    YieldReturn( Constant( 2 ) ),
    YieldBreak(),                   // stop enumeration here
    YieldReturn( Constant( 3 ) )    // never reached
);
```

---

## Type

The `Type` property returns `IEnumerable<T>` where `T` is the type of the values produced by
`YieldReturn` expressions in the block.

---

## Notes

- `YieldReturn` and `YieldBreak` must appear directly inside an `EnumerableBlockExpression`.
- Enumeration is lazy -- the body executes only as the caller iterates.
- Variables declared in the block are hoisted to state machine fields to survive yield points.
- See [ExpressionRuntimeOptions](../configuration/runtime-options.md) for configuration options.
- See [Yield](yield.md) for `YieldReturn` and `YieldBreak`.
