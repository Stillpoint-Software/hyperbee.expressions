---
layout: default
title: JSON
parent: Lab
nav_order: 2
---

# JSON

`Hyperbee.Expressions.Lab` provides two JSON-related expression types:

- **`JsonExpression`** -- deserializes a JSON string or stream to a typed object using `System.Text.Json`.
- **`JsonPathExpression`** -- queries a `JsonElement` or `JsonNode` using a JSONPath expression.

---

## JsonExpression

`JsonExpression` deserializes a JSON input to a target type.

### Factory Method

```csharp
using static Hyperbee.Expressions.Lab.ExpressionExtensions;
```

```csharp
JsonExpression Json( Expression inputExpression, Type targetType = null )
```

| Parameter | Type | Description |
|-----------|------|-------------|
| `inputExpression` | `Expression` | JSON string, `Stream`, `byte[]`, or `JsonElement` |
| `targetType` | `Type?` | Target deserialization type. When `null`, returns `JsonElement` |

### Usage

```csharp
using static System.Linq.Expressions.Expression;
using static Hyperbee.Expressions.Lab.ExpressionExtensions;

// Deserialize a JSON string to a typed object
var jsonString = Constant( """{"Name":"Alice","Age":30}""" );
var person = Variable( typeof(Person), "person" );

var expr = Block(
    [person],
    Assign( person, Json( jsonString, typeof(Person) ) ),
    person
);

var lambda = Lambda<Func<Person>>( expr );
var fn = lambda.Compile( serviceProvider );
var result = fn();
// result.Name == "Alice", result.Age == 30
```

```csharp
// Deserialize to JsonElement (no target type)
var element = Json( Constant( """{"key":42}""" ) );
```

---

## JsonPathExpression

`JsonPathExpression` evaluates a JSONPath query against a `JsonElement` or `JsonNode`, returning
matched nodes.

### Factory Method

```csharp
JsonPathExpression JsonPath( Expression jsonExpression, Expression path )
```

| Parameter | Type | Description |
|-----------|------|-------------|
| `jsonExpression` | `Expression` | A `JsonElement` or `JsonNode` to query |
| `path` | `Expression` (string) | JSONPath expression (e.g., `"$.store.book[*].author"`) |

### Usage

```csharp
// Query JSON for all names
var json = Constant( """{"users":[{"name":"Alice"},{"name":"Bob"}]}""" );
var element = Variable( typeof(JsonElement), "element" );
var results = Variable( typeof(IEnumerable<JsonElement>), "results" );

var expr = Block(
    [element, results],
    Assign( element, Json( json ) ),
    Assign( results, JsonPath( element, Constant( "$.users[*].name" ) ) ),
    results
);
```

### Combining Fetch, Json, and JsonPath

```csharp
var response = Variable( typeof(string), "response" );
var names = Variable( typeof(IEnumerable<JsonElement>), "names" );

var asyncBlock = BlockAsync(
    [response, names],
    Assign( response, Await( ReadText( Fetch( Constant( "https://api.example.com/users" ) ) ) ) ),
    Assign( names, JsonPath( Json( response ), Constant( "$.data[*].name" ) ) ),
    names
);
```

---

## Notes

- `JsonExpression` uses `System.Text.Json.JsonSerializer` internally.
- For custom `JsonSerializerOptions`, the options are resolved from `IServiceProvider` when
  `Compile(serviceProvider)` is called.
- `JsonPathExpression` supports RFC 9535 JSONPath syntax via the `Hyperbee.Json` library.
- See [Fetch](fetch.md) for HTTP integration.
