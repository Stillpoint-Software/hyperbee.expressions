---
layout: default
title: Compiler
has_children: true
nav_order: 4
---

# Compiler

`Hyperbee.Expressions.Compiler` is a high-performance, IR-based compiler for .NET expression trees.
It is a drop-in replacement for `Expression.Compile()` that emits IL directly, bypassing the System
expression interpreter.

---

## Installation

```
dotnet add package Hyperbee.Expressions.Compiler
```

## Quick Start

```csharp
using Hyperbee.Expressions.Compiler;

// Direct replacement for lambda.Compile()
var fn = HyperbeeCompiler.Compile( lambda );

// Or use the IExpressionCompiler interface for DI
services.AddSingleton<IExpressionCompiler>( HyperbeeExpressionCompiler.Instance );
```

---

## Topics

| Topic | Description |
|-------|-------------|
| [Overview](overview.md) | Architecture, compilation pipeline, optimization passes |
| [API Reference](api.md) | `HyperbeeCompiler`, `HyperbeeExpressionCompiler`, `CompileToMethod` |
| [Diagnostics](diagnostics.md) | IR capture, `CompilerDiagnostics`, `IRFormatter` |
| [Performance](performance.md) | Benchmarks vs System compiler and FastExpressionCompiler |

---

## Highlights

- **7-28x faster** compilation than the System compiler
- **1.20-1.66x** of FastExpressionCompiler (FEC) compilation time
- **31-52% fewer** allocations than the System compiler
- **15-19x faster** coroutine execution than the System compiler; FEC does not support coroutines
- Supports all expression patterns -- including those FEC does not support
- Fully compatible with `AsyncBlockExpression` state machines via ambient context
- `IExpressionCompiler` interface for DI-friendly injection
