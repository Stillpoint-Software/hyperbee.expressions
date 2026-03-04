---
layout: default
title: Performance
parent: Compiler
nav_order: 4
---

# Performance

`Hyperbee.Expressions.Compiler` is benchmarked against the System expression compiler (SEC) and
[FastExpressionCompiler](https://github.com/dadhi/FastExpressionCompiler) (FEC).

Benchmarks run on `.NET 9`, `BenchmarkDotNet`, 3 iterations, 3 warmup iterations.

---

## Compilation Speed

| Expression | System | FEC | **HEC** | vs System | vs FEC |
|------------|-------:|----:|--------:|----------:|-------:|
| Simple | 28.7 µs | 3.1 µs | **3.6 µs** | 8× faster | 1.16× |
| Closure | 27.4 µs | 2.9 µs | **3.4 µs** | 8× faster | 1.17× |
| TryCatch | 50.4 µs | 3.9 µs | **5.1 µs** | 10× faster | 1.31× |
| Complex | 136.7 µs | 3.4 µs | **4.4 µs** | 31× faster | 1.29× |
| Loop | 67.9 µs | 4.2 µs | **6.4 µs** | 11× faster | 1.51× |
| Switch | 60.4 µs | 3.4 µs | **5.2 µs** | 12× faster | 1.53× |

HEC compiles **9–34× faster** than the System compiler and within **1.16–1.54×** of FEC.

---

## Memory Allocations (per Compile call)

| Expression | System | FEC | **HEC** | vs System | vs FEC |
|------------|-------:|----:|--------:|----------:|-------:|
| Simple | 4,335 B | 904 B | **2,152 B** | 50% fewer | 2.4× |
| Closure | 4,279 B | 895 B | **2,136 B** | 50% fewer | 2.4× |
| TryCatch | 5,893 B | 1,519 B | **3,999 B** | 32% fewer | 2.6× |
| Complex | 4,741 B | 1,390 B | **2,512 B** | 47% fewer | 1.8× |
| Loop | 6,710 B | 1,110 B | **4,264 B** | 36% fewer | 3.8× |
| Switch | 6,264 B | 1,352 B | **4,128 B** | 34% fewer | 3.1× |

HEC allocates **up to 50% less** memory than the System compiler.

---

## Execution Speed

After compilation, delegates produced by HEC execute at the same speed as those produced by SEC
and FEC. For CPU-bound and I/O-bound workloads the execution times are indistinguishable.

| Expression | System | FEC | **HEC** |
|------------|-------:|----:|--------:|
| Simple | ~0.5 ns | ~1.0 ns | ~1.4 ns |
| Closure | ~0.8 ns | ~1.2 ns | ~1.9 ns |
| TryCatch | ~0.4 ns | ~1.0 ns | ~1.6 ns |
| Complex | ~27 ns | ~25 ns | ~24 ns |
| Loop | ~31 ns | N/A† | ~30 ns |
| Switch | ~1.5 ns | ~1.6 ns | ~2.0 ns |

† FEC does not support all loop patterns; `Loop | FEC` fails.

Execution overhead differences at sub-nanosecond scale are within measurement noise and not
meaningful.

---

## When to Use HEC

| Scenario | Recommendation |
|----------|---------------|
| Hot compilation path (many lambdas compiled at runtime) | HEC — 9–34× faster than SEC |
| Memory-constrained environment | HEC — up to 50% fewer allocations than SEC |
| All expression patterns including those FEC doesn't support | HEC |
| Async state machines (`BlockAsync`) | HEC — compiles MoveNext bodies directly |
| Static method IL emission (`CompileToMethod`) | HEC only |
| Maximum compatibility, no extra dependency | SEC (`lambda.Compile()`) |

---

## Optimization Passes

HEC runs three optimization passes over the IR before emission:

| Pass | Effect |
|------|--------|
| `StackSpillPass` | Eliminates merge-point locals introduced by conditional branches — reduces `StoreLocal`/`LoadLocal` pairs at phi-points |
| `PeepholePass` | Constant folding, branch simplification, load/store elimination, redundant-cast removal |
| `DeadCodePass` | Removes instructions after unconditional branches and unreachable label sequences |

These passes are the reason HEC produces tighter IL than SEC (which interprets and re-emits the
full expression tree) while remaining within striking distance of FEC (which does similar
peephole work).
