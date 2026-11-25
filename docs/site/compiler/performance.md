---
layout: default
title: Performance
parent: Compiler
nav_order: 4
---

# Performance

`Hyperbee.Expressions.Compiler` is benchmarked against the System expression compiler (SEC) and
[FastExpressionCompiler](https://github.com/dadhi/FastExpressionCompiler) (FEC).

**Run provenance.** Every table on this page comes from `BenchmarkDotNet 0.15.8` on an Intel Core
i9-9980HK, against `FastExpressionCompiler 6.0.0`, on two runtimes in one run: **.NET 10.0.15** and
**.NET 11.0.0**. Figures in the tables are the .NET 11 job unless a column says otherwise. Each
benchmark class carries its own iteration counts, so they are named per section rather than claimed
once for the page.

Absolute times drift between machines and between runs. Compare ratios within a table rather than
absolute times across tables. All tiers in a table come from one run.

> Two notes on method, both from mistakes worth keeping in view.
>
> The `Closure` tier once built `Add( parameter, Constant( capturedValue ) )`, which every compiler
> folds into the instruction stream. It measured a constant add, not a closure, so the closure path
> went unmeasured in both the benchmarks and the docs. The tier is now a nested lambda that
> captures the enclosing parameter.
>
> Execution was measured one call at a time, at three iterations. A body like `a + b` runs in less
> time than the harness spends reaching it, so those tiers reported a couple of nanoseconds with
> error bars several times their means, and this page drew a conclusion they could not support --
> see [Execution Speed](#execution-speed). Execution is now measured over a thousand calls per
> operation, which lifts the body above that floor.

---

## Compilation Speed

20 iterations, 8 warmup. Per `Compile` call.

| Expression | System | FEC | **HEC** | vs System | vs FEC |
|------------|-------:|----:|--------:|----------:|-------:|
| Simple | 46.0 us | 2.57 us | **2.97 us** | 15.5x faster | 1.15x |
| Closure | 46.0 us | 2.79 us | **3.37 us** | 13.7x faster | 1.21x |
| TryCatch | 49.2 us | 3.53 us | **5.28 us** | 9.3x faster | 1.49x |
| Complex | 192.2 us | 3.12 us | **4.00 us** | 48.1x faster | 1.28x |
| Loop | 93.7 us | 4.02 us | **6.14 us** | 15.3x faster | 1.53x |
| Switch | 81.1 us | 3.17 us | **4.74 us** | 17.1x faster | 1.50x |

HEC compiles **9.3-48x faster** than the System compiler and within **1.15-1.53x** of FEC.

**FEC compiles faster than HEC on every tier.** There is no tier where HEC wins. An earlier version
of this page named `Simple` as HEC's one compile-speed win over FEC, measured on FEC 5.4.1, where a
regression in that release had put FEC at 8.1 us on that tier. FEC 6.0.0 compiles `Simple` in
2.77 us. That win is gone, and it was never ours.

The spread against FEC is not fixed overhead: `Simple` is still the closest tier at 1.15x and
`Loop` the furthest at 1.53x, so the gap tracks how much work the pipeline does rather than a
constant startup cost. FEC walks the expression tree and emits IL. HEC lowers to an IR, runs three
passes over it, and then emits, which is what pays for the tighter IL, the coroutine support and
the patterns FEC declines.

The .NET 10 job shows the same ordering at the same ratios. Both compilers got faster on .NET 11 by
roughly the same proportion, so nothing in this table is a runtime story.

---

## Memory Allocations (per Compile call)

| Expression | System | FEC | **HEC** | vs System | vs FEC |
|------------|-------:|----:|--------:|----------:|-------:|
| Simple | 4,335 B | 911 B | **1,871 B** | 57% fewer | 2.05x |
| Closure | 5,622 B | 904 B | **2,879 B** | 49% fewer | 3.18x |
| TryCatch | 5,853 B | 1,526 B | **4,175 B** | 29% fewer | 2.74x |
| Complex | 4,749 B | 1,398 B | **2,407 B** | 49% fewer | 1.72x |
| Loop | 6,678 B | 1,118 B | **4,183 B** | 37% fewer | 3.74x |
| Switch | 6,264 B | 1,311 B | **3,856 B** | 38% fewer | 2.94x |

HEC allocates **29-57% less** memory than the System compiler, and **1.72-3.74x** more than FEC.

---

## Execution Speed

Delegates compiled by HEC, SEC and FEC execute at equivalent speed. Nothing allocates per call.
20 iterations, 8 warmup, a thousand calls per operation.

| Expression | System | FEC | **HEC** | HEC - SEC | HEC vs FEC |
|------------|-------:|----:|--------:|----------:|-----------:|
| Simple | 1.29 ns | 1.72 ns | **1.70 ns** | +0.41 ns | 0.99x |
| TryCatch | 1.22 ns | 1.65 ns | **1.65 ns** | +0.43 ns | 1.00x |
| Switch | 2.15 ns | 2.56 ns | **2.52 ns** | +0.37 ns | 0.98x |

The difference against SEC is a fixed cost of about **0.4 nanoseconds per call**, and it does not
grow with the body, so it is a property of reaching the delegate rather than of the code inside it.
**HEC and FEC are indistinguishable** at 0.98-1.00x: whatever this cost is, both third-party
compilers pay it and the System compiler does not.

The `HEC - SEC` column is the figure to read. A ratio taken here is sensitive to how much harness
overhead sits in the denominator. The loop and the delegate dispatch are in all three numbers
equally, which compresses a ratio on the slow tiers and inflates it on the fast ones. The
difference itself does not move.

Only three tiers are measured here. The `Complex` and `Loop` tiers an earlier version of this page
listed came from `ExecutionBenchmarks`, which measures one call per operation at ShortRun settings
and returns confidence intervals wider than its means. Those numbers could not support a conclusion
in either direction, so they are not reproduced.

### This was not always true

The page said the same thing before, but the thing it described was twice as bad. It read the
per-call tiers, all under two nanoseconds with error bars several times their means, as "roughly
equivalent". What those tiers were hiding was a **two nanosecond** gap, not a fractional one: HEC
was **1.75-1.92x** the System compiler per call on small bodies.

The conclusion was close to right for the wrong reason, drawn from measurements that could not
support it either way. It is kept here because the difference between a claim that holds and a
claim that happens to land is the measurement behind it.

The cause was the shape of the delegate rather than the code in it. HEC bound nothing to the
delegate unless the body needed a constants array, so most compiled delegates were open static
ones. `Delegate.Invoke` passes a target in the first slot, and a delegate over a static method with
nothing bound has no target to put there, so the runtime inserts a thunk that shifts every argument
down one on the way through.

**That thunk is still there on .NET 11.** Measured on its own, same `a + b` body, no compiler
involved, a thousand calls per operation:

| Delegate shape | .NET 10 | .NET 11 |
|----------------|--------:|--------:|
| Open static delegate | 2.468 ns | 2.510 ns |
| Closed over a leading argument | **2.075 ns** | **2.002 ns** |

So the fix described below is still load-bearing. Every compiled delegate is now closed over its
constants array, empty when there is nothing to read, which is the shape the System compiler uses
and why its IL for a two-parameter lambda reads `ldarg.1 / ldarg.2`. The emitted IL was never the
problem: for `Simple` and `Switch` it is identical in shape to SEC's, and for `Simple` two bytes
shorter.

---

## Invoked lambdas

`Expression.Invoke( lambda, args )` -- a lambda invoked in place rather than passed around -- is
inlined at the call site. The body runs in the calling frame with its parameters bound as block
variables, which removes a second compilation and removes the capture: an enclosing variable the
body reads becomes an ordinary local read, so nothing is boxed and nothing is allocated per call.
The System compiler does the same.

A lambda used as a *value*, assigned to a variable or field rather than invoked in place, still has
to be materialized per evaluation, in any compiler. That is near parity with SEC on the same
harness, and not specific to HEC.

---

## When to Use HEC

| Scenario | Recommendation |
|----------|---------------|
| `BlockAsync` or `BlockEnumerable` | HEC -- 16-24x faster to run, half the allocation |
| Hot compilation path (many lambdas compiled at runtime) | HEC -- 9.3-48x faster than SEC |
| Memory-constrained environment | HEC -- 29-57% fewer allocations than SEC |
| All expression patterns including those FEC doesn't support | HEC |
| Static method IL emission (`CompileToMethod`) | HEC only |
| Fastest compilation and fewest allocations, no coroutines needed | FEC |
| Maximum compatibility, no extra dependency | SEC (`lambda.Compile()`) |

FEC compiles faster than HEC on every tier here and allocates less on every tier. Where HEC wins is
coroutines, which FEC does not support, and the patterns FEC declines or miscompiles.

---

## Coroutine bodies

Execution, per call. 20 iterations, 8 warmup.

| Expression | System | **HEC** | vs System |
|------------|-------:|--------:|----------:|
| `BlockAsync`, no captures | 561.6 ns / 224 B | **31.0 ns / 168 B** | 18.1x faster |
| `BlockAsync`, captures an enclosing variable | 510.5 ns / 232 B | **31.0 ns / 120 B** | 16.4x faster |
| `BlockEnumerable`, no captures | 491.2 ns / 112 B | **20.8 ns / 56 B** | 23.6x faster |
| `BlockEnumerable`, captures an enclosing variable | 552.7 ns / 192 B | **27.8 ns / 80 B** | 19.9x faster |

**16-24x faster** than the System compiler on .NET 11, and 15-20x on .NET 10.

A coroutine body is compiled once and embedded as a constant delegate. A body that captures an
enclosing variable used to be a lambda-as-value that had to be materialized on every call, which
put it at parity with SEC; the captured variables are now hoisted into cells that the state machine
carries by field, so such a body is compiled once as well.

A body that actually suspends is dominated by scheduling, not by the state machine.
`AsyncSuspending x16` runs in **5,057 ns** on .NET 11 against 8,936 ns on .NET 10, a **43%**
improvement that is the runtime's async machinery and none of it ours.

### Where MoveNext lives

`CompileToMethod` was never ported past .NET Framework, so SEC cannot emit a body into a
`MethodBuilder`. Its state machine holds MoveNext as a delegate in a field and invokes it on every
resume. HEC can emit into one, so the body becomes the machine's own method:

| MoveNext form | Execution | Cold compile |
|---------------|----------:|-------------:|
| `BlockAsync`, emitted into the type | **31.0 ns** | 661.7 us / 64.5 KB |
| `BlockAsync`, delegate field | 38.1 ns | 683.3 us / 62.1 KB |
| `BlockEnumerable`, emitted into the type | **20.8 ns** | **402.0 us / 36.8 KB** |
| `BlockEnumerable`, delegate field | 28.4 ns | 620.9 us / 42.3 KB |

Allocation per call is identical either way: the delegate is built once, not per call.

**Emitting into the type is faster on every tier**, by 19% and 27% on .NET 11 and by 11% and 17% on
.NET 10. An earlier version of this page said the two forms were "close enough on the enumerable
tiers to trade places between runs", measured at 54 ns emitted against 48 ns as a delegate field.
That is not what either runtime now shows, in either direction: the enumerable tier is where the
emitted form wins by the widest margin.

MoveNext is entered once per *suspension*, not once per await, so an async body whose awaits
complete synchronously enters it exactly once no matter how many awaits it has, and a body that
does suspend pays scheduling that dwarfs an indirection. That still bounds what this is worth on a
suspending body: `AsyncSuspending x16` is 5,057 ns emitted against 5,357 ns as a delegate field, a
6% difference against 19% on the synchronous async tier.

A `DynamicMethod` is created with visibility checks skipped while a `MethodBuilder` is not, so a
body reaching a non-public member keeps the delegate form. Emitting into the type is an
optimization and must never narrow what compiles.
`ExpressionRuntimeOptions.EmitMoveNextIntoType` forces it off.

### Coroutine compilation

Cold compile, one invocation per iteration, 25 iterations, 5 warmup. A coroutine block caches its
reduction, so a second compile of the same instance is not a compile.

| Expression | System | **HEC** | vs System |
|------------|-------:|--------:|----------:|
| `BlockAsync`, no captures | 1,010 us / 67.1 KB | **662 us / 64.5 KB** | 0.65x / 0.96x |
| `BlockAsync`, captures | 1,011 us / 66.5 KB | **719 us / 71.8 KB** | 0.71x / 1.08x |
| `BlockEnumerable`, no captures | 883 us / 44.2 KB | **402 us / 36.8 KB** | 0.46x / 0.83x |
| `BlockEnumerable`, captures | 933 us / 45.4 KB | **387 us / 43.4 KB** | 0.42x / 0.96x |
| NestedClosure | 99.7 us / 7.3 KB | **31.1 us / 3.4 KB** | 0.31x / 0.47x |

Both compilers are dominated here by `TypeBuilder.CreateType()`, which is why these are hundreds of
microseconds against single digits everywhere else in this document.

`BlockAsync` with a capture is the one tier where HEC allocates more than SEC to compile. That is
the closure rewriter hoisting the captured variable into a cell, and it buys the 16x execution
figure above: a few kilobytes once, against an order of magnitude on every call.

**Two no-capture tiers compile more slowly on .NET 11 than on .NET 10**: `BlockEnumerable`
no-capture at 402.0 us against 314.8 us (+28%) and `BlockAsync` no-capture at 661.7 us against
595.3 us (+11%). Both gaps are outside the error bars of both jobs. The two capture tiers moved the
other way. This is `TypeBuilder` work on a new runtime rather than a change in HEC, and it is
recorded here rather than explained.

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
| `PeepholePass` | Nine local rewrites: dead load elimination, store-then-reload to `dup`, `box`/`unbox.any` identity roundtrip, `dup`/`pop` cancellation, branch-to-fallthrough removal, compare-plus-branch fusion |
| `DeadCodePass` | Removes instructions after unconditional branches and unreachable label sequences |

Invoked lambdas are inlined during lowering, before these passes run.

**`PeepholePass` does not do constant folding, and it does not remove redundant casts.** Its nine
patterns are all local rewrites over adjacent instruction pairs and triples. The closest thing to a
cast rewrite is cancelling a `box T` immediately followed by `unbox.any T` on the same operand,
which is an identity roundtrip rather than a cast analysis.

These passes are why HEC produces tighter IL than SEC, which interprets and re-emits the full
expression tree, while staying within striking distance of FEC, which does similar peephole work.

### A note on the validator

`IRValidator` checks stack depth and branch targets over the IR, and it is
`[Conditional( "DEBUG" )]`. **Release builds run with no validator.** That matters because
[dotnet/runtime#135663](https://github.com/dotnet/runtime/issues/135663) is a .NET 10 regression in
which a `DynamicMethod` containing an unreachable block that branches to a join point with a
mismatched stack depth drives unbounded allocation inside the JIT, where .NET 8 and 9 threw
`VerificationException`. `DeadCodePass` is the only defence in a Release build, and it stops at the
first block boundary, so an unreachable-but-retained block is reachable state for that bug.
