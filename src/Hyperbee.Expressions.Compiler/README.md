# Hyperbee Expression Compiler

A high-performance, IR-based expression compiler for .NET. Drop-in replacement for `Expression.Compile()`
that is **7-28x faster and allocates 31-52% less than the System compiler** and supports **all expression tree patterns**.

## Why Another Expression Compiler?

We :heart: [FastExpressionCompiler](https://github.com/dadhi/FastExpressionCompiler). FEC is faster than Hyperbee Expression Compiler, and allocates less memory - and for many workloads it's the right choice. If FEC compiles your expressions correctly, use it.

FEC's single-pass, low allocation, IL emission approach supports most, but not **all**, expression patterns. See [FEC issues](https://github.com/dadhi/FastExpressionCompiler/issues); patterns like compound assignments inside `TryCatch`, complex closure captures, and certain value-type operations aren't supported.

Hyperbee takes a middle ground: a **multi-pass IR pipeline** that lowers expression trees to an intermediate representation, runs optimization passes, validates structural correctness, and then emits IL. This architecture trades a small amount of speed and allocation overhead for **correct IL across all expression tree patterns** while significantly outperforming the System Compiler.

## Performance

HEC is consistently **7-28x faster than the System Compiler** and within **1.20-1.66x of FEC** across all tiers - while producing correct IL for the sub-set of patterns FEC doesn't support (`NegateChecked` overflow, `NaN` comparisons, value-type instance calls, compound assignments in `TryCatch`, etc.).

For `BlockAsync` and `BlockEnumerable`, which FEC does not support at all, HEC compiles about **twice as fast as the System compiler** and the compiled coroutines run **15-19x faster**.

A lambda invoked in place (`Expression.Invoke( lambda, args )`) is inlined at the call site, so it needs no second compilation and captures nothing. See [Invoked lambdas](../../docs/site/compiler/performance.md#invoked-lambdas).

The Complex tier standout (~28x vs System) is where the multi-pass IR architecture pays off against the System compiler's heavyweight compilation pipeline. The Switch tier at 1.66x is the widest gap vs FEC, and Simple at 1.20x the narrowest - the spread is the IR pipeline doing more work on the more complex trees, not fixed overhead.

### Compilation Benchmarks

```
BenchmarkDotNet v0.15.8, Windows 11
Intel Core i9-9980HK CPU 2.40GHz, 1 CPU, 16 logical and 8 physical cores
.NET SDK 10.0.103 - .NET 9.0.12, X64 RyuJIT x86-64-v3
```

| Tier         | Compiler     |         Mean |   Allocated | vs System (speed) | vs FEC (speed) |
| ------------ | ------------ | -----------: | ----------: | ----------------: | -------------: |
| **Simple**   | System       |     71.46 us |     4,335 B |                 - |              - |
|              | FEC          |      5.86 us |       903 B |      12.2x faster |              - |
|              | **Hyperbee** |  **7.04 us** | **2,095 B** |  **10.2x faster** |      **1.20x** |
| **Closure**  | System       |     57.84 us |     5,678 B |                 - |              - |
|              | FEC          |      5.62 us |       894 B |      10.3x faster |              - |
|              | **Hyperbee** |  **8.41 us** | **3,456 B** |   **6.9x faster** |      **1.50x** |
| **TryCatch** | System       |    110.02 us |     5,897 B |                 - |              - |
|              | FEC          |      8.21 us |     1,516 B |      13.4x faster |              - |
|              | **Hyperbee** | **13.09 us** | **4,085 B** |   **8.4x faster** |      **1.59x** |
| **Complex**  | System       |    263.57 us |     4,741 B |                 - |              - |
|              | FEC          |      7.11 us |     1,390 B |      37.1x faster |              - |
|              | **Hyperbee** |  **9.31 us** | **2,479 B** |  **28.3x faster** |      **1.31x** |
| **Loop**     | System       |    147.15 us |     6,718 B |                 - |              - |
|              | FEC          |     10.21 us |     1,110 B |      14.4x faster |              - |
|              | **Hyperbee** | **16.25 us** | **4,255 B** |   **9.1x faster** |      **1.59x** |
| **Switch**   | System       |    132.44 us |     6,272 B |                 - |              - |
|              | FEC          |      7.30 us |     1,352 B |      18.1x faster |              - |
|              | **Hyperbee** | **12.12 us** | **3,840 B** |  **10.9x faster** |      **1.66x** |

20 iterations, 8 warmup, one run.

### Coroutines

FEC does not support `BlockAsync` or `BlockEnumerable`. Against the System compiler:

| Tier                          | Compile              | Execute                |
| ----------------------------- | -------------------- | ---------------------- |
| `BlockAsync`                  | **0.68x** (faster)   | **15.3x faster**       |
| `BlockAsync`, captures        | **0.66x**            | **15.5x faster**       |
| `BlockEnumerable`             | **0.51x**            | **18.7x faster**       |
| `BlockEnumerable`, captures   | **0.54x**            | **19.3x faster**       |

### Allocation Profile

The multi-pass IR pipeline allocates roughly **1.8–3.9× more than FEC** per compilation call but
**31–52% less than the System Compiler**. The overhead is per-compilation, not per-execution: no
compiler allocates per call.

Compiled delegates execute at equivalent speed whichever compiler produced them. HEC costs about a
nanosecond more per call than the System compiler, flat across body sizes -- a property of reaching
the delegate, not of the code inside it -- and sits inside FEC's own margin. See
[Execution Speed](../../docs/site/compiler/performance.md#execution-speed).

For hot paths that compile once and cache, the allocation difference is negligible. For workloads
that re-compile frequently (dynamic LINQ providers, interpreted rule engines), prefer FEC when its
patterns cover your use case -- unless you need coroutines, which FEC does not compile.

### Execution Benchmarks

All three compilers produce delegates with equivalent runtime performance and no per-call
allocation. For non-trivial expressions (Complex, Loop) the difference is zero - the compiled IL is
structurally identical. For trivial expressions (Simple, Switch), sub-nanosecond differences reflect
JIT inlining decisions around `DynamicMethod` boundaries, not meaningful execution overhead.

> **Note:** FEC returns `N/A` for the Loop tier due to a known compilation issue with
> loop/break expressions. HEC compiles and runs it correctly.

| Tier         | Compiler     |     Mean | vs System |
| ------------ | ------------ | -------: | --------: |
| **Simple**   | System       | 1.098 ns |         - |
|              | FEC          | 1.363 ns |     1.24x |
|              | **Hyperbee** | 1.769 ns |     1.61x |
| **Closure**  | System       | 0.770 ns |         - |
|              | FEC          | 1.123 ns |     1.46x |
|              | **Hyperbee** | 1.581 ns |     2.05x |
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
`ShortRun` precision (3 iterations). Those ratios represent 1–3 extra clock cycles and should be
interpreted as "roughly equivalent" rather than a meaningful performance gap.

### Compiler Comparison

|                        | System (`Expression.Compile`)            | FEC (`CompileFast`)                                       | Hyperbee (`HyperbeeCompiler.Compile`)    |
| ---------------------- | ---------------------------------------- | --------------------------------------------------------- | ---------------------------------------- |
| **Speed**              | Baseline (slowest)                       | Fastest (10-37x vs System)                                | Fast (7-28x vs System)                   |
| **Allocations**        | Highest                                  | Lowest                                                    | Middle (31-52% less than System)         |
| **Correctness**        | Reference (always correct)               | Most patterns correct; some edge cases produce invalid IL | All patterns correct                     |
| **Architecture**       | Heavyweight runtime compilation pipeline | Single-pass IL emission                                   | Multi-pass IR pipeline with optimization |
| **Coroutines**         | `BlockAsync` / `BlockEnumerable`         | Not supported                                             | 15-19x faster to run than System         |
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
