# Hyperbee Expression Compiler

A high-performance, IR-based expression compiler for .NET. Drop-in replacement for `Expression.Compile()`
that is **9-34x faster than the System compiler** and supports **all expression tree patterns** — including
those that [FastExpressionCompiler](https://github.com/dadhi/FastExpressionCompiler) doesn't.

## Why Another Expression Compiler?

We :heart: [FastExpressionCompiler](https://github.com/dadhi/FastExpressionCompiler). FEC is faster than Hyperbee Expressions Compiler, and allocates less memory — and for many workloads it's the right choice. If FEC compiles your expressions correctly, use it.

However, FEC's single-pass, low allocation, IL emission approach supports most, but not **all**, expression patterns. See [FEC issues](https://github.com/dadhi/FastExpressionCompiler/issues); patterns like compound assignments inside `TryCatch`, complex closure captures, and certain value-type operations aren't supported.

Hyperbee takes a different approach: a **multi-pass IR pipeline** that lowers expression trees to an intermediate representation, runs optimization passes, validates structural correctness, and then emits IL. This architecture trades a small amount of speed and allocation overhead for **correct IL across all
expression tree patterns** while significantly outperforming System Compiler.

## Performance

The Hyperbee compiler is consistently 9-34x faster than System Compiler and within 1.11-1.47x of FEC across all tiers — while producing correct IL for the sub-set of patterns that FEC doesn't support (`NegateChecked` overflow, `NaN` comparisons, value-type instance calls, etc.).

The Switch tier at 1.47x is the widest gap vs FEC, the result of enhanced switch pattern handling. The Complex tier at ~34x faster than System Compiler is the standout — that's where the multi-pass IR architecture pays off vs the System compiler's heavyweight compilation pipeline.

### Compilation Benchmarks

```
BenchmarkDotNet v0.15.8, Windows 11
Intel Core i9-9980HK CPU 2.40GHz, 1 CPU, 16 logical and 8 physical cores
.NET SDK 10.0.103 — .NET 9.0.12, X64 RyuJIT x86-64-v3
```

| Tier         | Compiler     |         Mean |   Allocated | vs System (speed) | vs FEC (speed) |
| ------------ | ------------ | -----------: | ----------: | ----------------: | -------------: |
| **Simple**   | System       |    28.44 us  |     4,335 B |                 — |              — |
|              | FEC          |     2.57 us  |       904 B |      11.1x faster |              — |
|              | **Hyperbee** |  **2.86 us** | **2,168 B** |   **9.9x faster** |      **1.11x** |
| **Closure**  | System       |    27.37 us  |     4,279 B |                 — |              — |
|              | FEC          |     2.53 us  |       895 B |      10.8x faster |              — |
|              | **Hyperbee** |  **2.84 us** | **2,152 B** |   **9.6x faster** |      **1.12x** |
| **TryCatch** | System       |    47.34 us  |     5,901 B |                 — |              — |
|              | FEC          |     3.43 us  |     1,520 B |      13.8x faster |              — |
|              | **Hyperbee** |  **4.63 us** | **4,015 B** |  **10.2x faster** |      **1.35x** |
| **Complex**  | System       |   128.95 us  |     4,749 B |                 — |              — |
|              | FEC          |     3.18 us  |     1,392 B |      40.6x faster |              — |
|              | **Hyperbee** |  **3.81 us** | **2,576 B** |  **33.8x faster** |      **1.20x** |
| **Loop**     | System       |    63.99 us  |     6,718 B |                 — |              — |
|              | FEC          |     3.94 us  |     1,110 B |      16.2x faster |              — |
|              | **Hyperbee** |  **5.61 us** | **4,840 B** |  **11.4x faster** |      **1.42x** |
| **Switch**   | System       |    60.80 us  |     6,272 B |                 — |              — |
|              | FEC          |     3.03 us  |     1,352 B |      20.1x faster |              — |
|              | **Hyperbee** |  **4.47 us** | **3,968 B** |  **13.6x faster** |      **1.47x** |

### Execution Benchmarks

All three compilers produce delegates with equivalent runtime performance. Differences are sub-nanosecond
and reflect JIT characteristics of `DynamicMethod` vs static methods, not meaningful execution overhead.

| Method              |     Mean |
| ------------------- | -------: |
| Execute \| System   | 0.706 ns |
| Execute \| FEC      | 1.295 ns |
| Execute \| Hyperbee | 1.701 ns |

### Compiler Comparison

|                        | System (`Expression.Compile`)            | FEC (`CompileFast`)                                       | Hyperbee (`HyperbeeCompiler.Compile`)    |
| ---------------------- | ---------------------------------------- | --------------------------------------------------------- | ---------------------------------------- |
| **Speed**              | Baseline (slowest)                       | Fastest (10-40x vs System)                                | Fast (9-34x vs System)                   |
| **Allocations**        | Highest                                  | Lowest                                                    | Middle                                   |
| **Correctness**        | Reference (always correct)               | Most patterns correct; some edge cases produce invalid IL | All patterns correct                     |
| **Architecture**       | Heavyweight runtime compilation pipeline | Single-pass IL emission                                   | Multi-pass IR pipeline with optimization |
| **Exception handling** | Full support                             | Supported, some edge cases                                | Full support                             |
| **Closures**           | Full support                             | Supported, some edge cases                                | Full support                             |
| **Approach**           | Mature, battle-tested                    | Speed-optimized, pragmatic                                | Correctness + speed balanced             |

**Summary**: Use FEC when its speed advantage matters and your expression patterns are in its comfort zone.
Use Hyperbee when you need correct compilation across all patterns with near-FEC performance.

## Getting Started

### Installation

```
dotnet add package Hyperbee.Expressions.Compiler
```

### Basic Usage

```csharp
using Hyperbee.Expressions.Compiler;

// Direct compilation — drop-in replacement for Expression.Compile()
var lambda = Expression.Lambda<Func<int, int, int>>(
    Expression.Add( a, b ), a, b );

var fn = HyperbeeCompiler.Compile( lambda );
var result = fn( 1, 2 ); // 3
```

### Extension Method

```csharp
using Hyperbee.Expressions.Compiler;

var fn = lambda.CompileHyperbee();
```

### Safe Compilation

```csharp
// Returns null instead of throwing on unsupported patterns
var fn = HyperbeeCompiler.TryCompile( lambda );

// Falls back to System compiler on failure
var fn = HyperbeeCompiler.CompileWithFallback( lambda );
```

### Compile to MethodBuilder

Emit the expression tree directly into a static `MethodBuilder` on a dynamic type — useful when building
assemblies with `AssemblyBuilder`/`TypeBuilder`. Only expressions with embeddable constants (no closures
over heap objects) are supported; use `TryCompileToMethod` for a non-throwing variant.

```csharp
var ab = AssemblyBuilder.DefineDynamicAssembly( new AssemblyName( "MyAssembly" ), AssemblyBuilderAccess.Run );
var mb = ab.DefineDynamicModule( "MyModule" );
var tb = mb.DefineType( "MyType", TypeAttributes.Public | TypeAttributes.Class );
var method = tb.DefineMethod( "Add", MethodAttributes.Public | MethodAttributes.Static,
    typeof( int ), [typeof( int ), typeof( int )] );

var a = Expression.Parameter( typeof( int ), "a" );
var b = Expression.Parameter( typeof( int ), "b" );
HyperbeeCompiler.CompileToMethod( Expression.Lambda( Expression.Add( a, b ), a, b ), method );

var type = tb.CreateType();
var result = (int) type.GetMethod( "Add" )!.Invoke( null, [1, 2] )!; // 3
```

## Architecture

The compiler uses a four-stage pipeline:

```
Expression Tree
      |
      v
  [1. Lower]         ExpressionLowerer: tree → flat IR instruction stream
      |
      v
  [2. Transform]     StackSpillPass → PeepholePass → DeadCodePass → IRValidator
      |
      v
  [3. Map]           Build constants array for non-embeddable values
      |
      v
  [4. Emit]          ILEmissionPass: IR → CIL via ILGenerator → DynamicMethod
      |
      v
    Delegate
```

### Optimization Passes

| Pass               | Purpose                                                                              |
| ------------------ | ------------------------------------------------------------------------------------ |
| **StackSpillPass** | Ensures stack is empty at exception handling boundaries (CLR requirement)            |
| **PeepholePass**   | Removes redundant load/store pairs, dead loads, identity box/unbox roundtrips        |
| **DeadCodePass**   | Eliminates unreachable instructions after unconditional control transfers            |
| **IRValidator**    | Structural validation — stack depth, label references, exception blocks (DEBUG only) |

## Supported Frameworks

- .NET 8.0
- .NET 9.0
- .NET 10.0

## Credits

- [FastExpressionCompiler](https://github.com/dadhi/FastExpressionCompiler) by Maksim Volkau —
  the inspiration and benchmark target. FEC pioneered high-performance expression compilation
  and remains the fastest option available. :heart:
- [System.Linq.Expressions](https://learn.microsoft.com/en-us/dotnet/api/system.linq.expressions) —
  the reference implementation and correctness baseline.

## License

Licensed under the [MIT License](../../LICENSE).
