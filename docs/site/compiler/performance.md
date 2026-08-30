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
| Async state machines (`BlockAsync`) | HEC -- emits MoveNext into the machine's own type |
| Static method IL emission (`CompileToMethod`) | HEC only |
| Maximum compatibility, no extra dependency | SEC (`lambda.Compile()`) |

---

## Coroutine bodies

Execution, per call. 20 iterations, 8 warmup.

| Expression | System | **HEC** |
|------------|-------:|--------:|
| `BlockAsync`, no captures | 1,105 ns / 232 B | **81 ns / 168 B** |
| `BlockEnumerable`, no captures | 1,133 ns / 112 B | **36 ns / 48 B** |
| `BlockAsync`, captures an enclosing variable | 1,129 ns / 240 B | **78 ns / 120 B** |
| `BlockEnumerable`, captures an enclosing variable | 1,100 ns / 192 B | **53 ns / 72 B** |

A coroutine body is compiled once and embedded as a constant delegate. A body that captures an
enclosing variable used to be a lambda-as-value that had to be materialized on every call, which
put it at parity with SEC; the captured variables are now hoisted into cells that the state
machine carries by field, so such a body is compiled once as well.

### Where MoveNext lives

`CompileToMethod` was never ported past .NET Framework, so SEC cannot emit a body into a
`MethodBuilder`. Its state machine holds MoveNext as a delegate in a field and invokes it on every
resume. HEC can emit into one, so the body becomes the machine's own method:

| MoveNext form | Execution | Cold compile |
|---------------|----------:|-------------:|
| `BlockEnumerable`, emitted into the type | **36 ns** | **667 us / 36.1 KB** |
| `BlockEnumerable`, delegate field | 48 ns | 963 us / 41.5 KB |
| `BlockAsync`, emitted into the type | **81 ns** | **1,212 us / 65.6 KB** |
| `BlockAsync`, delegate field | 89 ns | 1,456 us / 63.4 KB |

Allocation per call is identical either way -- the delegate is built once, not per call. The gain
is the field and the indirection.

Two things bound this. MoveNext is entered once per *suspension*, not per await, so an async body
whose awaits complete synchronously enters it exactly once no matter how many awaits it has -- and
a body that does suspend pays scheduling costs that dwarf an indirection, which is why the async
gain is the smaller of the two. An enumerable re-enters MoveNext once per element, so it gains more.

The other bound is visibility. A `DynamicMethod` is created with visibility checks skipped while a
`MethodBuilder` is not, so a body reaching a non-public member keeps the delegate form. Emitting
into the type is an optimization and must never narrow what compiles.
`ExpressionRuntimeOptions.EmitMoveNextIntoType` forces it off.

### Coroutine compilation

Cold compile, one invocation per iteration -- a coroutine block caches its reduction, so a second
compile of the same instance is not a compile.

| Expression | System | **HEC** | vs System |
|------------|-------:|--------:|----------:|
| `BlockAsync`, no captures | 2,049 us / 67.1 KB | **1,212 us / 65.6 KB** | 0.59x / 0.98x |
| `BlockAsync`, captures | 2,120 us / 67.8 KB | **1,459 us / 74.4 KB** | 0.69x / 1.10x |
| `BlockEnumerable`, no captures | 1,301 us / 43.4 KB | **667 us / 36.1 KB** | 0.51x / 0.83x |
| `BlockEnumerable`, captures | 1,411 us / 44.6 KB | **785 us / 45.3 KB** | 0.56x / 1.02x |
| NestedClosure | 192 us / 7.3 KB | **55 us / 3.5 KB** | 0.29x / 0.48x |

Both compilers are dominated here by `TypeBuilder.CreateType()`, which is why these are milliseconds
against microseconds everywhere else in this document.

`BlockEnumerable` used to be the outlier, at 2.0x the System compiler's time and 2.1x its
allocation. Reducing a coroutine block builds a state machine type, and the pipeline reduces a node
more than once -- `BlockAsync` cached that and `BlockEnumerable` did not, so one compile emitted
three state machine types and used the last. It also lacked a `VisitChildren` override, so the base
implementation reduced the block and visited the state machine rather than the block's own
children: merely walking the tree built a type.

The generated enumerable type also derives from `EnumerableStateMachineBase<TResult>` rather than
implementing `IEnumerable<T>`, `IEnumerator<T>` and `IDisposable` itself. Two `GetEnumerator`
overloads, two `Current` accessors, `Reset` and `Dispose` were identical for every state machine
and were emitted per machine.

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
