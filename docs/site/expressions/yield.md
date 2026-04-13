---
layout: default
title: Yield
parent: Expressions
nav_order: 4
---

# Yield

`YieldExpression` represents a `yield return` or `yield break` statement inside an
`EnumerableBlockExpression`. Use `YieldReturn` to produce the next element of the sequence,
or `YieldBreak` to end enumeration early.

---

## Factory Methods

```csharp
using static Hyperbee.Expressions.ExpressionExtensions;
```

```csharp
YieldExpression YieldReturn( Expression value )
YieldExpression YieldBreak()
```

| Method | Description |
|--------|-------------|
| `YieldReturn( value )` | Yields `value` to the caller and suspends until the next `MoveNext()` |
| `YieldBreak()` | Terminates the enumerable sequence |

---

## Usage

### Yield a Sequence

```csharp
using static System.Linq.Expressions.Expression;
using static Hyperbee.Expressions.ExpressionExtensions;

var enumBlock = BlockEnumerable(
    YieldReturn( Constant( 10 ) ),
    YieldReturn( Constant( 20 ) ),
    YieldReturn( Constant( 30 ) )
);

var lambda = Lambda<Func<IEnumerable<int>>>( enumBlock );
var fn = lambda.Compile();

foreach ( var v in fn() )
    Console.WriteLine( v );  // 10  20  30
```

### Yield Break

```csharp
var limit = Variable( typeof(int), "limit" );

var enumBlock = BlockEnumerable(
    [limit],
    Assign( limit, Constant( 3 ) ),
    YieldReturn( Constant( 1 ) ),
    YieldReturn( Constant( 2 ) ),
    YieldBreak(),              // caller sees only 1 and 2
    YieldReturn( Constant( 3 ) )
);
```

### Inside a Loop

```csharp
var i = Variable( typeof(int), "i" );

var enumBlock = BlockEnumerable(
    [i],
    For(
        Assign( i, Constant( 0 ) ),
        LessThan( i, Constant( 5 ) ),
        PostIncrementAssign( i ),
        YieldReturn( Multiply( i, Constant( 2 ) ) )   // yields 0, 2, 4, 6, 8
    )
);
```

---

## Type

| Expression | `Type` |
|------------|--------|
| `YieldReturn( value )` | `typeof(void)` (the value type is inferred by the enclosing block) |
| `YieldBreak()` | `typeof(void)` |

---

## Notes

- `YieldReturn` and `YieldBreak` must be used inside an `EnumerableBlockExpression`.
- The element type of the generated `IEnumerable<T>` is determined by the type of values passed to `YieldReturn`.
- All `YieldReturn` calls in a block must produce the same type.
- See [Enumerable Block](enumerable-block.md) for the enclosing block type.
