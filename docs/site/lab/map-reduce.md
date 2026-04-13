---
layout: default
title: Map / Reduce
parent: Lab
nav_order: 3
---

# Map / Reduce

`Hyperbee.Expressions.Lab` provides `MapExpression` and `ReduceExpression` for functional collection
operations within expression trees -- the expression-tree equivalents of LINQ `Select` and
`Aggregate`.

---

## MapExpression

`MapExpression` projects each element of a collection through a body expression, producing a
`List<TResult>`.

### Factory Methods

```csharp
using static Hyperbee.Expressions.Lab.ExpressionExtensions;
```

| Overload | Description |
|----------|-------------|
| `Map( collection, resultType, MapBody body )` | Body receives: `item` |
| `Map( collection, MapBody body )` | Result type inferred from body |
| `Map( collection, resultType, MapBodyIndex body )` | Body receives: `item`, `index` |
| `Map( collection, MapBodyIndex body )` | Result type inferred; body receives: `item`, `index` |
| `Map( collection, resultType, MapBodyIndexSource body )` | Body receives: `item`, `index`, `source` |
| `Map( collection, MapBodyIndexSource body )` | Result type inferred; body receives all three |

**Delegate types:**

```csharp
public delegate Expression MapBody( ParameterExpression item );
public delegate Expression MapBodyIndex( ParameterExpression item, ParameterExpression index );
public delegate Expression MapBodyIndexSource( ParameterExpression item, ParameterExpression index, Expression source );
```

### Usage

```csharp
using static System.Linq.Expressions.Expression;
using static Hyperbee.Expressions.Lab.ExpressionExtensions;

// Project int[] to string[]
var numbers = Constant( new[] { 1, 2, 3, 4, 5 } );

var mapExpr = Map(
    numbers,
    typeof(string),
    item => Call( typeof(string).GetMethod("Concat", [typeof(string), typeof(string)])!,
                  Constant("item="),
                  Call( item, typeof(object).GetMethod("ToString")! ) )
);

var lambda = Lambda<Func<List<string>>>( mapExpr );
var fn = lambda.Compile();
fn();  // ["item=1", "item=2", "item=3", "item=4", "item=5"]
```

### Map with Index

```csharp
var mapExpr = Map(
    Constant( new[] { "a", "b", "c" } ),
    typeof(string),
    ( item, index ) =>
        StringFormat( Constant( "[{0}]={1}" ), [index, item] )
);
// ["[0]=a", "[1]=b", "[2]=c"]
```

---

## ReduceExpression

`ReduceExpression` aggregates a collection to a single value, passing an accumulator and each
element through a body expression -- equivalent to `Enumerable.Aggregate`.

### Factory Methods

| Overload | Description |
|----------|-------------|
| `Reduce( collection, seed, ReduceBody body )` | Body receives: `accumulator`, `item` |
| `Reduce( collection, seed, ReduceBodyIndex body )` | Body receives: `accumulator`, `item`, `index` |
| `Reduce( collection, seed, ReduceBodyIndexSource body )` | Body receives: `accumulator`, `item`, `index`, `source` |

**Delegate types:**

```csharp
public delegate Expression ReduceBody( ParameterExpression accumulator, ParameterExpression item );
public delegate Expression ReduceBodyIndex( ParameterExpression accumulator, ParameterExpression item, ParameterExpression index );
public delegate Expression ReduceBodyIndexSource( ParameterExpression accumulator, ParameterExpression item, ParameterExpression index, Expression source );
```

### Usage

```csharp
// Sum all elements
var numbers = Constant( new[] { 1, 2, 3, 4, 5 } );

var reduceExpr = Reduce(
    numbers,
    Constant( 0 ),                           // seed
    ( acc, item ) => Add( acc, item )         // body: accumulator + item
);

var lambda = Lambda<Func<int>>( reduceExpr );
var fn = lambda.Compile();
Console.WriteLine( fn() );  // 15
```

### Reduce with Index

```csharp
// Build a string with position markers
var words = Constant( new[] { "hello", "world" } );

var reduceExpr = Reduce(
    words,
    Constant( string.Empty ),
    ( acc, item, index ) =>
        StringFormat( Constant( "{0}[{1}:{2}]" ), [acc, index, item] )
);
// "[0:hello][1:world]"
```

---

## Combining Map and Reduce

```csharp
// Sum the squares: [1,2,3,4,5] -> [1,4,9,16,25] -> 55
var numbers = Constant( new[] { 1, 2, 3, 4, 5 } );

var squares = Map( numbers, item => Multiply( item, item ) );

var sum = Reduce(
    squares,
    Constant( 0 ),
    ( acc, item ) => Add( acc, item )
);

var lambda = Lambda<Func<int>>( sum );
Console.WriteLine( lambda.Compile()() );  // 55
```

---

## Notes

- `Map` always produces a `List<TResult>`. The `resultType` parameter controls `TResult`; when
  omitted, it is inferred from the body expression's `Type`.
- `Reduce` returns the same type as the `seed` expression.
- Both expressions are eager -- the entire collection is processed when the delegate is invoked.
- For lazy evaluation over large collections, prefer `ForEach` with side effects or compose with
  LINQ after compilation.
