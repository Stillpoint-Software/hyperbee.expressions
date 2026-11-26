# Hyperbee Expression Compiler

A high-performance, IR-based expression compiler for .NET. Drop-in replacement for `Expression.Compile()`
that is **9.3-48x faster and allocates 29-57% less than the System compiler** and supports **all expression tree patterns**.

## Why Another Expression Compiler?

We :heart: [FastExpressionCompiler](https://github.com/dadhi/FastExpressionCompiler). FEC is faster than Hyperbee Expression Compiler, and allocates less memory - and for many workloads it's the right choice. If FEC compiles your expressions correctly, use it.

FEC's single-pass, low allocation, IL emission approach supports most, but not **all**, expression patterns. See [FEC issues](https://github.com/dadhi/FastExpressionCompiler/issues); patterns like compound assignments inside `TryCatch`, complex closure captures, and certain value-type operations aren't currently supported.

Hyperbee takes a middle ground: a **multi-pass IR pipeline** that lowers expression trees to an intermediate representation, runs optimization passes, validates structural correctness, and then emits IL. This architecture trades a small amount of speed and allocation overhead for **correct IL across all expression tree patterns** while significantly outperforming the System Compiler.

## Performance

HEC is consistently **9.3-48x faster than the System Compiler** and within **1.15-1.53x of FEC** across all tiers - while producing correct IL for the sub-set of patterns FEC does not support or miscompiles.

For `BlockAsync` and `BlockEnumerable`, which FEC does not support, HEC compiles about **twice as fast as the System compiler** and the compiled coroutines run **16-24x faster**.

A lambda invoked in place (`Expression.Invoke( lambda, args )`) is inlined at the call site, so it needs no second compilation and captures nothing. See [Invoked lambdas](../../docs/site/compiler/performance.md#invoked-lambdas).

The Complex tier standout (48x vs System) is where the multi-pass IR architecture pays off against the System compiler's heavyweight compilation pipeline. The Loop tier at 1.53x is the widest gap vs FEC, and Simple at 1.15x the narrowest. FEC 6.0.0 compiles faster than HEC on every tier; an earlier version of this file named Simple as HEC's one win, which was a regression in FEC 5.4.1 rather than a result of ours.

### Compilation Benchmarks

```
BenchmarkDotNet v0.15.8, Windows 11
Intel Core i9-9980HK CPU 2.40GHz, 1 CPU, 16 logical and 8 physical cores
.NET SDK 11.0.100 - .NET 11.0.0, X64 RyuJIT x86-64-v3
FastExpressionCompiler 6.0.0
```

| Tier         | Compiler     |         Mean |   Allocated | vs System (speed) | vs FEC (speed) |
| ------------ | ------------ | -----------: | ----------: | ----------------: | -------------: |
| **Simple**   | System       |     45.99 us |     4,335 B |                 - |              - |
|              | FEC          |      2.57 us |       911 B |      17.9x faster |              - |
|              | **Hyperbee** |  **2.97 us** | **1,871 B** |  **15.5x faster** |      **1.15x** |
| **Closure**  | System       |     45.97 us |     5,622 B |                 - |              - |
|              | FEC          |      2.79 us |       904 B |      16.5x faster |              - |
|              | **Hyperbee** |  **3.37 us** | **2,879 B** |  **13.7x faster** |      **1.21x** |
| **TryCatch** | System       |     49.20 us |     5,853 B |                 - |              - |
|              | FEC          |      3.53 us |     1,526 B |      13.9x faster |              - |
|              | **Hyperbee** |  **5.28 us** | **4,175 B** |   **9.3x faster** |      **1.49x** |
| **Complex**  | System       |    192.19 us |     4,749 B |                 - |              - |
|              | FEC          |      3.12 us |     1,398 B |      61.6x faster |              - |
|              | **Hyperbee** |  **4.00 us** | **2,407 B** |  **48.1x faster** |      **1.28x** |
| **Loop**     | System       |     93.69 us |     6,678 B |                 - |              - |
|              | FEC          |      4.02 us |     1,118 B |      23.3x faster |              - |
|              | **Hyperbee** |  **6.14 us** | **4,183 B** |  **15.3x faster** |      **1.53x** |
| **Switch**   | System       |     81.11 us |     6,264 B |                 - |              - |
|              | FEC          |      3.17 us |     1,311 B |      25.6x faster |              - |
|              | **Hyperbee** |  **4.74 us** | **3,856 B** |  **17.1x faster** |      **1.50x** |

20 iterations, 8 warmup, one run, .NET 11.0.0. The .NET 10.0.15 job in the same run shows the same
ordering at the same ratios.

### Coroutines

FEC does not support `BlockAsync` or `BlockEnumerable`. Against the System compiler:

| Tier                        | Compile            | Execute          |
| --------------------------- | ------------------ | ---------------- |
| `BlockAsync`                | **0.65x** (faster) | **18.1x faster** |
| `BlockAsync`, captures      | **0.71x**          | **16.4x faster** |
| `BlockEnumerable`           | **0.46x**          | **23.6x faster** |
| `BlockEnumerable`, captures | **0.42x**          | **19.9x faster** |

### Allocation Profile

The multi-pass IR pipeline allocates roughly **1.7–3.7× more than FEC** per compilation call but
**29–57% less than the System Compiler**. The overhead is per-compilation, not per-execution: no
compiler allocates per call.

Compiled delegates execute at equivalent speed whichever compiler produced them. HEC costs about
**0.4 nanoseconds** more per call than the System compiler, flat across body sizes -- a property of
reaching the delegate, not of the code inside it -- and is indistinguishable from FEC at
0.98-1.00x. See [Execution Speed](../../docs/site/compiler/performance.md#execution-speed).

For hot paths that compile once and cache, the allocation difference is negligible. For workloads
that re-compile frequently (dynamic LINQ providers, interpreted rule engines), prefer FEC when its
patterns cover your use case -- unless you need coroutines, which FEC does not compile.

### Execution Benchmarks

All three compilers produce delegates with equivalent runtime performance and no per-call
allocation. HEC costs about **0.4 nanoseconds** more per call than the System compiler, flat across
body sizes -- a property of reaching the delegate rather than of the code inside it -- and is
indistinguishable from FEC at 0.98-1.00x.

Measured over a thousand calls per operation, in nanoseconds per call. Measuring one call at a time
does not work here: a body like `a + b` runs in less time than the harness spends reaching it.

| Tier         | Compiler     |     Mean | vs System |
| ------------ | ------------ | -------: | --------: |
| **Simple**   | System       |  1.29 ns |         - |
|              | FEC          |  1.72 ns |  +0.42 ns |
|              | **Hyperbee** |  1.70 ns |  +0.41 ns |
| **TryCatch** | System       |  1.22 ns |         - |
|              | FEC          |  1.65 ns |  +0.44 ns |
|              | **Hyperbee** |  1.65 ns |  +0.43 ns |
| **Switch**   | System       |  2.15 ns |         - |
|              | FEC          |  2.56 ns |  +0.41 ns |
|              | **Hyperbee** |  2.52 ns |  +0.37 ns |

The difference is the column to read rather than a ratio. A ratio taken here is sensitive to how
much loop and dispatch overhead sits in the denominator -- it is in all three numbers equally --
which inflates it on exactly the tiers where the absolute difference is smallest.

This section previously attributed the difference to JIT inlining decisions around `DynamicMethod`
boundaries. It was not that. Delegates over a static method with nothing bound have no target for
the slot `Delegate.Invoke` passes one in, so the runtime inserts a thunk that shifts every argument
down one on the way through. Every compiled delegate is now closed over its constants array, empty
when there is nothing to read, which is the shape the System compiler uses. See
[Execution Speed](../../docs/site/compiler/performance.md#execution-speed).

### Compiler Comparison

|                        | System (`Expression.Compile`)            | FEC (`CompileFast`)                                       | Hyperbee (`HyperbeeCompiler.Compile`)    |
| ---------------------- | ---------------------------------------- | --------------------------------------------------------- | ---------------------------------------- |
| **Speed**              | Baseline (slowest)                       | Fastest (14-62x vs System)                                | Fast (9.3-48x vs System)                 |
| **Allocations**        | Highest                                  | Lowest                                                    | Middle (29-57% less than System)         |
| **Correctness**        | Reference (always correct)               | Most patterns correct; some edge cases produce invalid IL | All patterns correct                     |
| **Architecture**       | Heavyweight runtime compilation pipeline | Single-pass IL emission                                   | Multi-pass IR pipeline with optimization |
| **Coroutines**         | `BlockAsync` / `BlockEnumerable`         | Not supported                                             | 16-24x faster to run than System         |
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

## Target frameworks

`net11.0`, `net10.0`, `netstandard2.1`.

`net8.0` and `net9.0` both reached end of support on 2026-11-10 and are no longer
built directly. `netstandard2.1` takes their place, so a `net8.0` or `net9.0` project
resolves that asset and keeps working.

The `netstandard2.1` asset is functionally complete. Every `NETSTANDARD` conditional
in the repository lives in `src/Shared`, and all of it supplies types newer targets
have in box: the compiler-required attributes, an empty-collection helper and two
argument-check helpers. No compiler API is withheld on that target. The asset also
pulls `Microsoft.Bcl.AsyncInterfaces` 8.0.0 for `IAsyncEnumerable`.

## Credits

- [FastExpressionCompiler](https://github.com/dadhi/FastExpressionCompiler) by Maksim Volkau -
  the inspiration and benchmark target. FEC pioneered high-performance expression compilation
  and remains the fastest option available. :heart:
- [System.Linq.Expressions](https://learn.microsoft.com/en-us/dotnet/api/system.linq.expressions) -
  the reference implementation and correctness baseline.

## License

Licensed under the [MIT License](../../LICENSE).
