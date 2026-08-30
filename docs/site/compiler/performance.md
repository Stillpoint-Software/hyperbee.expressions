---
layout: default
title: Performance
parent: Compiler
nav_order: 4
---

# Performance

`Hyperbee.Expressions.Compiler` is benchmarked against the System expression compiler (SEC) and
[FastExpressionCompiler](https://github.com/dadhi/FastExpressionCompiler) (FEC).

Benchmarks run on `.NET 9`, `BenchmarkDotNet`, 20 iterations, 8 warmup iterations. All tiers in a
table come from one run, so the ratios are comparable even though absolute times drift between
runs.

> Two notes on method, both from mistakes worth keeping in view.
>
> The `Closure` tier once built `Add( parameter, Constant( capturedValue ) )`, which every compiler
> folds into the instruction stream. It measured a constant add, not a closure, so the closure path
> went unmeasured in both the benchmarks and the docs. The tier is now a nested lambda that
> captures the enclosing parameter.
>
> Execution was measured one call at a time, at three iterations. A body like `a + b` runs in less
> time than the harness spends reaching it, so those tiers reported a couple of nanoseconds with
> error bars several times their means, and this page read a story into them that was not there --
> see [Execution Speed](#execution-speed). Execution is now measured over a thousand calls per
> operation, which lifts the body above that floor.

---

## Compilation Speed

| Expression | System | FEC | **HEC** | vs System | vs FEC |
|------------|-------:|----:|--------:|----------:|-------:|
| Simple | 71.5 us | 5.9 us | **7.0 us** | 10.2x faster | 1.20x |
| Closure | 57.8 us | 5.6 us | **8.4 us** | 6.9x faster | 1.50x |
| TryCatch | 110.0 us | 8.2 us | **13.1 us** | 8.4x faster | 1.59x |
| Complex | 263.6 us | 7.1 us | **9.3 us** | 28.3x faster | 1.31x |
| Loop | 147.2 us | 10.2 us | **16.3 us** | 9.1x faster | 1.59x |
| Switch | 132.4 us | 7.3 us | **12.1 us** | 10.9x faster | 1.66x |

HEC compiles **7-28x faster** than the System compiler and within **1.20-1.66x** of FEC.

The spread against FEC is not fixed overhead -- `Simple` is the closest tier, not the furthest.
FEC walks the expression tree and emits IL. HEC lowers to an IR, runs three passes over it, and
then emits, which is what pays for the tighter IL, the coroutine support and the patterns FEC
declines. `Simple` compiles at near parity because that pipeline has almost nothing to do; `Switch`
and `TryCatch` are the tiers where it does the most.

---

## Memory Allocations (per Compile call)

| Expression | System | FEC | **HEC** | vs System | vs FEC |
|------------|-------:|----:|--------:|----------:|-------:|
| Simple | 4,335 B | 903 B | **2,095 B** | 52% fewer | 2.3x |
| Closure | 5,678 B | 894 B | **3,456 B** | 39% fewer | 3.9x |
| TryCatch | 5,897 B | 1,516 B | **4,085 B** | 31% fewer | 2.7x |
| Complex | 4,741 B | 1,390 B | **2,479 B** | 48% fewer | 1.8x |
| Loop | 6,718 B | 1,110 B | **4,255 B** | 37% fewer | 3.8x |
| Switch | 6,272 B | 1,352 B | **3,840 B** | 39% fewer | 2.8x |

HEC allocates **31-52% less** memory than the System compiler, and **1.8-3.9x** more than FEC.

---

## Execution Speed

Delegates compiled by HEC run at FEC's speed, and a little behind the System compiler's on small
bodies. Nothing allocates per call.

Measured over a thousand calls per operation, in nanoseconds per call:

| Expression | System | FEC | **HEC** | vs System | vs FEC |
|------------|-------:|----:|--------:|----------:|-------:|
| Simple | 2.20 ns | 2.99 ns | **3.13 ns** | 1.42x | 1.05x |
| TryCatch | 2.64 ns | 3.57 ns | **3.49 ns** | 1.32x | 0.98x |
| Switch | 4.66 ns | 5.37 ns | **5.27 ns** | 1.13x | 0.98x |

Larger bodies converge, because the body rather than the call dominates. `Complex` is 53.1 ns for
SEC, 54.3 for FEC and 54.1 for HEC. `Loop` is 83.1 for SEC and 84.8 for HEC; FEC does not support
that loop pattern.

### What this page used to say

It said the three compilers executed at the same speed, and read the per-call tiers -- all under
two nanoseconds, with error bars several times their means -- as "roughly equivalent". They were
not equivalent. They were unreadable, and the difference they hid was real: HEC was **1.75-1.92x**
the System compiler per call on small bodies.

The cause was the shape of the delegate rather than the code in it. HEC bound nothing to the
delegate unless the body needed a constants array, so most compiled delegates were open static
ones. `Delegate.Invoke` passes a target in the first slot, and a delegate over a static method with
nothing bound has no target to put there, so the runtime inserts a thunk that shifts every argument
down one on the way through. Measured on its own, same `a + b` body, no compiler involved:
**4.11 ns** through an open static delegate against **3.03 ns** through one closed over a leading
argument.

Every compiled delegate is now closed over its constants array, empty when there is nothing to read
-- the shape the System compiler uses, which is why its IL for a two-parameter lambda reads
`ldarg.1 / ldarg.2`. The emitted IL was never the problem: for `Simple` and `Switch` it is identical
in shape to SEC's, and for `Simple` two bytes shorter.

---

## Invoked lambdas

`Expression.Invoke( lambda, args )` -- a lambda invoked in place rather than passed around -- is
inlined at the call site. The body runs in the calling frame with its parameters bound as block
variables, which removes a second compilation and removes the capture: an enclosing variable the
body reads becomes an ordinary local read, so nothing is boxed and nothing is allocated per call.
The System compiler does the same.

Before inlining was added, the `Closure` tier compiled in 10.9 us / 7,831 B and executed in 8.0 ns
with 24 bytes allocated per call.

A lambda used as a *value* -- assigned to a variable or field rather than invoked in place -- still
has to be materialized per evaluation, in any compiler. Measured on the same harness that costs
~416 ns with SEC and ~463 ns with HEC: near parity, and not specific to HEC.

---

## When to Use HEC

| Scenario | Recommendation |
|----------|---------------|
| `BlockAsync` or `BlockEnumerable` | HEC -- 15-19x faster to run, half the allocation |
| Hot compilation path (many lambdas compiled at runtime) | HEC -- 7-28x faster than SEC |
| Memory-constrained environment | HEC -- 31-52% fewer allocations than SEC |
| All expression patterns including those FEC doesn't support | HEC |
| Static method IL emission (`CompileToMethod`) | HEC only |
| Fastest compilation and fewest allocations, no coroutines needed | FEC |
| Maximum compatibility, no extra dependency | SEC (`lambda.Compile()`) |

FEC compiles faster than HEC on every tier here and allocates less on every tier. Where HEC wins is
coroutines, which FEC does not support, and the patterns FEC declines.

---

## Coroutine bodies

Execution, per call.

| Expression | System | **HEC** | vs System |
|------------|-------:|--------:|----------:|
| `BlockAsync`, no captures | 1,096 ns / 232 B | **72 ns / 168 B** | 15.3x faster |
| `BlockAsync`, captures an enclosing variable | 1,092 ns / 240 B | **70 ns / 120 B** | 15.5x faster |
| `BlockEnumerable`, no captures | 1,016 ns / 120 B | **54 ns / 56 B** | 18.7x faster |
| `BlockEnumerable`, captures an enclosing variable | 1,093 ns / 200 B | **57 ns / 80 B** | 19.3x faster |

A coroutine body is compiled once and embedded as a constant delegate. A body that captures an
enclosing variable used to be a lambda-as-value that had to be materialized on every call, which
put it at parity with SEC; the captured variables are now hoisted into cells that the state machine
carries by field, so such a body is compiled once as well.

### Where MoveNext lives

`CompileToMethod` was never ported past .NET Framework, so SEC cannot emit a body into a
`MethodBuilder`. Its state machine holds MoveNext as a delegate in a field and invokes it on every
resume. HEC can emit into one, so the body becomes the machine's own method:

| MoveNext form | Execution | Cold compile |
|---------------|----------:|-------------:|
| `BlockAsync`, emitted into the type | **72 ns** | 1,663 us / 64.1 KB |
| `BlockAsync`, delegate field | 84 ns | 1,307 us / 63.2 KB |
| `BlockEnumerable`, emitted into the type | 54 ns | **768 us / 36.5 KB** |
| `BlockEnumerable`, delegate field | 48 ns | 995 us / 42.0 KB |

Allocation per call is identical either way -- the delegate is built once, not per call.

MoveNext is entered once per *suspension*, not once per await, so an async body whose awaits
complete synchronously enters it exactly once no matter how many awaits it has, and a body that
does suspend pays scheduling that dwarfs an indirection. That bounds what this can be worth, and
the two forms are close enough on the enumerable tiers to trade places between runs.

A `DynamicMethod` is created with visibility checks skipped while a `MethodBuilder` is not, so a
body reaching a non-public member keeps the delegate form. Emitting into the type is an
optimization and must never narrow what compiles.
`ExpressionRuntimeOptions.EmitMoveNextIntoType` forces it off.

### Coroutine compilation

Cold compile, one invocation per iteration -- a coroutine block caches its reduction, so a second
compile of the same instance is not a compile.

| Expression | System | **HEC** | vs System |
|------------|-------:|--------:|----------:|
| `BlockAsync`, no captures | 2,434 us / 65.7 KB | **1,663 us / 64.1 KB** | 0.68x / 0.98x |
| `BlockAsync`, captures | 2,142 us / 66.5 KB | **1,405 us / 72.6 KB** | 0.66x / 1.09x |
| `BlockEnumerable`, no captures | 1,506 us / 44.0 KB | **768 us / 36.5 KB** | 0.51x / 0.83x |
| `BlockEnumerable`, captures | 1,590 us / 45.2 KB | **853 us / 43.1 KB** | 0.54x / 0.95x |
| NestedClosure | 277 us / 7.3 KB | **63 us / 3.4 KB** | 0.23x / 0.47x |

Both compilers are dominated here by `TypeBuilder.CreateType()`, which is why these are milliseconds
against microseconds everywhere else in this document.

`BlockAsync` with a capture is the one tier where HEC allocates more than SEC to compile. That is
the closure rewriter hoisting the captured variable into a cell, and it buys the 15x execution
figure above: a few kilobytes once, against an order of magnitude on every call.

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

HEC runs three passes over the IR before emission:

| Pass | Effect |
|------|--------|
| `StackSpillPass` | Empties the evaluation stack at exception boundaries, and converts branches leaving a protected region into `leave` |
| `PeepholePass` | Constant folding, branch simplification, load/store elimination, redundant-cast removal |
| `DeadCodePass` | Removes instructions after unconditional branches and unreachable label sequences |

Invoked lambdas are inlined during lowering, before these passes run.

These passes are why HEC produces tighter IL than SEC, which interprets and re-emits the full
expression tree, while staying within striking distance of FEC, which does similar peephole work.
They are also most of why HEC compiles more slowly than FEC. Those are the same fact, not two.
