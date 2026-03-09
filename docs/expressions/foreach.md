---
layout: default
title: ForEach
parent: Expressions
nav_order: 6
---

# ForEach

`ForEachExpression` represents a `foreach` loop over any `IEnumerable` or `IEnumerable<T>` collection.
It generates standard loop IL equivalent to:

```csharp
foreach ( var element in collection )
    body;
```

The expression handles enumerator acquisition, disposal of `IDisposable` enumerators, and typed
element access automatically.

---

## Factory Methods

```csharp
using static Hyperbee.Expressions.ExpressionExtensions;
```

| Overload | Description |
|----------|-------------|
| `ForEach( Expression collection, ParameterExpression element, Expression body )` | Basic foreach |
| `ForEach( Expression collection, ParameterExpression element, Expression body, LabelTarget brk, LabelTarget cont )` | With explicit break/continue labels |
| `ForEach( Expression collection, ParameterExpression element, LoopBody body )` | Body receives break/continue labels |

The `LoopBody` delegate provides break and continue labels to the body builder:

```csharp
public delegate Expression LoopBody( LabelTarget breakLabel, LabelTarget continueLabel );
```

---

## Usage

### Iterate a List

```csharp
using static System.Linq.Expressions.Expression;
using static Hyperbee.Expressions.ExpressionExtensions;

var items = Constant( new[] { 1, 2, 3, 4, 5 } );
var item = Variable( typeof(int), "item" );
var sum = Variable( typeof(int), "sum" );

var forEachExpr = Block(
    [sum],
    Assign( sum, Constant( 0 ) ),
    ForEach(
        items,
        item,
        AddAssign( sum, item )
    ),
    sum
);

var lambda = Lambda<Func<int>>( forEachExpr );
var fn = lambda.Compile();
Console.WriteLine( fn() );  // 15
```

### ForEach with Break

```csharp
var item = Variable( typeof(int), "item" );
var first = Variable( typeof(int), "first" );

var forEachExpr = Block(
    [first],
    ForEach(
        items,
        item,
        ( brk, cont ) => Block(
            Assign( first, item ),
            Break( brk )         // capture first element and exit
        )
    ),
    first
);
```

### ForEach with Continue

```csharp
var item = Variable( typeof(int), "item" );

var forEachExpr = ForEach(
    items,
    item,
    ( brk, cont ) =>
        IfThenElse(
            Equal( Modulo( item, Constant( 2 ) ), Constant( 0 ) ),
            Continue( cont ),                              // skip even numbers
            Call( typeof(Console).GetMethod("WriteLine", [typeof(int)]), item )
        )
);
```

### Inside an Async Block

```csharp
var item = Variable( typeof(string), "item" );

var asyncBlock = BlockAsync(
    ForEach(
        Constant( new[] { "a", "b", "c" } ),
        item,
        Await( Call( typeof(MyService).GetMethod("ProcessAsync"), item ) )
    )
);
```

---

## Notes

- `collection` can be any expression whose type implements `IEnumerable` or `IEnumerable<T>`.
- If the enumerator implements `IDisposable`, it is disposed in a generated `try/finally` block.
- The `element` variable is scoped to the loop body and holds the current element on each iteration.
- See [For](for.md) and [While](while.md) for other loop expressions.
