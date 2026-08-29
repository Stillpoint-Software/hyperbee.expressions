---
layout: default
title: Performance
parent: Compiler
nav_order: 4
---

# Performance

`Hyperbee.Expressions.Compiler` is benchmarked against the System expression compiler (SEC) and
[FastExpressionCompiler](https://github.com/dadhi/FastExpressionCompiler) (FEC).

Benchmarks run on `.NET 9`, `BenchmarkDotNet`, 3 iterations, 3 warmup iterations. All tiers in a
table come from one run, so the ratios are comparable even though absolute times drift between
runs.

> The `Closure` tier previously built `Add( parameter, Constant( capturedValue ) )`, which every
> compiler folds into the instruction stream. It measured a constant add, not a closure, so the
> closure path went unmeasured in both the benchmarks and the docs. The tier is now a nested lambda
> that captures the enclosing parameter.

---

## Compilation Speed

| Expression | System | FEC | **HEC** | vs System | vs FEC |
|------------|-------:|----:|--------:|----------:|-------:|
| Simple | 45.9 us | 3.7 us | **4.4 us** | 10.4x faster | 1.19x |
| Closure | 40.3 us | 3.8 us | **5.9 us** | 6.8x faster | 1.54x |
| TryCatch | 69.8 us | 4.9 us | **7.5 us** | 9.3x faster | 1.52x |
| Complex | 184.7 us | 4.6 us | **5.7 us** | 32.3x faster | 1.25x |
| Loop | 98.4 us | 6.0 us | **8.8 us** | 11.2x faster | 1.48x |
| Switch | 84.1 us | 4.8 us | **6.9 us** | 12.2x faster | 1.45x |

HEC compiles **7-32x faster** than the System compiler and within **1.19-1.54x** of FEC.

---

## Memory Allocations (per Compile call)

| Expression | System | FEC | **HEC** | vs System | vs FEC |
|------------|-------:|----:|--------:|----------:|-------:|
| Simple | 4,335 B | 904 B | **2,184 B** | 50% fewer | 2.4x |
| Closure | 5,680 B | 895 B | **3,111 B** | 45% fewer | 3.5x |
| TryCatch | 5,893 B | 1,518 B | **4,128 B** | 30% fewer | 2.7x |
| Complex | 4,741 B | 1,391 B | **2,543 B** | 46% fewer | 1.8x |
| Loop | 6,710 B | 1,111 B | **4,295 B** | 36% fewer | 3.9x |
| Switch | 6,264 B | 1,352 B | **3,880 B** | 38% fewer | 2.9x |

HEC allocates **30-50% less** memory than the System compiler.

---

## Execution Speed

Delegates produced by HEC execute at the same speed as those produced by SEC and FEC, and
allocate nothing per call.

| Expression | System | FEC | **HEC** |
|------------|-------:|----:|--------:|
| Simple | ~0.6 ns | ~1.6 ns | ~1.9 ns |
| Closure | ~0.8 ns | ~1.1 ns | ~1.6 ns |
| TryCatch | ~0.6 ns | ~1.1 ns | ~2.0 ns |
| Complex | ~32 ns | ~31 ns | ~31 ns |
| Loop | ~41 ns | N/A(*) | ~41 ns |
| Switch | ~2.0 ns | ~1.7 ns | ~3.3 ns |

(*) FEC does not support all loop patterns; `Loop | FEC` fails.

The sub-nanosecond differences on the trivial tiers are 1-3 clock cycles and should be read as
"roughly equivalent". No HEC tier allocates per call.

---

## Invoked lambdas

`Expression.Invoke( lambda, args )` -- a lambda invoked in place rather than passed around -- is
inlined at the call site. The body runs in the calling frame with its parameters bound as block
variables, which removes a second compilation and removes the capture: an enclosing variable the
body reads becomes an ordinary local read, so nothing is boxed and nothing is allocated per call.
The System compiler does the same.

Before inlining was added, the `Closure` tier compiled in 10.9 us / 7,831 B and executed in 8.0 ns
with 24 bytes allocated per call. It now compiles in 5.9 us / 3,111 B and executes in 1.6 ns with
no allocation.

A lambda used as a *value* -- assigned to a variable or field rather than invoked in place -- still
has to be materialized per evaluation, in any compiler. Measured on the same harness that costs
~416 ns with SEC and ~463 ns with HEC: near parity, and not specific to HEC.

---

## When to Use HEC

| Scenario | Recommendation |
|----------|---------------|
| Hot compilation path (many lambdas compiled at runtime) | HEC -- 7-32x faster than SEC |
| Memory-constrained environment | HEC -- 30-50% fewer allocations than SEC |
| All expression patterns including those FEC doesn't support | HEC |
| Async state machines (`BlockAsync`) | HEC -- compiles MoveNext bodies directly |
| Static method IL emission (`CompileToMethod`) | HEC only |
| Maximum compatibility, no extra dependency | SEC (`lambda.Compile()`) |

---

## Coroutine bodies

| Expression | System | **HEC** |
|------------|-------:|--------:|
| `BlockAsync`, no captures | 705 ns / 232 B | **53 ns / 168 B** |
| `BlockEnumerable`, no captures | 647 ns / 112 B | **25 ns / 48 B** |
| `BlockAsync`, captures an enclosing variable | 680 ns / 240 B | 729 ns / 264 B |
| `BlockEnumerable`, captures an enclosing variable | 688 ns / 192 B | 731 ns / 216 B |

A coroutine body that captures nothing is compiled once and embedded as a constant delegate, which
is where the 13-26x advantage comes from. A body that captures an enclosing variable is a
lambda-as-value: it has to be materialized per call, which puts it at parity with SEC. Threading
the captured box through a state-machine field would let such a body stay a constant delegate --
that optimization has not been done.

---

## Optimization Passes

HEC runs three optimization passes over the IR before emission:

| Pass | Effect |
|------|--------|
| `StackSpillPass` | Eliminates merge-point locals introduced by conditional branches -- reduces `StoreLocal`/`LoadLocal` pairs at phi-points |
| `PeepholePass` | Constant folding, branch simplification, load/store elimination, redundant-cast removal |
| `DeadCodePass` | Removes instructions after unconditional branches and unreachable label sequences |

Invoked lambdas are inlined during lowering, before these passes run.

These passes are the reason HEC produces tighter IL than SEC (which interprets and re-emits the
full expression tree) while remaining within striking distance of FEC (which does similar
peephole work).
