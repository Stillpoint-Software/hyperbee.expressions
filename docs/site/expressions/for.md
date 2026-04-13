---
layout: default
title: For
parent: Expressions
nav_order: 5
---

# For

`ForExpression` represents a `for` loop with an initializer, condition test, iteration step, and body.
It generates standard loop IL equivalent to:

```csharp
for ( initialization; test; iteration )
    body;
```

---

## Factory Methods

```csharp
using static Hyperbee.Expressions.ExpressionExtensions;
```

| Overload | Description |
|----------|-------------|
| `For( Expression init, Expression test, Expression iter, Expression body )` | Basic for loop |
| `For( Expression init, Expression test, Expression iter, Expression body, LabelTarget brk, LabelTarget cont )` | With explicit break/continue labels |
| `For( Expression init, Expression test, Expression iter, LoopBody body )` | Body receives break/continue labels |
| `For( IEnumerable<ParameterExpression> vars, Expression init, Expression test, Expression iter, Expression body )` | With scoped variables |
| `For( IEnumerable<ParameterExpression> vars, Expression init, Expression test, Expression iter, LoopBody body )` | With scoped variables and label access |

The `LoopBody` delegate provides break and continue labels to the body builder:

```csharp
public delegate Expression LoopBody( LabelTarget breakLabel, LabelTarget continueLabel );
```

---

## Usage

### Basic For Loop

```csharp
using static System.Linq.Expressions.Expression;
using static Hyperbee.Expressions.ExpressionExtensions;

var i = Variable( typeof(int), "i" );
var sum = Variable( typeof(int), "sum" );

var forExpr = Block(
    [i, sum],
    Assign( sum, Constant( 0 ) ),
    For(
        Assign( i, Constant( 0 ) ),           // init:      i = 0
        LessThan( i, Constant( 10 ) ),         // test:      i < 10
        PostIncrementAssign( i ),              // iteration: i++
        AddAssign( sum, i )                    // body:      sum += i
    ),
    sum
);

var lambda = Lambda<Func<int>>( forExpr );
var fn = lambda.Compile();
Console.WriteLine( fn() );  // 45
```

### For Loop with Break

```csharp
var i = Variable( typeof(int), "i" );

var forExpr = For(
    Assign( i, Constant( 0 ) ),
    LessThan( i, Constant( 100 ) ),
    PostIncrementAssign( i ),
    ( brk, cont ) =>
        IfThenElse(
            GreaterThanOrEqual( i, Constant( 5 ) ),
            Break( brk ),              // exit loop when i >= 5
            Empty()
        )
);
```

### For Loop with Scoped Variable

```csharp
var i = Variable( typeof(int), "i" );

// The variable 'i' is scoped to the loop -- equivalent to for (int i = 0; ...)
var forExpr = For(
    variables: [i],
    initialization: Assign( i, Constant( 0 ) ),
    test: LessThan( i, Constant( 3 ) ),
    iteration: PostIncrementAssign( i ),
    body: Call( typeof(Console).GetMethod("WriteLine", [typeof(int)]), i )
);
```

### Inside an Async Block

```csharp
var asyncBlock = BlockAsync(
    For(
        Assign( i, Constant( 0 ) ),
        LessThan( i, Constant( 3 ) ),
        PostIncrementAssign( i ),
        Await( Call( typeof(Task).GetMethod("Yield") ) )
    )
);
```

---

## Notes

- All four parameters -- `initialization`, `test`, `iteration`, `body` -- are required.
- Pass `null` for `test` to create an infinite loop (equivalent to `for(;;)`).
- The `LoopBody` delegate overloads are the idiomatic way to use `Break` and `Continue` without
  manually creating labels.
- See [ForEach](foreach.md) and [While](while.md) for other loop expressions.
