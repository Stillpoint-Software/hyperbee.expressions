---
layout: default
title: While
parent: Expressions
nav_order: 7
---

# While

`WhileExpression` represents a `while` loop that repeats its body as long as a condition is true.
It generates standard loop IL equivalent to:

```csharp
while ( test )
    body;
```

---

## Factory Methods

```csharp
using static Hyperbee.Expressions.ExpressionExtensions;
```

| Overload | Description |
|----------|-------------|
| `While( Expression test, Expression body )` | Basic while loop |
| `While( Expression test, Expression body, LabelTarget brk, LabelTarget cont )` | With explicit break/continue labels |
| `While( Expression test, LoopBody body )` | Body receives break/continue labels |

The `LoopBody` delegate provides break and continue labels to the body builder:

```csharp
public delegate Expression LoopBody( LabelTarget breakLabel, LabelTarget continueLabel );
```

---

## Usage

### Basic While Loop

```csharp
using static System.Linq.Expressions.Expression;
using static Hyperbee.Expressions.ExpressionExtensions;

var i = Variable( typeof(int), "i" );
var sum = Variable( typeof(int), "sum" );

var whileExpr = Block(
    [i, sum],
    Assign( i, Constant( 0 ) ),
    Assign( sum, Constant( 0 ) ),
    While(
        LessThan( i, Constant( 5 ) ),    // condition: i < 5
        Block(
            AddAssign( sum, i ),          // sum += i
            PostIncrementAssign( i )      // i++
        )
    ),
    sum
);

var lambda = Lambda<Func<int>>( whileExpr );
var fn = lambda.Compile();
Console.WriteLine( fn() );  // 10
```

### While with Break

```csharp
var i = Variable( typeof(int), "i" );

var whileExpr = Block(
    [i],
    Assign( i, Constant( 0 ) ),
    While(
        Constant( true ),                            // infinite loop
        ( brk, cont ) => Block(
            PostIncrementAssign( i ),
            IfThen(
                GreaterThanOrEqual( i, Constant( 5 ) ),
                Break( brk )                         // exit when i >= 5
            )
        )
    ),
    i
);
```

### While with Continue

```csharp
var i = Variable( typeof(int), "i" );

var whileExpr = Block(
    [i],
    Assign( i, Constant( 0 ) ),
    While(
        LessThan( i, Constant( 10 ) ),
        ( brk, cont ) => Block(
            PostIncrementAssign( i ),
            IfThen(
                Equal( Modulo( i, Constant( 2 ) ), Constant( 0 ) ),
                Continue( cont )                     // skip even numbers
            ),
            Call( typeof(Console).GetMethod("WriteLine", [typeof(int)]), i )
        )
    )
);
```

### Inside an Async Block

```csharp
var running = Variable( typeof(bool), "running" );

var asyncBlock = BlockAsync(
    [running],
    Assign( running, Constant( true ) ),
    While(
        running,
        Block(
            Await( Call( typeof(Task).GetMethod("Delay", [typeof(int)]), Constant( 100 ) ) ),
            Assign( running, Call( typeof(MyService).GetMethod("ShouldContinue") ) )
        )
    )
);
```

---

## Notes

- Pass `Constant( true )` as `test` to create an infinite loop -- use `Break` in the body to exit.
- The `LoopBody` delegate overloads are the idiomatic way to use `Break` and `Continue` without
  manually creating labels.
- See [For](for.md) and [ForEach](foreach.md) for other loop expressions.
