---
layout: default
title: Fetch
parent: Lab
nav_order: 1
---

# Fetch

`FetchExpression` performs an HTTP request via `IHttpClientFactory` within an expression tree.
It resolves `IHttpClientFactory` from the service provider at compile time and produces a
`Task<HttpResponseMessage>`.

---

## Factory Methods

```csharp
using static Hyperbee.Expressions.Lab.ExpressionExtensions;
```

```csharp
FetchExpression Fetch(
    Expression url,
    Expression? method = null,
    Expression? headers = null,
    Expression? content = null )

FetchExpression Fetch(
    Expression clientName,
    Expression url,
    Expression? method = null,
    Expression? headers = null,
    Expression? content = null )
```

| Parameter | Type | Description |
|-----------|------|-------------|
| `clientName` | `Expression` (string) | Named HTTP client (from `IHttpClientFactory`) |
| `url` | `Expression` (string) | Request URL |
| `method` | `Expression?` (string) | HTTP method -- `"GET"`, `"POST"`, etc. Default: `"GET"` |
| `headers` | `Expression?` (`IDictionary<string,string>`) | Additional request headers |
| `content` | `Expression?` (`HttpContent`) | Request body content |

---

## Usage

### Simple GET Request

```csharp
using static System.Linq.Expressions.Expression;
using static Hyperbee.Expressions.Lab.ExpressionExtensions;

var response = Variable( typeof(HttpResponseMessage), "response" );

var asyncBlock = BlockAsync(
    [response],
    Assign(
        response,
        Await( Fetch( Constant( "https://api.example.com/data" ) ) )
    ),
    response
);

var lambda = Lambda<Func<Task<HttpResponseMessage>>>( asyncBlock );
var fn = lambda.Compile( serviceProvider );
var result = await fn();
```

### Read Response as JSON

```csharp
using static Hyperbee.Expressions.Lab.FetchExpressionExtensions;

var fetch = Fetch( Constant( "https://api.example.com/user/1" ) );

// ReadJson<T> chains the deserialization onto the fetch
var user = Variable( typeof(User), "user" );

var asyncBlock = BlockAsync(
    [user],
    Assign( user, Await( ReadJson( fetch, typeof(User) ) ) ),
    user
);
```

### POST with Content

```csharp
var fetch = Fetch(
    url: Constant( "https://api.example.com/data" ),
    method: Constant( "POST" ),
    content: Constant( new StringContent( """{"key":"value"}""", Encoding.UTF8, "application/json" ) )
);
```

### Named HTTP Client

```csharp
// Uses the named client registered as "api-client" in AddHttpClient(...)
var fetch = Fetch(
    clientName: Constant( "api-client" ),
    url: Constant( "https://internal.api/endpoint" )
);
```

---

## FetchExpressionExtensions

`FetchExpressionExtensions` provides helper methods to chain response reading:

| Method | Returns | Description |
|--------|---------|-------------|
| `ReadJson( fetch, type )` | `Task<T>` | Deserialize response body as JSON |
| `ReadText( fetch )` | `Task<string>` | Read response body as string |
| `ReadBytes( fetch )` | `Task<byte[]>` | Read response body as bytes |
| `ReadStream( fetch )` | `Task<Stream>` | Read response body as stream |

---

## Notes

- `FetchExpression` implements `IDependencyInjectionExpression`. `IHttpClientFactory` is resolved
  from the service provider when `Compile(serviceProvider)` is called.
- If no named client is specified, the default `HttpClient` is used.
- `FetchExpression.Type` is `typeof(Task<HttpResponseMessage>)` -- wrap in `Await` inside an async block.
