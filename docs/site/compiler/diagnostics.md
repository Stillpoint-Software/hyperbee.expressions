---
layout: default
title: Diagnostics
parent: Compiler
nav_order: 3
---

# Diagnostics

`CompilerDiagnostics` provides hooks into the HEC compilation pipeline for debugging and inspection.
Pass an instance to `HyperbeeCompiler.Compile()` to capture intermediate representations.

---

## CompilerDiagnostics

```csharp
public class CompilerDiagnostics
{
    public Action<string>? IRCapture { get; init; }
}
```

| Property | Type | Description |
|----------|------|-------------|
| `IRCapture` | `Action<string>?` | Called after IR lowering and all optimization passes with a human-readable IR listing |

---

## Usage

### Capture IR to Console

```csharp
using Hyperbee.Expressions.Compiler;
using Hyperbee.Expressions.Compiler.Diagnostics;

var diagnostics = new CompilerDiagnostics
{
    IRCapture = ir => Console.WriteLine( ir )
};

var fn = HyperbeeCompiler.Compile( lambda, diagnostics );
```

### Capture IR to a String

```csharp
string? capturedIR = null;

var diagnostics = new CompilerDiagnostics
{
    IRCapture = ir => capturedIR = ir
};

HyperbeeCompiler.Compile( lambda, diagnostics );
Console.WriteLine( capturedIR );
```

### Capture IR to a File

```csharp
var diagnostics = new CompilerDiagnostics
{
    IRCapture = ir => File.WriteAllText( "ir_output.txt", ir )
};
```

---

## IR Listing Format

The captured IR listing is a human-readable text representation of the optimized instruction stream.

```
0000  LoadArg                  0
0001  LoadConst                [0] 1
0002  Add
0003  Ret

Locals:
```

Each line has the format:

```
{index:D4}  {Op,-22}  {operand}
```

For instructions with operands, the operand is shown with context:

| Instruction | Operand Format |
|-------------|----------------|
| `LoadConst` | `[idx] value` -- method, ctor, field, type, or constant value |
| `Call` / `CallVirt` | `[idx] Type.Method()` |
| `LoadLocal` / `StoreLocal` | `[idx] name (Type)` |
| `Branch` / `Label` | `L{label:D4} -> {target:D4}` |
| Type operations | `[idx] TypeName` |

After the instructions, a `Locals:` section lists declared locals:

```
Locals:
  [0] Int32 result1
  [1] Int32 result2
```

---

## Example IR Output

For a simple addition lambda `(int x) => x + 1`:

```
0000  LoadArg                  0
0001  LoadConst                [0] 1
0002  Add
0003  Ret
```

For an if/else:

```
0000  LoadArg                  0
0001  LoadConst                [0] 0
0002  Cgt
0003  BranchFalse              L0001 -> 0007
0004  LoadArg                  0
0005  Branch                   L0002 -> 0009
0006  Label                    L0001 -> 0007
0007  LoadConst                [1] 0
0008  Label                    L0002 -> 0009
0009  Ret
```

---

## Notes

- `IRCapture` fires once per `Compile()` call, after all optimization passes but before IL emission.
- The IR shown reflects the optimized form -- `PeepholePass`, `DeadCodePass`, and `StackSpillPass`
  have already run.
- To capture the unoptimized IR for comparison, you would need to run the lowerer manually, which
  is not part of the public API.
- `CompilerDiagnostics` is not thread-safe if the same instance is shared across concurrent
  `Compile()` calls with a stateful callback (e.g., appending to a list). Use per-call instances
  or synchronize externally.
