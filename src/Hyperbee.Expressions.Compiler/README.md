# Hyperbee Expression Compiler

A high-performance, IR-based expression compiler for .NET. Drop-in replacement for `Expression.Compile()`
that is **9-34x faster and allocates up to 50% less than the System compiler** and supports **all expression tree patterns**.

## Why Another Expression Compiler?

We :heart: [FastExpressionCompiler](https://github.com/dadhi/FastExpressionCompiler). FEC is faster than Hyperbee Expression Compiler, and allocates less memory - and for many workloads it's the right choice. If FEC compiles your expressions correctly, use it.

FEC's single-pass, low allocation, IL emission approach supports most, but not **all**, expression patterns. See [FEC issues](https://github.com/dadhi/FastExpressionCompiler/issues); patterns like compound assignments inside `TryCatch`, complex closure captures, and certain value-type operations aren't supported.

Hyperbee takes a middle ground: a **multi-pass IR pipeline** that lowers expression trees to an intermediate representation, runs optimization passes, validates structural correctness, and then emits IL. This architecture trades a small amount of speed and allocation overhead for **correct IL across all expression tree patterns** while significantly outperforming the System Compiler.

## Performance

HEC is consistently **9-34x faster than the System Compiler** and within **1.16-1.54x of FEC** across all tiers - while producing correct IL for the sub-set of patterns FEC doesn't support (`NegateChecked` overflow, `NaN` comparisons, value-type instance calls, compound assignments in `TryCatch`, etc.).

The Complex tier standout (~34x vs System) is where the multi-pass IR architecture pays off against the System compiler's heavyweight compilation pipeline. The Switch tier at 1.54x is the widest gap vs FEC.

### Compilation Benchmarks

```
BenchmarkDotNet v0.15.8, Windows 11
Intel Core i9-9980HK CPU 2.40GHz, 1 CPU, 16 logical and 8 physical cores
.NET SDK 10.0.103 - .NET 9.0.12, X64 RyuJIT x86-64-v3
```

| Tier         | Compiler     |        Mean |   Allocated | vs System (speed) | vs FEC (speed) |
| ------------ | ------------ | ----------: | ----------: | ----------------: | -------------: |
| **Simple**   | System       |    30.65 us |     4,335 B |                 - |              - |
|              | FEC          |     2.96 us |       904 B |      10.3x faster |              - |
|              | **Hyperbee** | **3.50 us** | **2,176 B** |   **8.8x faster** |      **1.18x** |
| **Closure**  | System       |    28.55 us |     4,279 B |                 - |              - |
|              | FEC          |     2.79 us |       895 B |      10.2x faster |              - |
|              | **Hyperbee** | **3.24 us** | **2,160 B** |   **8.8x faster** |      **1.16x** |
| **TryCatch** | System       |    49.59 us |     5,893 B |                 - |              - |
|              | FEC          |     3.78 us |     1,518 B |      13.1x faster |              - |
|              | **Hyperbee** | **5.54 us** | **4,023 B** |   **9.0x faster** |      **1.47x** |
| **Complex**  | System       |   150.71 us |     4,741 B |                 - |              - |
|              | FEC          |     3.51 us |     1,392 B |      42.9x faster |              - |
|              | **Hyperbee** | **4.47 us** | **2,536 B** |  **33.7x faster** |      **1.27x** |
| **Loop**     | System       |    65.29 us |     6,710 B |                 - |              - |
|              | FEC          |     4.21 us |     1,110 B |      15.5x faster |              - |
|              | **Hyperbee** | **5.77 us** | **4,855 B** |  **11.3x faster** |      **1.37x** |
| **Switch**   | System       |    61.83 us |     6,264 B |                 - |              - |
|              | FEC          |     3.61 us |     1,352 B |      17.1x faster |              - |
|              | **Hyperbee** | **5.55 us** | **4,152 B** |  **11.2x faster** |      **1.54x** |

### Allocation Profile

The multi-pass IR pipeline allocates roughly **1.8–4.4× more than FEC** per compilation call but
**up to 50% less than the System Compiler**. The overhead is per-compilation, not per-execution -
compiled delegates run at equivalent speed regardless of which compiler produced them. For hot paths
that compile once and cache, the allocation difference is negligible. For workloads that re-compile
frequently (dynamic LINQ providers, interpreted rule engines), prefer FEC when its patterns cover your
use case.

### Execution Benchmarks

All three compilers produce delegates with equivalent runtime performance. For non-trivial expressions
(Complex, Loop), the difference is zero - the compiled IL is structurally identical. For trivial
expressions (Simple, Switch), sub-nanosecond differences reflect JIT inlining decisions around
`DynamicMethod` boundaries, not meaningful execution overhead.

> **Note:** FEC returns `N/A` for the Loop tier due to a known compilation issue with
> loop/break expressions. HEC compiles and runs it correctly.

| Tier         | Compiler     |     Mean | vs System |
| ------------ | ------------ | -------: | --------: |
| **Simple**   | System       | 1.098 ns |         - |
|              | FEC          | 1.363 ns |     1.24x |
|              | **Hyperbee** | 1.769 ns |     1.61x |
| **Closure**  | System       | 0.387 ns |         - |
|              | FEC          | 0.996 ns |     2.58x |
|              | **Hyperbee** | 1.520 ns |     3.93x |
| **TryCatch** | System       | 0.447 ns |         - |
|              | FEC          | 1.074 ns |     2.40x |
|              | **Hyperbee** | 1.731 ns |     3.87x |
| **Complex**  | System       | 25.42 ns |         - |
|              | FEC          | 25.22 ns |   **~1x** |
|              | **Hyperbee** | 24.81 ns |   **~1x** |
| **Loop**     | System       | 30.62 ns |         - |
|              | FEC          |      N/A |       N/A |
|              | **Hyperbee** | 31.76 ns |   **~1x** |
| **Switch**   | System       |  1.57 ns |         - |
|              | FEC          |  1.87 ns |     1.20x |
|              | **Hyperbee** |  2.23 ns |     1.42x |

The sub-nanosecond Simple/Closure/TryCatch numbers (< 2 ns absolute) are at the boundary of
`ShortRun` precision (3 iterations). The 1–4x ratios represent 1–3 extra clock cycles and should
be interpreted as "roughly equivalent" rather than a meaningful performance gap.

### Compiler Comparison

|                        | System (`Expression.Compile`)            | FEC (`CompileFast`)                                       | Hyperbee (`HyperbeeCompiler.Compile`)    |
| ---------------------- | ---------------------------------------- | --------------------------------------------------------- | ---------------------------------------- |
| **Speed**              | Baseline (slowest)                       | Fastest (10-43x vs System)                                | Fast (9-34x vs System)                   |
| **Allocations**        | Highest                                  | Lowest                                                    | Middle (up to 50% less than System)      |
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

// Direct compilation - drop-in replacement for Expression.Compile()
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

Emit the expression tree directly into a static `MethodBuilder` on a dynamic type - useful when building
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
| **IRValidator**    | Structural validation - stack depth, label references, exception blocks (DEBUG only) |

## Supported Frameworks

- .NET 8.0
- .NET 9.0
- .NET 10.0

## Credits

- [FastExpressionCompiler](https://github.com/dadhi/FastExpressionCompiler) by Maksim Volkau -
  the inspiration and benchmark target. FEC pioneered high-performance expression compilation
  and remains the fastest option available. :heart:
- [System.Linq.Expressions](https://learn.microsoft.com/en-us/dotnet/api/system.linq.expressions) -
  the reference implementation and correctness baseline.

## License

Licensed under the [MIT License](../../LICENSE).
