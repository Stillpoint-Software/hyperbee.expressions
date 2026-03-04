---
layout: default
title: String Format
parent: Expressions
nav_order: 10
---

# String Format

`StringFormatExpression` represents a `string.Format` call within an expression tree. It accepts a
format string and an array of argument expressions, producing a `string` result equivalent to:

```csharp
string.Format( format, arg0, arg1, ... )
```

An optional `IFormatProvider` expression is supported for culture-sensitive formatting.

---

## Factory Methods

```csharp
using static Hyperbee.Expressions.ExpressionExtensions;
```

| Overload | Description |
|----------|-------------|
| `StringFormat( Expression format, Expression argument )` | Single argument |
| `StringFormat( Expression format, Expression[] arguments )` | Multiple arguments |
| `StringFormat( Expression formatProvider, Expression format, Expression[] arguments )` | With format provider |

---

## Usage

### Single Argument

```csharp
using static System.Linq.Expressions.Expression;
using static Hyperbee.Expressions.ExpressionExtensions;

var value = Variable( typeof(int), "value" );

var expr = Block(
    [value],
    Assign( value, Constant( 42 ) ),
    StringFormat( Constant( "The answer is {0}" ), value )
);

var lambda = Lambda<Func<string>>( expr );
Console.WriteLine( lambda.Compile()() );  // "The answer is 42"
```

### Multiple Arguments

```csharp
var name = Constant( "world" );
var count = Constant( 3 );

var formatExpr = StringFormat(
    Constant( "Hello, {0}! You have {1} messages." ),
    [name, count]
);
```

### With Format Provider

```csharp
var price = Constant( 9.99m );
var culture = Constant( System.Globalization.CultureInfo.GetCultureInfo("en-GB") );

var formatExpr = StringFormat(
    culture,
    Constant( "{0:C}" ),
    [price]
);
// produces "£9.99"
```

---

## Type

`StringFormatExpression.Type` is always `typeof(string)`.

---

## Notes

- The `format` expression must produce a `string` value.
- Arguments are boxed to `object[]` internally, matching the `string.Format` signature.
- For simple concatenation, prefer `Expression.Add` on strings for better performance.
- For interpolated string patterns, `StringFormat` is the idiomatic approach in expression trees.
