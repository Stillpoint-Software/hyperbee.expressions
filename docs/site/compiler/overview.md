---
layout: default
title: Overview
parent: Compiler
nav_order: 1
---

# Compiler Overview

`HyperbeeCompiler` compiles `LambdaExpression` trees to `DynamicMethod` delegates through a
four-stage pipeline: **Lower -> Transform -> Map -> Emit**.

---

## Compilation Pipeline

```
LambdaExpression
       |
       v
  [ 1. Lower ]          ExpressionLowerer
  Expression tree -> flat IR instruction stream (IROp)
       |
       v
  [ 2. Transform ]      Optimization passes
  StackSpillPass        -- eliminate unnecessary locals at branch merge-points
  PeepholePass          -- constant folding, branch simplification, load/store elimination
  DeadCodePass          -- remove unreachable instructions
  IRValidator           -- structural correctness checks (debug builds)
       |
       v
  [ 3. Map ]            Constants array construction
  Collect non-embeddable constants (object refs, delegates, nested lambdas)
  into a captured array; replace operands with indices
       |
       v
  [ 4. Emit ]           ILEmissionPass
  IR -> CIL -> DynamicMethod delegate
```

---

## IR Instruction Set

The intermediate representation (`IROp`) maps closely to CIL but at a slightly higher abstraction.
Key categories:

| Category | Examples |
|----------|---------|
| Constants & locals | `LoadConst`, `LoadLocal`, `StoreLocal`, `LoadArg` |
| Fields | `LoadField`, `StoreField`, `LoadStaticField`, `LoadFieldAddress` |
| Arrays | `LoadElement`, `StoreElement`, `NewArray`, `LoadArrayLength` |
| Arithmetic | `Add`, `Sub`, `Mul`, `Div`, `Negate`, `AddChecked`, ... |
| Comparison | `Ceq`, `Clt`, `Cgt`, `CltUn`, `CgtUn` |
| Conversion | `Convert`, `ConvertChecked`, `Box`, `UnboxAny`, `CastClass`, `IsInst` |
| Calls | `Call`, `CallVirt`, `NewObj`, `Constrained` |
| Control flow | `Branch`, `BranchTrue`, `BranchFalse`, `Label`, `Leave` |
| Exceptions | `BeginTry`, `BeginCatch`, `BeginFinally`, `BeginFault`, `Throw`, `Rethrow` |
| Stack | `Dup`, `Pop`, `Ret` |

---

## AsyncBlockExpression Integration

When `HyperbeeCompiler.Compile()` processes a lambda containing `AsyncBlockExpression`, it sets
itself as the ambient `ICoroutineDelegateBuilder` via `CoroutineBuilderContext`. When the
`AsyncBlockExpression` reduces (generating its `MoveNext` lambda), the ambient builder is picked
up automatically, and HEC compiles the `MoveNext` body rather than the System compiler.

This is transparent to callers -- no special options are needed:

```csharp
// HEC compiles both the outer lambda AND the inner MoveNext state machine
var fn = HyperbeeCompiler.Compile( outerLambdaContainingBlockAsync );
```

For `AsyncBlockExpression` reductions that occur outside an explicit `HyperbeeCompiler.Compile()`
call (e.g., in outer expressions compiled by the System compiler), call `UseAsDefault()` at
application startup:

```csharp
// Register HEC as the default builder for all AsyncBlockExpression reductions
HyperbeeCompiler.UseAsDefault();
```

---

## Expression Support

HEC supports all standard `ExpressionType` values, plus all `Hyperbee.Expressions` custom types.
Patterns not supported by FastExpressionCompiler that HEC handles include:

- `RuntimeVariables` expressions (`IRuntimeVariables`)
- `Dynamic` expressions
- Certain complex `TryCatch` patterns with result values
- Nested lambda closures over struct fields

---

## Notes

- Compilation is thread-safe -- `HyperbeeCompiler` has no instance state.
- The IR is not cached -- each `Compile()` call traverses and emits from scratch.
- For heavy compilation workloads, consider compiling once and caching the delegate.
- See [API Reference](api.md) for all public methods.
- See [Performance](performance.md) for benchmark results.
