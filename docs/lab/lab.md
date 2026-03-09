---
layout: default
title: Lab
has_children: true
nav_order: 5
---

# Lab

`Hyperbee.Expressions.Lab` provides experimental expression types that extend the core library with
higher-level constructs for HTTP, JSON, and collection operations.

> **Note:** Lab expressions are experimental. APIs may change between releases.

---

## Installation

```
dotnet add package Hyperbee.Expressions.Lab
```

---

## Expression Types

| Expression | Factory | Description |
|------------|---------|-------------|
| [Fetch](fetch.md) | `Fetch(...)` | HTTP request via `HttpClient` |
| [JSON](json.md) | `Json(...)` / `JsonPath(...)` | JSON deserialization and path queries |
| [Map / Reduce](map-reduce.md) | `Map(...)` / `Reduce(...)` | Collection projection and aggregation |

---

## Factory Methods

All factory methods are in `ExpressionExtensions` in the `Hyperbee.Expressions.Lab` namespace:

```csharp
using static Hyperbee.Expressions.Lab.ExpressionExtensions;
```
