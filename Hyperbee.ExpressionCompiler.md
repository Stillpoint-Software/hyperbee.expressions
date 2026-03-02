# Hyperbee.ExpressionCompiler

A high-performance, IR-based expression compiler for .NET that aims to match
FastExpressionCompiler's compilation speed while maintaining the correctness
and completeness of the System Expression Compiler.

---

## Table of Contents

1. [Problem Statement](#1-problem-statement)
2. [Background and Context](#2-background-and-context)
3. [Analysis of the System Expression Compiler](#3-analysis-of-the-system-expression-compiler)
4. [Analysis of FastExpressionCompiler (FEC)](#4-analysis-of-fastexpressioncompiler-fec)
5. [Root Cause Analysis: Why the System Compiler Is Slow](#5-root-cause-analysis-why-the-system-compiler-is-slow)
6. [Proposed Strategy: IR-Based Expression Compiler](#6-proposed-strategy-ir-based-expression-compiler)
7. [Graceful Fallback Strategy](#7-graceful-fallback-strategy)
8. [CompileToMethod Support](#8-compiletomethod-support)
9. [Architecture and Design](#9-architecture-and-design)
10. [Implementation Plan](#10-implementation-plan)
11. [Estimated Performance Impact](#11-estimated-performance-impact)
12. [Risk Analysis](#12-risk-analysis)
13. [References and Source Locations](#13-references-and-source-locations)
14. [Testing and Validation Strategy](#14-testing-and-validation-strategy)
15. [Appendix A: StackSpiller Deep Dive](#appendix-a-stackspiller-deep-dive)
16. [Appendix B: DynamicMethod Constructor Differences](#appendix-b-dynamicmethod-constructor-differences)
17. [Appendix C: Closure Strategy Comparison](#appendix-c-closure-strategy-comparison)
18. [Appendix D: CompileToMethod History in .NET](#appendix-d-compiletomethod-history-in-net)

---

## 1. Problem Statement

### The Dilemma

.NET's `System.Linq.Expressions.Expression.Compile()` is the standard way to compile
expression trees into executable delegates at runtime. It is correct and complete,
handling all edge cases including complex closures, nested lambdas, try/catch with
stack spilling, and more. However, it is **10-46x slower** at compilation than
FastExpressionCompiler (FEC).

FEC (`Expression.CompileFast()`) achieves dramatically faster compilation times but
**fails in many situations**, returning `null` or producing incorrect results for
complex expression patterns. This is inherent to its single-pass architecture, which
trades correctness for speed.

### The Goal

Build **Hyperbee.ExpressionCompiler** -- a new expression compiler that:

1. Matches or approaches FEC's compilation speed (10-40x faster than system compiler)
2. Handles all expression tree patterns correctly (matching system compiler correctness)
3. Uses a proper compiler IR architecture that is extensible and maintainable
4. Serves as a drop-in replacement for `Expression.Compile()`

### Why Not Just Use FEC with Fallback?

The common pattern today is:

```csharp
var compiled = expression.CompileFast(ifFastFailedReturnNull: true);
compiled ??= expression.Compile();
```

This works but has problems:

- FEC's failure detection is incomplete -- it sometimes produces incorrect delegates
  instead of returning null (see FEC issue #495 and others)
- The fallback to the slow system compiler defeats the purpose when it triggers
- Two compilation libraries means two sets of bugs, two sets of behaviors to reason about
- FEC's single-pass architecture makes it fundamentally difficult to fix edge cases
  without introducing new regressions

### Why Not Fork and Optimize the System Compiler?

The system compiler's architecture makes incremental optimization difficult:

- The StackSpiller's tree-rewriting approach is inherently allocation-heavy due to
  expression tree immutability
- The multi-pass design (StackSpiller → VariableBinder → LambdaCompiler) requires
  three full tree traversals
- The closure strategy (StrongBox<T> per captured variable) is deeply embedded
- Removing any one bottleneck still leaves the others

A ground-up redesign with a proper IR is more practical than trying to incrementally
fix an architecture that is fundamentally mismatched to the performance goal.

---

## 2. Background and Context

### Expression Trees in .NET

`System.Linq.Expressions` provides an API for building expression trees -- data
structures representing code as a tree of nodes (AST). These trees can be inspected,
transformed, and compiled into executable delegates.

Expression trees are used extensively in:

- ORM libraries (Entity Framework, NHibernate) for query translation
- Serialization libraries for dynamic accessor generation
- IoC/DI containers for factory delegate compilation
- Dynamic proxy libraries
- Rule engines and DSLs

Many of these use cases compile expression trees on hot paths or during application
startup, making compilation speed critical.

### The Two Existing Compilers

| | System Compiler | FastExpressionCompiler |
|---|---|---|
| NuGet Package | Built into .NET runtime | `FastExpressionCompiler` |
| Repository | dotnet/runtime | dadhi/FastExpressionCompiler |
| License | MIT | MIT |
| API | `Expression.Compile()` | `Expression.CompileFast()` |
| Architecture | Multi-pass tree rewriting + IL emission | Single-pass IL emission |
| Compilation Speed | Baseline (slow) | 10-46x faster |
| Delegate Execution Speed | ~11ns | ~7-10ns |
| Correctness | Complete | Fails on complex patterns |
| Closure Strategy | StrongBox<T> per captured variable | ArrayClosure with flat object[] |
| DynamicMethod Hosting | Anonymously hosted (sandbox) | Type-associated |

---

## 3. Analysis of the System Expression Compiler

### Source Code Location

The system compiler lives in the dotnet/runtime repository:

```
dotnet/runtime/src/libraries/System.Linq.Expressions/
  src/System/Linq/Expressions/Compiler/
    LambdaCompiler.cs              -- Main compiler, DynamicMethod creation
    LambdaCompiler.Lambda.cs       -- Nested lambda compilation
    LambdaCompiler.Expressions.cs  -- Expression-specific IL emission
    LambdaCompiler.Statements.cs   -- Statement-specific IL emission
    LambdaCompiler.Binary.cs       -- Binary expression IL emission
    LambdaCompiler.Unary.cs        -- Unary expression IL emission
    LambdaCompiler.Address.cs      -- Address-of operations
    CompilerScope.cs               -- Closure/scope management
    CompilerScope.Storage.cs       -- Variable storage strategies
    StackSpiller.cs                -- Stack spilling tree rewriter
    StackSpiller.Generated.cs      -- Expression type dispatcher
    StackSpiller.Temps.cs          -- Temporary variable management
    StackSpiller.ChildRewriter.cs  -- Child expression rewriting
    StackSpiller.Bindings.cs       -- Member binding rewriting
    VariableBinder.cs              -- Variable binding analysis
    BoundConstants.cs              -- Constant management
    ILGen.cs                       -- ILGenerator extension methods
    KeyedStack.cs                  -- Local variable reuse pool
```

### Compilation Pipeline

```
Expression.Compile()
    │
    ▼
LambdaCompiler.Compile(lambda)     [static entry point]
    │
    ├── AnalyzeLambda(ref lambda)
    │       │
    │       ├── StackSpiller.AnalyzeLambda(lambda)
    │       │       └── Recursive tree walk, rewrites tree to ensure
    │       │           empty stack at try/loop/goto boundaries
    │       │           Returns: new LambdaExpression (or original if unchanged)
    │       │
    │       └── VariableBinder.Bind(lambda)
    │               └── Recursive tree walk, determines variable scoping,
    │                   closure requirements, and constant binding
    │                   Returns: AnalyzedTree with scope/constant info
    │
    ├── new LambdaCompiler(tree, lambda)
    │       └── Creates DynamicMethod (anonymously hosted)
    │           new DynamicMethod(name, returnType, parameterTypes, true)
    │
    ├── EmitLambdaBody()
    │       └── Recursive tree walk #3, emits IL via ILGenerator
    │           For nested lambdas: creates additional DynamicMethod instances
    │
    └── CreateDelegate()
            └── method.CreateDelegate(type, new Closure(constants, null))
```

Key observation: **Three full recursive traversals** of the expression tree.

### DynamicMethod Creation (Verified from Source)

```csharp
// LambdaCompiler.cs - constructor
private LambdaCompiler(AnalyzedTree tree, LambdaExpression lambda)
{
    Type[] parameterTypes = GetParameterTypes(lambda, typeof(Closure));

    int lambdaMethodIndex = Interlocked.Increment(ref s_lambdaMethodIndex);
    var method = new DynamicMethod(
        lambda.Name ?? ("lambda_method" + lambdaMethodIndex.ToString()),
        lambda.ReturnType,
        parameterTypes,
        true);  // restrictedSkipVisibility -- ANONYMOUSLY HOSTED

    _tree = tree;
    _lambda = lambda;
    _method = method;
    _ilg = method.GetILGenerator();
    _hasClosureArgument = true;
    _scope = tree.Scopes[lambda];
    _boundConstants = tree.Constants[lambda];

    InitializeMethod();
}
```

### Myth Busted: No AssemblyBuilder in Compile()

A common misconception is that the system compiler uses AssemblyBuilder/TypeBuilder
for complex closures. **This is false.** The TypeBuilder code path only exists behind
`#if FEATURE_COMPILE_TO_METHODBUILDER` and is exclusively used by the separate
`CompileToMethod(MethodBuilder)` API. The standard `Compile()` **always** uses
`DynamicMethod`, even for nested lambdas.

### Closure Implementation

The system compiler wraps captured variables in `StrongBox<T>`, stored in an
`object[]` inside a `Closure` object:

```csharp
// Runtime closure structure
public sealed class Closure
{
    public readonly object[] Constants;  // bound constants + nested delegates
    public readonly object[] Locals;     // StrongBox<T> instances for hoisted vars
}
```

Accessing a captured `int` variable requires:
1. Load closure argument (Ldarg_0)
2. Load the Locals array (Ldfld)
3. Load array element by index (Ldelem_Ref)
4. Cast to StrongBox<int> (Castclass)
5. Load the Value field (Ldfld)

That's **5 IL instructions with a type cast** for every captured variable access.

---

## 4. Analysis of FastExpressionCompiler (FEC)

### Source Code Location

```
dadhi/FastExpressionCompiler/
  src/FastExpressionCompiler/
    FastExpressionCompiler.cs    -- The entire compiler (single large file)
```

### Compilation Pipeline

```
Expression.CompileFast()
    │
    ▼
ExpressionCompiler.TryCompile()
    │
    ├── TryCollectInfo()
    │       └── Single tree walk: collects constants, nested lambdas,
    │           captured variables. Deduplicates nested lambdas.
    │           NO tree rewriting. NO StackSpiller equivalent.
    │
    ├── new DynamicMethod("", returnType, closureAndParamTypes,
    │                      typeof(ArrayClosure), true)
    │       └── Type-associated, NOT anonymously hosted
    │
    ├── TryEmit()
    │       └── Single-pass IL emission directly from expression tree
    │           Handles stack spilling inline during emission
    │           Returns false if it encounters unsupported patterns
    │
    └── DynamicMethod.CreateDelegate(type, arrayClosureInstance)
```

Key observation: **At most two tree traversals** (collect + emit), often with less
analysis overhead than the system compiler's single StackSpiller pass.

### Closure Implementation

```csharp
public class ArrayClosure
{
    public readonly object[] ConstantsAndNestedLambdas;
}

public class ArrayClosureWithNonPassedParams : ArrayClosure
{
    public readonly object[] NonPassedParams;  // captured from outer scope
}
```

No StrongBox<T> wrappers. Captured values are stored directly in the array.
Value types are boxed into the array but accessed without the extra StrongBox
indirection.

### Why FEC Fails

FEC's single-pass IL emission means it must handle all complexity (stack spilling,
closure scoping, evaluation order) simultaneously while emitting IL. This leads to
failures when:

- Complex try/catch blocks interact with compound assignments
- Return gotos from within TryCatch blocks with compound values
- Certain patterns of nested lambda captures
- Some edge cases with by-ref arguments and spilling
- Extension expression types that reduce to complex patterns

The fundamental issue is architectural: a single-pass emitter has no room for
multi-step analysis. When FEC encounters a pattern it can't handle on-the-fly,
it has no fallback within its own architecture.

---

## 5. Root Cause Analysis: Why the System Compiler Is Slow

### Ranked by Impact

#### 1. StackSpiller Tree Rewriting (HIGHEST IMPACT)

The StackSpiller walks the entire expression tree recursively. Because expression
tree nodes are immutable, any change deep in the tree causes "Copy propagation" --
every ancestor node must be recreated.

For a tree with N nodes where a single try/catch block triggers spilling:
- The spiller visits all N nodes
- It allocates new nodes for every ancestor of the spilled node
- Each new node allocates: the node itself + ReadOnlyCollection + backing array
- **Estimated: 60-100+ heap allocations for a 100-node tree**

For trees WITHOUT try/catch (the common case), the StackSpiller visits all N
nodes and returns `RewriteAction.None` everywhere -- **pure waste**.

See [Appendix A](#appendix-a-stackspiller-deep-dive) for a detailed walkthrough
of the StackSpiller source code.

#### 2. Three Full Tree Traversals (HIGH IMPACT)

```
Pass 1: StackSpiller    -- recursive walk of all nodes
Pass 2: VariableBinder  -- recursive walk of all nodes
Pass 3: LambdaCompiler  -- recursive walk of all nodes (IL emission)
```

Each traversal involves virtual dispatch for every node, dictionary lookups for
scope/variable resolution, and stack frame overhead for the recursion itself.

#### 3. Closure Strategy Overhead (MEDIUM IMPACT)

StrongBox<T> allocation per captured variable adds:
- Compilation time: allocating StrongBox objects, emitting cast instructions
- Runtime overhead: extra indirection on every captured variable access

#### 4. Anonymously Hosted DynamicMethod (MODERATE IMPACT)

The system compiler uses `DynamicMethod(name, returnType, types, bool)` which
creates an "anonymously hosted" dynamic method associated with a system-generated
anonymous assembly. FEC uses `DynamicMethod(name, returnType, types, Type, bool)`
which associates the method with an existing type's module.

The anonymous hosting was designed for .NET Framework partial-trust sandboxing,
largely irrelevant in modern .NET. It still carries overhead from anonymous
assembly management and cross-assembly type resolution.

See [Appendix B](#appendix-b-dynamicmethod-constructor-differences) for details.

#### 5. Object Allocation During Analysis (MEDIUM IMPACT)

The StackSpiller and VariableBinder create significant garbage:
- StackSpiller's ChildRewriter allocates arrays for child expressions
- VariableBinder creates CompilerScope objects and dictionaries
- BoundConstants accumulates constants into collections
- HoistedLocals manages multi-level scope chains with dictionaries

#### 6. IL Emission Quality (LOW IMPACT)

Both compilers use ILGenerator and emit similar CIL. FEC is slightly more
diligent about short-form opcodes, but this has minimal impact on either
compilation or execution speed.

---

## 6. Proposed Strategy: IR-Based Expression Compiler

### Core Insight

The current system compiler tries to go directly from AST (expression tree) to
IL with tree-rewriting passes in between. This is the wrong architecture:

- The AST is optimized for construction and inspection, not transformation
- Immutability makes transformation expensive (copy-on-write for entire ancestor chains)
- Tree structures are cache-unfriendly (pointer chasing)

Every serious compiler uses an intermediate representation (IR):

- **Roslyn**: Syntax Tree → Bound Tree → Lowered Bound Tree → IL
- **LLVM**: AST → LLVM IR → Optimized IR → Machine Code
- **.NET JIT**: IL → JIT IR (SSA-based) → Register-allocated → Native Code

### The Proposed Architecture

```
Expression Tree
      │
      ▼
  ┌─────────────────────┐
  │  Lowering Pass       │  Single recursive walk of expression tree.
  │  (Tree → IR)         │  Only code that ever touches the expression tree.
  └─────────────────────┘
      │
      ▼
  ┌─────────────────────┐
  │  IR Instruction List │  Flat, mutable, struct-based.
  │  (List<IRInst>)      │  Cache-friendly. Cheap to scan and modify.
  └─────────────────────┘
      │
      ▼
  ┌─────────────────────┐
  │  Pass 1: Stack       │  Linear scan. Inserts StoreLocal/LoadLocal
  │  Spilling            │  when BeginTry found with non-empty stack.
  └─────────────────────┘  ~50 lines. Zero tree allocations.
      │
      ▼
  ┌─────────────────────┐
  │  Pass 2: Closure     │  Linear scan. Identifies captured variables.
  │  Analysis            │  Rewrites LoadLocal/StoreLocal to closure ops.
  └─────────────────────┘  Decides closure strategy.
      │
      ▼
  ┌─────────────────────┐
  │  Pass 3: Peephole    │  Linear scan. Optional optimizations:
  │  Optimization        │  redundant load/store elimination,
  └─────────────────────┘  constant folding, short-form opcodes.
      │
      ▼
  ┌─────────────────────┐
  │  IL Emission         │  Linear scan. 1:1 mapping from IR to CIL.
  │  (IR → ILGenerator)  │  Trivially simple.
  └─────────────────────┘
      │
      ▼
  DynamicMethod.CreateDelegate()
```

### Why This Is Better

| Concern | System Compiler | FEC | IR-Based |
|---|---|---|---|
| Tree traversals | 3 recursive | 1-2 recursive | 1 recursive (lowering only) |
| Stack spilling | Rewrites immutable tree | Inline during emission | List insertion (zero alloc) |
| Closure analysis | Separate tree walk | During collection | Linear scan of IR |
| Data structure for passes | Immutable tree (pointer-chasing) | None (single-pass) | Flat struct list (cache-friendly) |
| Adding new optimizations | New recursive tree rewriter | Impossible (single-pass) | New linear scan |
| Correctness at scale | Complete | Fails on complex patterns | Complete (multi-pass analysis) |
| Compilation speed | Baseline | 10-46x faster | Target: 10-30x faster |

The IR approach gives us the **analysis capability** of the system compiler
(multiple passes can handle complex patterns) with the **performance profile**
of FEC (flat data structures, minimal allocation, no tree rewriting).

---

## 7. Graceful Fallback Strategy

### The Question

Should Hyperbee.ExpressionCompiler return `null` (like FEC's `ifFastFailedReturnNull`)
when it encounters an expression pattern it cannot compile, allowing the caller to
fall back to the system compiler?

### Recommendation: Yes, With a Layered Approach

The compiler should support three modes of operation:

#### Mode 1: Strict (default)

Throws `NotSupportedException` for unsupported expression patterns. This is the
correct behavior for a compiler that claims to be a drop-in replacement. As
implementation progresses through phases, fewer patterns will be unsupported.

```csharp
// Throws if the expression cannot be compiled
Delegate result = HyperbeeCompiler.Compile(lambda);
```

#### Mode 2: TryCompile (return null on failure)

Returns `null` if the expression cannot be compiled, allowing the caller to
fall back to the system compiler. This is the FEC-style pattern.

```csharp
// Returns null if the expression cannot be compiled
Delegate? result = HyperbeeCompiler.TryCompile(lambda);
result ??= lambda.Compile(); // fall back to system compiler
```

#### Mode 3: Compile with automatic fallback

Attempts Hyperbee compilation first, automatically falls back to the system
compiler on failure. This is the most convenient API for consumers who just
want the fastest correct result.

```csharp
// Tries Hyperbee first, falls back to system compiler automatically
Delegate result = HyperbeeCompiler.CompileWithFallback(lambda);
```

### API Design

```csharp
public static class HyperbeeCompiler
{
    /// <summary>
    /// Compiles the expression. Throws on unsupported patterns.
    /// </summary>
    public static Delegate Compile(LambdaExpression lambda);

    /// <summary>
    /// Compiles the expression. Returns null on unsupported patterns.
    /// </summary>
    public static Delegate? TryCompile(LambdaExpression lambda);

    /// <summary>
    /// Compiles the expression. Falls back to system compiler on failure.
    /// </summary>
    public static Delegate CompileWithFallback(LambdaExpression lambda);

    // Generic overloads
    public static TDelegate Compile<TDelegate>(Expression<TDelegate> lambda)
        where TDelegate : Delegate;

    public static TDelegate? TryCompile<TDelegate>(Expression<TDelegate> lambda)
        where TDelegate : Delegate;

    public static TDelegate CompileWithFallback<TDelegate>(Expression<TDelegate> lambda)
        where TDelegate : Delegate;
}
```

### Failure Detection: Lessons from FEC

FEC's biggest problem is not that it fails -- it's that it **fails silently**.
Sometimes `CompileFast()` produces a delegate that behaves incorrectly rather
than returning `null`. This happens because FEC's single-pass architecture
makes it difficult to detect all failure conditions before IL has already been
partially emitted.

The IR-based architecture gives Hyperbee a significant advantage here:

1. **Lowering failures are caught before any IL is emitted.** If an expression
   node type is not supported, the lowering pass throws immediately. No partial
   IL exists to clean up.

2. **Pass failures are isolated.** If the stack spill pass or closure analysis
   encounters an unexpected state, the IR can be discarded without side effects.
   DynamicMethod has not been created yet.

3. **IR validation pass (optional).** A validation pass can verify IR correctness
   (stack depth consistency, label targets exist, locals are declared before use)
   before IL emission. This catches bugs in the compiler itself.

```csharp
public static Delegate? TryCompile(LambdaExpression lambda)
{
    try
    {
        // Phase 1: Lower (catches unsupported node types)
        var ir = new IRBuilder();
        var lowerer = new ExpressionLowerer(ir);
        lowerer.Lower(lambda);

        // Phase 2-3: Analysis passes (catches unexpected IR patterns)
        StackSpillPass.Run(ir);
        ClosureAnalysisPass.Run(ir, lambda);

        // Phase 4: Optional IR validation
        if (!IRValidator.Validate(ir))
            return null;

        // Phase 5-6: IL emission (should not fail if IR is valid)
        PeepholePass.Run(ir);
        var method = CreateDynamicMethod(lambda, ir);
        ILEmissionPass.Run(ir, method.GetILGenerator());

        return method.CreateDelegate(lambda.Type, BuildClosure(ir));
    }
    catch (NotSupportedException)
    {
        return null;  // Unsupported expression pattern
    }
    catch (Exception)
    {
        // Unexpected compiler bug -- log and return null
        // In strict mode (Compile), this would rethrow
        return null;
    }
}
```

### Why This Is Better Than FEC's Approach

| Concern | FEC | Hyperbee |
|---|---|---|
| Failure detected before IL emission? | Sometimes no | Always yes (lowering/IR validation) |
| Partial IL corruption possible? | Yes | No (IL emitted only after validated IR) |
| Silent incorrect results? | Possible | Prevented by IR validation |
| Fallback API? | `ifFastFailedReturnNull: true` | `TryCompile()` / `CompileWithFallback()` |
| Caller knows failure happened? | Only if null returned | Yes (null, or exception in strict mode) |

---

## 8. CompileToMethod Support

### Background: What CompileToMethod Was

In .NET Framework 4.0-4.8, `LambdaExpression.CompileToMethod(MethodBuilder)` allowed
compiling an expression tree directly into a `MethodBuilder` within a dynamic assembly.
This was essential for:

- **Language implementations** (IronPython, IronRuby) that compiled scripts to assemblies
- **Code generators** that wanted to persist compiled expression trees to DLLs
- **AOT-like scenarios** where startup cost of runtime compilation was unacceptable
- **Debugging** -- persisted assemblies can be inspected with ILSpy, dotPeek, etc.

### Why It Was Removed from .NET Core

`CompileToMethod` was removed in .NET Core 1.0 and has **never been restored** in any
version of .NET Core, 5, 6, 7, 8, 9, or 10. The code still exists in the dotnet/runtime
source behind the undefined `FEATURE_COMPILE_TO_METHODBUILDER` flag, but the public API
entry point was removed.

**Reasons for removal:**

1. **Layering constraints**: `Reflection.Emit` types like `MethodBuilder` were not in
   .NET Standard, so APIs taking those types could not be in .NET Standard either.
   (This reasoning has been acknowledged as overly restrictive by the .NET team.)

2. **Missing `AssemblyBuilder.Save()`**: .NET Core did not support saving dynamic
   assemblies to disk (the implementation depended on Windows-specific native code).
   Without Save, CompileToMethod was significantly less useful.

3. **Architectural concern** (Jan Kotas, March 2025, GitHub issue #113583): proper
   compilers should decouple the compiler's runtime version from the target runtime
   version. `CompileToMethod` inherently couples them because it uses the running
   CLR's type system to resolve types referenced in the expression tree.

Multiple GitHub issues have requested its return (#20270, #22025, #88555, #113583),
all without resolution.

### The .NET 9+ Enabler: PersistedAssemblyBuilder

.NET 9 introduced `PersistedAssemblyBuilder` -- a fully managed `Reflection.Emit`
implementation that can **save assemblies to disk**. This removes the primary blocker
that made CompileToMethod less useful in .NET Core.

```csharp
// .NET 9+: create a saveable assembly
var ab = new PersistedAssemblyBuilder(
    new AssemblyName("MyCompiledExpressions"),
    typeof(object).Assembly);

ModuleBuilder mob = ab.DefineDynamicModule("Module");
TypeBuilder tb = mob.DefineType("CompiledExpressions",
    TypeAttributes.Public | TypeAttributes.Class);

MethodBuilder mb = tb.DefineMethod("MyMethod",
    MethodAttributes.Public | MethodAttributes.Static,
    typeof(int), new[] { typeof(int), typeof(int) });

// Compile expression tree into the MethodBuilder
HyperbeeCompiler.CompileToMethod(lambda, mb);

tb.CreateType();
ab.Save("MyCompiledExpressions.dll");  // Persist to disk!
```

### Hyperbee CompileToMethod Design

The IR-based architecture makes CompileToMethod a natural extension. The only
difference from the normal `Compile()` path is the final emission target:

```
Normal Compile():        IR → ILGenerator from DynamicMethod
CompileToMethod():       IR → ILGenerator from MethodBuilder
```

The lowering, analysis, and optimization passes are **identical**. Only the
IL emission target changes.

#### API Design

```csharp
public static class HyperbeeCompiler
{
    // --- Existing Compile APIs (DynamicMethod target) ---

    public static Delegate Compile(LambdaExpression lambda);
    public static Delegate? TryCompile(LambdaExpression lambda);
    public static Delegate CompileWithFallback(LambdaExpression lambda);

    // --- CompileToMethod APIs (MethodBuilder target) ---

    /// <summary>
    /// Compiles the expression tree into the provided MethodBuilder.
    /// The MethodBuilder must be a static method on a TypeBuilder.
    /// </summary>
    public static void CompileToMethod(
        LambdaExpression lambda,
        MethodBuilder method);

    /// <summary>
    /// Compiles the expression tree into the provided MethodBuilder.
    /// Returns false if the expression cannot be compiled.
    /// </summary>
    public static bool TryCompileToMethod(
        LambdaExpression lambda,
        MethodBuilder method);

    /// <summary>
    /// Convenience: creates a TypeBuilder with the compiled method and
    /// returns the finalized Type. Optionally saves to disk.
    /// </summary>
    public static Type CompileToType(
        LambdaExpression lambda,
        string typeName = "CompiledExpression",
        string methodName = "Execute",
        string? savePath = null);
}
```

#### Constant Handling: The Key Challenge

The critical difference between `Compile()` (DynamicMethod) and `CompileToMethod()`
(MethodBuilder) is **how non-literal constants are handled**.

With `DynamicMethod`, non-literal constants (object references like service instances,
cached values, etc.) can be stored in the closure object which is passed at runtime.
The delegate carries a reference to the closure.

With `MethodBuilder`, if the assembly is **persisted to disk**, object references
cannot be serialized. There are several strategies:

**Strategy A: Reject non-embeddable constants (strict)**

```csharp
// Throw if any ConstantExpression holds a non-literal value
if (constantExpr.Value != null && !IsEmbeddable(constantExpr.Value))
    throw new NotSupportedException(
        $"CompileToMethod cannot embed constant of type {constantExpr.Value.GetType()}. " +
        "Use a parameter instead.");
```

**Strategy B: Lift constants to static fields (runtime-only)**

For in-memory assemblies (not persisted), constants can be stored in static fields
of the TypeBuilder and initialized at runtime:

```csharp
// During IL emission for CompileToMethod:
FieldBuilder field = typeBuilder.DefineField(
    $"__const_{index}",
    constantType,
    FieldAttributes.Private | FieldAttributes.Static);

// Emit: load from static field instead of closure
ilg.Emit(OpCodes.Ldsfld, field);

// After type creation, set the field values:
Type createdType = typeBuilder.CreateType();
createdType.GetField("__const_0", ...).SetValue(null, constantValue);
```

**Strategy C: Lift constants to constructor parameters (most flexible)**

Generate a constructor that accepts the non-embeddable constants and stores
them in instance fields. The compiled method becomes an instance method:

```csharp
// Generated type:
public class CompiledExpression
{
    private readonly IService _const0;
    private readonly ILogger _const1;

    public CompiledExpression(IService const0, ILogger const1)
    {
        _const0 = const0;
        _const1 = const1;
    }

    public int Execute(int arg0, string arg1)
    {
        // compiled expression body, accessing _const0, _const1
    }
}
```

**Recommended approach for Hyperbee:**

- Default to Strategy B (static fields) for in-memory CompileToMethod
- Use Strategy A (reject) for persisted assemblies, with a clear error message
  guiding the user to replace ConstantExpression with ParameterExpression
- Strategy C as a future option for the `CompileToType` convenience API

#### Implementation in the IR Pipeline

The IR needs a new emission backend that targets `MethodBuilder` instead of
`DynamicMethod`:

```csharp
public static void CompileToMethod(LambdaExpression lambda, MethodBuilder method)
{
    // Validate: must be static, must be on a TypeBuilder
    if (!method.IsStatic)
        throw new ArgumentException("MethodBuilder must be static.");

    // Phase 1-4: Identical to Compile()
    var ir = new IRBuilder();
    var lowerer = new ExpressionLowerer(ir);
    lowerer.Lower(lambda);
    StackSpillPass.Run(ir);
    ClosureAnalysisPass.Run(ir, lambda);
    PeepholePass.Run(ir);

    // Phase 5: Set method signature
    method.SetReturnType(lambda.ReturnType);
    method.SetParameters(GetParameterTypes(lambda));

    // Phase 6: Emit IL into the MethodBuilder's ILGenerator
    // Use MethodBuilder emission mode (constants → static fields, not closure)
    var emitter = new ILEmissionPass(EmissionMode.MethodBuilder);
    emitter.Run(ir, method.GetILGenerator());
}
```

The `ILEmissionPass` needs an `EmissionMode` to handle the differences:

| IR Operation | DynamicMethod Mode | MethodBuilder Mode |
|---|---|---|
| `LoadConst` (literal) | Emit `Ldc_I4`, `Ldstr`, etc. | Same |
| `LoadConst` (object ref) | Load from closure array | Load from static field |
| `LoadClosureVar` | Load from closure argument | Load from static field or instance field |
| `StoreClosureVar` | Store to closure argument | Store to static field or instance field |

#### Use Cases Enabled

1. **Startup optimization**: Compile expression trees during build/publish,
   save to DLL, load at runtime without recompilation.

2. **Debugging**: Inspect the generated IL with ILSpy or dotPeek to verify
   correctness.

3. **IL verification**: Run the persisted assembly through ILVerify to catch
   invalid IL before runtime.

4. **AOT compatibility**: Pre-compiled assemblies avoid `DynamicMethod` which
   is not available in AOT/NativeAOT scenarios.

5. **Plugin architectures**: Generate and save compiled expression assemblies
   that can be loaded dynamically.

6. **Testing**: Round-trip test -- compile to DLL, load DLL, execute, compare
   results with `Compile()` output. This is a powerful correctness validation
   tool for the compiler itself.

---

## 9. Architecture and Design

### 9.1 IR Instruction Design

The IR uses a flat, stack-based instruction format close to CIL but at a
slightly higher abstraction level:

```csharp
/// <summary>
/// A single IR instruction. Value type for cache-friendly storage in lists.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public readonly struct IRInstruction
{
    /// <summary>The operation.</summary>
    public readonly IROp Op;

    /// <summary>
    /// Operand whose meaning depends on Op:
    ///   LoadConst       → index into operand table
    ///   LoadLocal       → local variable index
    ///   StoreLocal      → local variable index
    ///   LoadArg         → argument index
    ///   Call/CallVirt   → index into operand table (MethodInfo)
    ///   NewObj          → index into operand table (ConstructorInfo)
    ///   Branch*         → label index
    ///   LoadField       → index into operand table (FieldInfo)
    ///   Box/Unbox       → index into operand table (Type)
    ///   BeginTry        → try block ID
    ///   BeginCatch      → index into operand table (Type)
    /// </summary>
    public readonly int Operand;

    public IRInstruction(IROp op, int operand = 0)
    {
        Op = op;
        Operand = operand;
    }
}
```

### 9.2 IR Operation Codes

```csharp
public enum IROp : byte
{
    // Constants and variables
    Nop,
    LoadConst,              // Push constant from operand table
    LoadNull,               // Push null
    LoadLocal,              // Push local variable
    StoreLocal,             // Pop and store to local variable
    LoadArg,                // Push argument
    StoreArg,               // Pop and store to argument
    LoadClosureVar,         // Push variable from closure (post closure-analysis)
    StoreClosureVar,        // Pop and store to closure variable

    // Fields and properties
    LoadField,              // Push field value (instance on stack)
    StoreField,             // Store to field (instance and value on stack)
    LoadStaticField,        // Push static field value
    StoreStaticField,       // Pop and store to static field

    // Array operations
    LoadElement,            // Push array element
    StoreElement,           // Store to array element
    LoadArrayLength,        // Push array length
    NewArray,               // Create new array

    // Arithmetic
    Add, Sub, Mul, Div, Rem,
    AddChecked, SubChecked, MulChecked,
    Negate, NegateChecked,
    And, Or, Xor, Not,
    LeftShift, RightShift,

    // Comparison
    Ceq, Clt, Cgt,
    CltUn, CgtUn,

    // Conversion
    Convert,                // Type conversion (operand → Type)
    ConvertChecked,
    Box, Unbox, UnboxAny,
    CastClass, IsInst,

    // Method calls
    Call,                   // Static/non-virtual call
    CallVirt,               // Virtual/interface call
    NewObj,                 // Constructor call

    // Control flow
    Branch,                 // Unconditional branch
    BranchTrue,             // Branch if true
    BranchFalse,            // Branch if false
    Label,                  // Branch target marker

    // Exception handling
    BeginTry,               // Enter try block
    BeginCatch,             // Enter catch handler
    BeginFinally,           // Enter finally handler
    BeginFault,             // Enter fault handler
    EndTryCatch,            // End exception handling block
    Throw,                  // Throw exception
    Rethrow,                // Rethrow current exception

    // Stack manipulation
    Dup,                    // Duplicate top of stack
    Pop,                    // Discard top of stack
    Ret,                    // Return

    // Scope markers (for variable lifetime tracking)
    BeginScope,             // Enter a new variable scope
    EndScope,               // Exit variable scope

    // Delegate creation (high-level, expanded during closure pass)
    CreateDelegate,         // Create delegate from nested lambda IR

    // Special
    InitObj,                // Initialize value type
    LoadAddress,            // Load address of local/arg/field
    LoadToken,              // Load runtime type/method/field token
    Switch,                 // Switch table branch
}
```

### 9.3 IR Builder

```csharp
public class IRBuilder
{
    // The instruction stream -- the heart of the IR
    private readonly List<IRInstruction> _instructions = new();

    // Side tables
    private readonly List<object> _operands = new();         // constants, MethodInfo, etc.
    private readonly List<LocalInfo> _locals = new();        // local variable metadata
    private readonly List<LabelInfo> _labels = new();        // branch targets
    private readonly List<TryBlockInfo> _tryBlocks = new();  // exception handling regions

    // Closure analysis results (populated by closure pass)
    public ClosureStrategy ClosureStrategy { get; set; }
    public HashSet<int> CapturedLocals { get; } = new();

    // --- Instruction emission ---

    public void Emit(IROp op)
        => _instructions.Add(new IRInstruction(op));

    public void Emit(IROp op, int operand)
        => _instructions.Add(new IRInstruction(op, operand));

    // --- Operand table ---

    public int AddOperand(object value)
    {
        int index = _operands.Count;
        _operands.Add(value);
        return index;
    }

    // --- Local variables ---

    public int DeclareLocal(Type type, string? name = null)
    {
        int index = _locals.Count;
        _locals.Add(new LocalInfo(type, name, scopeDepth: _currentScope));
        return index;
    }

    // --- Labels ---

    public int DefineLabel()
    {
        int index = _labels.Count;
        _labels.Add(new LabelInfo());
        return index;
    }

    public void MarkLabel(int labelIndex)
    {
        _labels[labelIndex] = _labels[labelIndex] with
        {
            InstructionIndex = _instructions.Count
        };
        Emit(IROp.Label, labelIndex);
    }

    // --- Instruction list manipulation (for passes) ---

    public IReadOnlyList<IRInstruction> Instructions => _instructions;

    public void InsertAt(int position, IRInstruction instruction)
        => _instructions.Insert(position, instruction);

    public void RemoveAt(int position)
        => _instructions.RemoveAt(position);

    public void ReplaceAt(int position, IRInstruction instruction)
        => _instructions[position] = instruction;
}

public readonly record struct LocalInfo(Type Type, string? Name, int ScopeDepth);
public readonly record struct LabelInfo(int InstructionIndex = -1);

public readonly record struct TryBlockInfo(
    int TryStart,
    int TryEnd,
    int HandlerStart,
    int HandlerEnd,
    TryBlockKind Kind,
    Type? CatchType);

public enum TryBlockKind { TryCatch, TryFinally, TryFault }

public enum ClosureStrategy
{
    None,                // No captured variables
    ConstantsOnly,       // Only constants, use flat object[]
    TypedClosure,        // Generate a typed closure (ClosureN<T1,T2,...>)
    ArrayClosure,        // Use ArrayClosure with object[]
}
```

### 9.4 Expression Lowering Pass

```csharp
/// <summary>
/// Lowers a System.Linq.Expressions expression tree into flat IR instructions.
/// This is the ONLY code that traverses the expression tree.
/// </summary>
public class ExpressionLowerer
{
    private readonly IRBuilder _ir;
    private readonly Dictionary<ParameterExpression, int> _parameterMap = new();
    private readonly Dictionary<ParameterExpression, int> _localMap = new();
    private int _scopeDepth;

    public ExpressionLowerer(IRBuilder ir)
    {
        _ir = ir;
    }

    public void Lower(LambdaExpression lambda)
    {
        // Map lambda parameters to argument indices
        // (index 0 is reserved for closure if needed)
        int argOffset = 1; // reserve slot 0 for closure
        for (int i = 0; i < lambda.Parameters.Count; i++)
        {
            _parameterMap[lambda.Parameters[i]] = i + argOffset;
        }

        LowerExpression(lambda.Body);
        _ir.Emit(IROp.Ret);
    }

    private void LowerExpression(Expression node)
    {
        if (node == null) return;

        switch (node.NodeType)
        {
            case ExpressionType.Constant:
                LowerConstant((ConstantExpression)node);
                break;

            case ExpressionType.Parameter:
                LowerParameter((ParameterExpression)node);
                break;

            case ExpressionType.Add:
            case ExpressionType.Subtract:
            case ExpressionType.Multiply:
            // ... other binary operations
                LowerBinary((BinaryExpression)node);
                break;

            case ExpressionType.Call:
                LowerMethodCall((MethodCallExpression)node);
                break;

            case ExpressionType.Lambda:
                LowerNestedLambda((LambdaExpression)node);
                break;

            case ExpressionType.Try:
                LowerTryCatch((TryExpression)node);
                break;

            case ExpressionType.Block:
                LowerBlock((BlockExpression)node);
                break;

            case ExpressionType.Conditional:
                LowerConditional((ConditionalExpression)node);
                break;

            // ... all expression types
            // Extension expressions: call node.Reduce() then lower the result

            default:
                if (node.CanReduce)
                {
                    LowerExpression(node.Reduce());
                }
                else
                {
                    throw new NotSupportedException(
                        $"Expression type {node.NodeType} is not supported");
                }
                break;
        }
    }

    private void LowerConstant(ConstantExpression node)
    {
        if (node.Value == null)
        {
            _ir.Emit(IROp.LoadNull);
        }
        else
        {
            _ir.Emit(IROp.LoadConst, _ir.AddOperand(node.Value));
        }
    }

    private void LowerParameter(ParameterExpression node)
    {
        if (_parameterMap.TryGetValue(node, out int argIndex))
        {
            _ir.Emit(IROp.LoadArg, argIndex);
        }
        else if (_localMap.TryGetValue(node, out int localIndex))
        {
            _ir.Emit(IROp.LoadLocal, localIndex);
        }
        else
        {
            // Variable from outer scope -- will be resolved by closure pass
            int local = _ir.DeclareLocal(node.Type, node.Name);
            _localMap[node] = local;
            _ir.Emit(IROp.LoadLocal, local);
        }
    }

    private void LowerBinary(BinaryExpression node)
    {
        if (node.Method != null)
        {
            // Operator overload -- emit as method call
            LowerExpression(node.Left);
            LowerExpression(node.Right);
            _ir.Emit(IROp.Call, _ir.AddOperand(node.Method));
            return;
        }

        LowerExpression(node.Left);
        LowerExpression(node.Right);

        _ir.Emit(node.NodeType switch
        {
            ExpressionType.Add => IROp.Add,
            ExpressionType.AddChecked => IROp.AddChecked,
            ExpressionType.Subtract => IROp.Sub,
            ExpressionType.SubtractChecked => IROp.SubChecked,
            ExpressionType.Multiply => IROp.Mul,
            ExpressionType.MultiplyChecked => IROp.MulChecked,
            ExpressionType.Divide => IROp.Div,
            ExpressionType.Modulo => IROp.Rem,
            ExpressionType.And => IROp.And,
            ExpressionType.Or => IROp.Or,
            ExpressionType.ExclusiveOr => IROp.Xor,
            ExpressionType.LeftShift => IROp.LeftShift,
            ExpressionType.RightShift => IROp.RightShift,
            ExpressionType.Equal => IROp.Ceq,
            ExpressionType.LessThan => IROp.Clt,
            ExpressionType.GreaterThan => IROp.Cgt,
            _ => throw new NotSupportedException($"Binary op {node.NodeType}")
        });
    }

    private void LowerMethodCall(MethodCallExpression node)
    {
        if (node.Object != null)
        {
            LowerExpression(node.Object);
        }
        for (int i = 0; i < node.Arguments.Count; i++)
        {
            LowerExpression(node.Arguments[i]);
        }
        _ir.Emit(
            node.Method.IsVirtual ? IROp.CallVirt : IROp.Call,
            _ir.AddOperand(node.Method));
    }

    private void LowerTryCatch(TryExpression node)
    {
        int tryBlockId = _ir.DefineLabel();

        _ir.Emit(IROp.BeginTry, tryBlockId);
        LowerExpression(node.Body);

        if (node.Handlers != null)
        {
            foreach (var handler in node.Handlers)
            {
                _ir.Emit(IROp.BeginCatch,
                    _ir.AddOperand(handler.Test ?? typeof(Exception)));

                if (handler.Variable != null)
                {
                    int local = _ir.DeclareLocal(handler.Variable.Type, handler.Variable.Name);
                    _localMap[handler.Variable] = local;
                    _ir.Emit(IROp.StoreLocal, local);
                }

                if (handler.Filter != null)
                {
                    LowerExpression(handler.Filter);
                    // TODO: filter support
                }

                LowerExpression(handler.Body);
            }
        }

        if (node.Finally != null)
        {
            _ir.Emit(IROp.BeginFinally);
            LowerExpression(node.Finally);
        }

        if (node.Fault != null)
        {
            _ir.Emit(IROp.BeginFault);
            LowerExpression(node.Fault);
        }

        _ir.Emit(IROp.EndTryCatch, tryBlockId);
    }

    private void LowerBlock(BlockExpression node)
    {
        _scopeDepth++;
        _ir.Emit(IROp.BeginScope);

        // Declare block variables
        foreach (var variable in node.Variables)
        {
            int local = _ir.DeclareLocal(variable.Type, variable.Name);
            _localMap[variable] = local;
        }

        // Lower all expressions in the block
        for (int i = 0; i < node.Expressions.Count; i++)
        {
            LowerExpression(node.Expressions[i]);

            // All expressions except the last have their result discarded
            if (i < node.Expressions.Count - 1
                && node.Expressions[i].Type != typeof(void))
            {
                _ir.Emit(IROp.Pop);
            }
        }

        _ir.Emit(IROp.EndScope);
        _scopeDepth--;
    }

    private void LowerConditional(ConditionalExpression node)
    {
        int falseLabel = _ir.DefineLabel();
        int endLabel = _ir.DefineLabel();

        LowerExpression(node.Test);
        _ir.Emit(IROp.BranchFalse, falseLabel);

        LowerExpression(node.IfTrue);
        _ir.Emit(IROp.Branch, endLabel);

        _ir.MarkLabel(falseLabel);
        LowerExpression(node.IfFalse);

        _ir.MarkLabel(endLabel);
    }

    private void LowerNestedLambda(LambdaExpression node)
    {
        // Create a sub-IRBuilder for the nested lambda
        // The closure pass will later wire up captured variables
        _ir.Emit(IROp.CreateDelegate, _ir.AddOperand(node));
    }
}
```

### 9.5 Stack Spilling Pass

```csharp
/// <summary>
/// Ensures the evaluation stack is empty at try/catch/loop/goto boundaries
/// by inserting StoreLocal/LoadLocal instructions. Operates on the flat IR
/// instruction list -- no expression tree allocations.
/// </summary>
public class StackSpillPass
{
    public static bool Run(IRBuilder ir)
    {
        bool modified = false;

        // First, quick check: does the IR even contain try blocks?
        bool hasTry = false;
        foreach (var inst in ir.Instructions)
        {
            if (inst.Op == IROp.BeginTry)
            {
                hasTry = true;
                break;
            }
        }
        if (!hasTry) return false;  // Nothing to do -- fast exit

        // Track stack depth as we scan
        int stackDepth = 0;
        int i = 0;

        while (i < ir.Instructions.Count)
        {
            var inst = ir.Instructions[i];

            if (inst.Op == IROp.BeginTry && stackDepth > 0)
            {
                // Stack must be empty here. Spill to temps.
                modified = true;
                SpillStack(ir, ref i, stackDepth);
                stackDepth = 0;
            }

            stackDepth += GetStackDelta(ir, inst);
            i++;
        }

        return modified;
    }

    private static void SpillStack(IRBuilder ir, ref int position, int depth)
    {
        // Allocate temporary locals for each value on the stack
        int[] tempLocals = new int[depth];
        for (int s = depth - 1; s >= 0; s--)
        {
            tempLocals[s] = ir.DeclareLocal(typeof(object), $"$spill{s}$");
            ir.InsertAt(position, new IRInstruction(IROp.StoreLocal, tempLocals[s]));
            position++;
        }

        // Find the matching EndTryCatch
        int tryDepth = 0;
        int endPos = position;
        for (int j = position; j < ir.Instructions.Count; j++)
        {
            if (ir.Instructions[j].Op == IROp.BeginTry) tryDepth++;
            if (ir.Instructions[j].Op == IROp.EndTryCatch)
            {
                if (tryDepth == 0) { endPos = j; break; }
                tryDepth--;
            }
        }

        // Reload the spilled values after the try/catch
        for (int s = 0; s < depth; s++)
        {
            endPos++;
            ir.InsertAt(endPos, new IRInstruction(IROp.LoadLocal, tempLocals[s]));
        }
    }

    private static int GetStackDelta(IRBuilder ir, IRInstruction inst)
    {
        return inst.Op switch
        {
            IROp.LoadConst or IROp.LoadNull or IROp.LoadLocal or
            IROp.LoadArg or IROp.LoadClosureVar or IROp.LoadField or
            IROp.LoadStaticField or IROp.LoadArrayLength or IROp.Dup
                => +1,

            IROp.StoreLocal or IROp.StoreArg or IROp.StoreClosureVar or
            IROp.StoreStaticField or IROp.Pop or IROp.Throw or
            IROp.BranchTrue or IROp.BranchFalse
                => -1,

            IROp.StoreField or IROp.StoreElement or IROp.Ceq or
            IROp.Clt or IROp.Cgt or IROp.Add or IROp.Sub or
            IROp.Mul or IROp.Div or IROp.Rem
                => -1,

            IROp.Call or IROp.CallVirt =>
                GetCallStackDelta(ir, inst),

            IROp.NewObj => GetNewObjStackDelta(ir, inst),

            IROp.Ret or IROp.Branch or IROp.Label or IROp.Nop or
            IROp.BeginScope or IROp.EndScope or IROp.BeginTry or
            IROp.EndTryCatch or IROp.BeginFinally or IROp.BeginFault
                => 0,

            _ => 0  // Conservative default
        };
    }

    private static int GetCallStackDelta(IRBuilder ir, IRInstruction inst)
    {
        var method = (MethodInfo)ir.Operands[inst.Operand];
        int pops = method.GetParameters().Length;
        if (!method.IsStatic) pops++;  // instance
        int pushes = (method.ReturnType != typeof(void)) ? 1 : 0;
        return pushes - pops;
    }

    private static int GetNewObjStackDelta(IRBuilder ir, IRInstruction inst)
    {
        var ctor = (ConstructorInfo)ir.Operands[inst.Operand];
        int pops = ctor.GetParameters().Length;
        return 1 - pops;  // pops args, pushes new instance
    }
}
```

### 9.6 Closure Analysis Pass

```csharp
/// <summary>
/// Analyzes variable capture patterns and rewrites local variable access
/// to closure variable access where needed. Decides the optimal closure
/// strategy based on what is captured.
/// </summary>
public class ClosureAnalysisPass
{
    public static void Run(IRBuilder ir, LambdaExpression rootLambda)
    {
        // Step 1: Identify variables that are captured by nested lambdas
        AnalyzeCaptures(ir);

        if (ir.CapturedLocals.Count == 0)
        {
            ir.ClosureStrategy = ClosureStrategy.None;
            return;
        }

        // Step 2: Rewrite captured variable access
        for (int i = 0; i < ir.Instructions.Count; i++)
        {
            var inst = ir.Instructions[i];
            if (inst.Op == IROp.LoadLocal && ir.CapturedLocals.Contains(inst.Operand))
            {
                ir.ReplaceAt(i, new IRInstruction(IROp.LoadClosureVar, inst.Operand));
            }
            else if (inst.Op == IROp.StoreLocal && ir.CapturedLocals.Contains(inst.Operand))
            {
                ir.ReplaceAt(i, new IRInstruction(IROp.StoreClosureVar, inst.Operand));
            }
        }

        // Step 3: Decide closure strategy
        ir.ClosureStrategy = DetermineStrategy(ir);
    }

    private static void AnalyzeCaptures(IRBuilder ir)
    {
        // A variable is "captured" if it is accessed inside a CreateDelegate
        // instruction's scope but declared outside of it.
        // This is a linear scan tracking scope depth.

        int delegateDepth = 0;
        var declaredAtDepth = new Dictionary<int, int>(); // local → depth

        for (int i = 0; i < ir.Instructions.Count; i++)
        {
            var inst = ir.Instructions[i];

            switch (inst.Op)
            {
                case IROp.CreateDelegate:
                    delegateDepth++;
                    break;

                // Track where locals are declared
                case IROp.BeginScope:
                    break;

                case IROp.LoadLocal or IROp.StoreLocal:
                    if (delegateDepth > 0)
                    {
                        var local = ir.Locals[inst.Operand];
                        if (local.ScopeDepth < delegateDepth)
                        {
                            ir.CapturedLocals.Add(inst.Operand);
                        }
                    }
                    break;
            }
        }
    }

    private static ClosureStrategy DetermineStrategy(IRBuilder ir)
    {
        // Check if any captured variables are written to
        bool hasMutableCaptures = false;
        foreach (var inst in ir.Instructions)
        {
            if (inst.Op == IROp.StoreClosureVar)
            {
                hasMutableCaptures = true;
                break;
            }
        }

        int captureCount = ir.CapturedLocals.Count;

        if (!hasMutableCaptures && captureCount <= 8)
            return ClosureStrategy.TypedClosure;  // Use Closure<T1,T2,...>

        return ClosureStrategy.ArrayClosure;  // Fall back to object[]
    }
}
```

### 9.7 Peephole Optimization Pass (Optional)

```csharp
/// <summary>
/// Simple peephole optimizations over the IR instruction list.
/// Each optimization is a pattern match on a small window of instructions.
/// </summary>
public class PeepholePass
{
    public static void Run(IRBuilder ir)
    {
        for (int i = 0; i < ir.Instructions.Count - 1; i++)
        {
            var a = ir.Instructions[i];
            var b = ir.Instructions[i + 1];

            // StoreLocal X; LoadLocal X → Dup; StoreLocal X
            if (a.Op == IROp.StoreLocal && b.Op == IROp.LoadLocal
                && a.Operand == b.Operand)
            {
                ir.InsertAt(i, new IRInstruction(IROp.Dup));
                ir.RemoveAt(i + 2); // remove the LoadLocal
                continue;
            }

            // LoadConst; Pop → remove both (dead store)
            if (a.Op == IROp.LoadConst && b.Op == IROp.Pop)
            {
                ir.RemoveAt(i);
                ir.RemoveAt(i); // b is now at position i
                i--;
                continue;
            }

            // Box T; UnboxAny T → nop (identity roundtrip)
            if (a.Op == IROp.Box && b.Op == IROp.UnboxAny
                && a.Operand == b.Operand)
            {
                ir.RemoveAt(i);
                ir.RemoveAt(i);
                i--;
                continue;
            }
        }
    }
}
```

### 9.8 IL Emission Pass

```csharp
/// <summary>
/// Final pass: emits CIL from the IR instruction list via ILGenerator.
/// This is a straightforward 1:1 mapping.
/// </summary>
public class ILEmissionPass
{
    public static void Run(IRBuilder ir, ILGenerator ilg)
    {
        // Pre-declare all IL locals
        var ilLocals = new LocalBuilder[ir.Locals.Count];
        for (int i = 0; i < ir.Locals.Count; i++)
        {
            ilLocals[i] = ilg.DeclareLocal(ir.Locals[i].Type);
        }

        // Pre-declare all IL labels
        var ilLabels = new Label[ir.Labels.Count];
        for (int i = 0; i < ir.Labels.Count; i++)
        {
            ilLabels[i] = ilg.DefineLabel();
        }

        // Emit instructions
        foreach (var inst in ir.Instructions)
        {
            switch (inst.Op)
            {
                case IROp.Nop:
                    break;

                case IROp.LoadConst:
                    EmitLoadConstant(ilg, ir.Operands[inst.Operand]);
                    break;

                case IROp.LoadNull:
                    ilg.Emit(OpCodes.Ldnull);
                    break;

                case IROp.LoadLocal:
                    EmitLoadLocal(ilg, inst.Operand);
                    break;

                case IROp.StoreLocal:
                    EmitStoreLocal(ilg, inst.Operand);
                    break;

                case IROp.LoadArg:
                    EmitLoadArg(ilg, inst.Operand);
                    break;

                case IROp.Add:
                    ilg.Emit(OpCodes.Add);
                    break;

                case IROp.Call:
                    ilg.Emit(OpCodes.Call, (MethodInfo)ir.Operands[inst.Operand]);
                    break;

                case IROp.CallVirt:
                    ilg.Emit(OpCodes.Callvirt, (MethodInfo)ir.Operands[inst.Operand]);
                    break;

                case IROp.NewObj:
                    ilg.Emit(OpCodes.Newobj, (ConstructorInfo)ir.Operands[inst.Operand]);
                    break;

                case IROp.Branch:
                    ilg.Emit(OpCodes.Br, ilLabels[inst.Operand]);
                    break;

                case IROp.BranchTrue:
                    ilg.Emit(OpCodes.Brtrue, ilLabels[inst.Operand]);
                    break;

                case IROp.BranchFalse:
                    ilg.Emit(OpCodes.Brfalse, ilLabels[inst.Operand]);
                    break;

                case IROp.Label:
                    ilg.MarkLabel(ilLabels[inst.Operand]);
                    break;

                case IROp.BeginTry:
                    ilg.BeginExceptionBlock();
                    break;

                case IROp.BeginCatch:
                    ilg.BeginCatchBlock((Type)ir.Operands[inst.Operand]);
                    break;

                case IROp.BeginFinally:
                    ilg.BeginFinallyBlock();
                    break;

                case IROp.EndTryCatch:
                    ilg.EndExceptionBlock();
                    break;

                case IROp.Ret:
                    ilg.Emit(OpCodes.Ret);
                    break;

                case IROp.Box:
                    ilg.Emit(OpCodes.Box, (Type)ir.Operands[inst.Operand]);
                    break;

                case IROp.Dup:
                    ilg.Emit(OpCodes.Dup);
                    break;

                case IROp.Pop:
                    ilg.Emit(OpCodes.Pop);
                    break;

                // ... remaining operations follow the same pattern
            }
        }
    }

    private static void EmitLoadLocal(ILGenerator ilg, int index)
    {
        switch (index)
        {
            case 0: ilg.Emit(OpCodes.Ldloc_0); break;
            case 1: ilg.Emit(OpCodes.Ldloc_1); break;
            case 2: ilg.Emit(OpCodes.Ldloc_2); break;
            case 3: ilg.Emit(OpCodes.Ldloc_3); break;
            default:
                if (index <= 255)
                    ilg.Emit(OpCodes.Ldloc_S, (byte)index);
                else
                    ilg.Emit(OpCodes.Ldloc, index);
                break;
        }
    }

    private static void EmitStoreLocal(ILGenerator ilg, int index)
    {
        switch (index)
        {
            case 0: ilg.Emit(OpCodes.Stloc_0); break;
            case 1: ilg.Emit(OpCodes.Stloc_1); break;
            case 2: ilg.Emit(OpCodes.Stloc_2); break;
            case 3: ilg.Emit(OpCodes.Stloc_3); break;
            default:
                if (index <= 255)
                    ilg.Emit(OpCodes.Stloc_S, (byte)index);
                else
                    ilg.Emit(OpCodes.Stloc, index);
                break;
        }
    }

    private static void EmitLoadArg(ILGenerator ilg, int index)
    {
        switch (index)
        {
            case 0: ilg.Emit(OpCodes.Ldarg_0); break;
            case 1: ilg.Emit(OpCodes.Ldarg_1); break;
            case 2: ilg.Emit(OpCodes.Ldarg_2); break;
            case 3: ilg.Emit(OpCodes.Ldarg_3); break;
            default:
                if (index <= 255)
                    ilg.Emit(OpCodes.Ldarg_S, (byte)index);
                else
                    ilg.Emit(OpCodes.Ldarg, index);
                break;
        }
    }

    private static void EmitLoadConstant(ILGenerator ilg, object value)
    {
        switch (value)
        {
            case int i:
                ilg.Emit(OpCodes.Ldc_I4, i);
                break;
            case long l:
                ilg.Emit(OpCodes.Ldc_I8, l);
                break;
            case float f:
                ilg.Emit(OpCodes.Ldc_R4, f);
                break;
            case double d:
                ilg.Emit(OpCodes.Ldc_R8, d);
                break;
            case string s:
                ilg.Emit(OpCodes.Ldstr, s);
                break;
            case bool b:
                ilg.Emit(b ? OpCodes.Ldc_I4_1 : OpCodes.Ldc_I4_0);
                break;
            // For reference types, store in closure and emit field load
            default:
                // Handle via closure constants array
                break;
        }
    }
}
```

### 9.9 Main Entry Point

```csharp
/// <summary>
/// Hyperbee Expression Compiler -- drop-in replacement for Expression.Compile().
/// </summary>
public static class HyperbeeCompiler
{
    /// <summary>
    /// Compiles a lambda expression into a delegate using the IR-based compiler.
    /// </summary>
    public static TDelegate Compile<TDelegate>(Expression<TDelegate> lambda)
        where TDelegate : Delegate
    {
        return (TDelegate)Compile((LambdaExpression)lambda);
    }

    /// <summary>
    /// Compiles a lambda expression into a delegate.
    /// </summary>
    public static Delegate Compile(LambdaExpression lambda)
    {
        // Phase 1: Lower expression tree to IR (single tree walk)
        var ir = new IRBuilder();
        var lowerer = new ExpressionLowerer(ir);
        lowerer.Lower(lambda);

        // Phase 2: Stack spilling (linear scan, zero tree allocations)
        StackSpillPass.Run(ir);

        // Phase 3: Closure analysis (linear scan)
        ClosureAnalysisPass.Run(ir, lambda);

        // Phase 4: Peephole optimization (linear scan, optional)
        PeepholePass.Run(ir);

        // Phase 5: Create DynamicMethod (type-associated, not anonymous)
        Type[] paramTypes = BuildParameterTypes(lambda, ir.ClosureStrategy);
        var method = new DynamicMethod(
            string.Empty,
            lambda.ReturnType,
            paramTypes,
            typeof(HyperbeeCompiler),  // associate with our type, not anonymous
            skipVisibility: true);

        // Phase 6: Emit IL from IR (linear scan, 1:1 mapping)
        ILEmissionPass.Run(ir, method.GetILGenerator());

        // Phase 7: Create delegate
        object? closureInstance = BuildClosure(ir);
        return method.CreateDelegate(lambda.Type, closureInstance);
    }

    private static Type[] BuildParameterTypes(
        LambdaExpression lambda, ClosureStrategy strategy)
    {
        int offset = (strategy != ClosureStrategy.None) ? 1 : 0;
        var types = new Type[lambda.Parameters.Count + offset];

        if (offset > 0)
        {
            types[0] = strategy switch
            {
                ClosureStrategy.ArrayClosure => typeof(ArrayClosure),
                ClosureStrategy.TypedClosure => typeof(object), // typed closure base
                ClosureStrategy.ConstantsOnly => typeof(object[]),
                _ => typeof(object)
            };
        }

        for (int i = 0; i < lambda.Parameters.Count; i++)
        {
            var p = lambda.Parameters[i];
            types[i + offset] = p.IsByRef ? p.Type.MakeByRefType() : p.Type;
        }

        return types;
    }

    private static object? BuildClosure(IRBuilder ir)
    {
        return ir.ClosureStrategy switch
        {
            ClosureStrategy.None => null,
            ClosureStrategy.ArrayClosure => new ArrayClosure(/* constants */),
            ClosureStrategy.ConstantsOnly => ir.GetConstants(),
            _ => null // TODO: typed closures
        };
    }
}

/// <summary>
/// Extension methods for convenient usage.
/// </summary>
public static class HyperbeeCompilerExtensions
{
    public static TDelegate CompileHyperbee<TDelegate>(
        this Expression<TDelegate> expression)
        where TDelegate : Delegate
    {
        return HyperbeeCompiler.Compile(expression);
    }
}
```

---

## 10. Implementation Plan

### Phase 0: Project Setup and Test Infrastructure

**Goal:** Establish the project structure, build system, benchmark harness, and
test infrastructure before writing compiler code.

1. Create solution: `Hyperbee.ExpressionCompiler.sln`
   - `src/Hyperbee.ExpressionCompiler/` -- main library (targets net8.0+)
   - `test/Hyperbee.ExpressionCompiler.Tests/` -- MSTest test project
   - `test/Hyperbee.ExpressionCompiler.IssueTests/` -- FEC failure regression tests
   - `test/Hyperbee.ExpressionCompiler.Benchmarks/` -- BenchmarkDotNet project
2. Set up `CompilerType` enum (`System`, `Fast`, `Interpret`, `Hyperbee`) and
   `ExpressionCompilerExtensions` dispatch -- see
   [Section 14: Testing and Validation Strategy](#14-testing-and-validation-strategy)
   for the full test infrastructure design
3. Port the first batch of tests from dotnet/runtime's
   `System.Linq.Expressions/tests/` (MIT licensed) -- start with BinaryOperators,
   Unary, Constants, Parameters, Conditional; adapt xUnit `[Theory]` to MSTest
   `[DataRow]` using the porting guide in Section 14.9
4. Seed `IssueTests` project with the highest-impact FEC issue regression cases
   (see Section 14.7) -- these are the patterns Hyperbee must handle correctly
   where FEC fails
5. Set up BenchmarkDotNet project with compilation-speed, execution-speed, and
   allocation benchmarks for all three compilers across all expression tiers
6. Set up CI to run tests and benchmarks on every commit;
   establish Phase 0 benchmark baselines for System and FEC
7. Implement `HyperbeeCompiler.TryCompile()` returning `null` (stub) and
   `CompileWithFallback()` that falls back to `Expression.Compile()`

**Expected outcome:** Full test and benchmark infrastructure running. Hyperbee
stubs return null/fallback. Baselines established for System and FEC.

### Phase 1: Foundation (MVP)

**Goal:** Compile simple expression trees (no closures, no try/catch) correctly.

1. Define `IROp` enum, `IRInstruction` struct, `IRBuilder` class
2. Implement `ExpressionLowerer` for the most common expression types:
   - Constants, Parameters, Binary ops, Unary ops
   - Method calls (static and instance)
   - Conditionals (ternary)
   - Type conversions (Convert, TypeAs)
   - Member access (field and property)
   - New object creation
   - Block expressions
3. Implement `ILEmissionPass` -- direct IR to IL mapping
4. Implement `HyperbeeCompiler.Compile()`, `TryCompile()`, and
   `CompileWithFallback()` entry points
5. Optional: implement `IRValidator` pass for debug/development builds
6. Run benchmarks -- compare compilation speed to system compiler and FEC
7. Run ported correctness tests -- all three compilers should pass

**Expected outcome:** Working compiler for simple cases, 5-20x faster than system.
All Phase 0 tests pass for Hyperbee on supported expression types.

### Phase 2: Exception Handling

**Goal:** Support try/catch/finally with correct stack spilling.

1. Extend `ExpressionLowerer` with try/catch/finally/fault lowering
2. Implement `StackSpillPass` -- linear scan stack analysis and spill insertion
3. Add Throw/Rethrow support
4. Port exception handling tests from dotnet/runtime
5. Test with expressions containing try/catch inside method call arguments
6. Add FEC failure-case tests (from FEC GitHub issues) to verify Hyperbee handles
   patterns that FEC cannot
7. Run differential tests: System vs Hyperbee on exception-handling expressions

**Expected outcome:** Correct exception handling with near-zero spilling overhead.

### Phase 3: Closures

**Goal:** Support captured variables and nested lambdas.

1. Implement `ClosureAnalysisPass` -- captured variable detection
2. Design closure type hierarchy (ArrayClosure, typed closures)
3. Implement closure creation and variable access in IL emission
4. Handle nested lambda compilation (recursive compilation)
5. Handle mutable captured variables correctly
6. Port closure/variable tests from dotnet/runtime
7. Test with complex closure patterns that FEC fails on (from FEC issues)
8. Run differential tests comparing all three compilers on closure scenarios

**Expected outcome:** Full closure support matching system compiler correctness.

### Phase 4: Completeness

**Goal:** Handle all remaining expression types.

1. Loop expressions (Loop, Break, Continue)
2. Switch expressions
3. Goto/Label
4. Dynamic expressions
5. Index expressions
6. ListInit and MemberInit expressions
7. RuntimeVariables
8. Quote expressions
9. Extension expressions (via Reduce())
10. DebugInfo expressions
11. Port remaining test categories from dotnet/runtime for each expression type

**Expected outcome:** Drop-in replacement for `Expression.Compile()`.

### Phase 5: Optimization

**Goal:** Maximize compilation speed and delegate quality.

1. Implement `PeepholePass` with common optimizations
2. Optimize closure strategy (typed closures for small capture sets)
3. Profile and eliminate remaining allocation hotspots
4. Consider pooling IRBuilder internals for repeated compilation
5. Add short-form opcode selection throughout IL emission
6. Run full benchmark suite -- target within 2x of FEC compilation speed
7. Run allocation benchmarks -- target 80%+ reduction vs system compiler

**Expected outcome:** Compilation speed within 2x of FEC, correctness matching system.

### Phase 6: CompileToMethod Support

**Goal:** Support compilation to MethodBuilder for persistence and AOT scenarios.

1. Implement `HyperbeeCompiler.CompileToMethod(LambdaExpression, MethodBuilder)`
2. Implement `TryCompileToMethod()` returning bool
3. Implement constant handling strategies:
   - Strategy A: reject non-embeddable constants for persisted assemblies
   - Strategy B: lift to static fields for in-memory assemblies
4. Implement `CompileToType()` convenience method with optional save path
5. Add IL verification tests using ILVerify on persisted output
6. Add round-trip tests: compile → save → load → execute → compare
7. Test with `PersistedAssemblyBuilder` (.NET 9+)

**Expected outcome:** Working CompileToMethod with save-to-disk support.

### Phase 7: Production Hardening

**Goal:** Production-ready library.

1. Run full differential test suite: System vs FEC vs Hyperbee across all
   expression categories
2. Implement random expression tree fuzzing (see Appendix E)
3. Thread safety validation
4. Error handling and diagnostics (clear error messages for unsupported patterns)
5. NuGet package creation (Hyperbee.ExpressionCompiler)
6. Documentation and API reference
7. Integration tests with real-world libraries (AutoMapper, Mapster, etc.)
8. Performance regression CI gate (benchmark must not regress beyond threshold)

---

## 11. Estimated Performance Impact

### Compilation Speed Estimates

Based on the analysis of where time is spent in the system compiler:

| Optimization | Estimated Speedup | Cumulative |
|---|---|---|
| Single tree walk instead of 3 | ~2-3x | 2-3x |
| Eliminate StackSpiller tree copying | ~3-5x | 6-15x |
| Flat IR passes vs tree recursion | ~1.5-2x | 9-30x |
| Type-associated DynamicMethod | ~1.2-1.5x | 11-45x |
| Reduced allocation / GC pressure | ~1.3-2x | 14-90x |
| Simpler closure strategy | ~1.1-1.3x | 15-100x |

**Conservative target: 10-20x faster than system compiler.**
**Optimistic target: 20-40x faster (approaching FEC).**

### Allocation Estimates

For a 100-node expression tree with a try/catch block:

| Metric | System Compiler | Hyperbee (estimated) |
|---|---|---|
| Expression tree traversals | 3 | 1 |
| New expression tree nodes | 60-100 | 0 |
| ReadOnlyCollection allocations | 20-40 | 0 |
| IR instruction storage | N/A | ~150 structs in a List (1 alloc) |
| Temp variable allocations | ParameterExpression objects | int indices (0 allocs) |
| Total heap allocations | ~100-200 | ~10-20 |

### Delegate Execution Speed

The compiled delegates should perform comparably to both the system compiler
and FEC delegates. The IR-based approach allows for small improvements:

- Closure access: ArrayClosure with flat array vs StrongBox<T> chain
  (saves 1-2 instructions per captured variable access)
- Short-form opcodes consistently used
- Potential for peephole optimizations reducing redundant load/stores

**Estimated: comparable to FEC (~7-10ns), slightly faster than system (~11ns).**

---

## 12. Risk Analysis

### Technical Risks

| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| Missing expression type support | High (initially) | Medium | Phased implementation; fallback to system compiler |
| Incorrect IL generation | Medium | High | Comprehensive test suite; PEVerify validation |
| Closure scoping bugs | Medium | High | Port system compiler's test cases; test FEC failure patterns |
| Performance not meeting targets | Low | Medium | Profile early and often; the architecture is sound |
| DynamicMethod limitations | Low | Low | Same API as FEC uses successfully |

### Architectural Risks

| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| IR design too low/high level | Medium | Medium | Start with stack-based IR close to CIL; refine based on experience |
| Pass ordering dependencies | Low | Medium | Document pass contracts clearly |
| Nested lambda compilation complexity | Medium | High | Start simple; handle recursive compilation carefully |

### Practical Risks

| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| Scope creep | High | Medium | Strict phased approach; MVP first |
| .NET version compatibility | Medium | Low | Target .NET 8+ (LTS); use only public APIs |
| Maintenance burden | Medium | Medium | Clean architecture makes passes independently testable |

---

## 13. References and Source Locations

### System Expression Compiler Source (dotnet/runtime)

All paths relative to `dotnet/runtime/src/libraries/System.Linq.Expressions/src/System/Linq/Expressions/`:

| File | Description |
|---|---|
| `Compiler/LambdaCompiler.cs` | Main compiler, DynamicMethod creation, entry point |
| `Compiler/LambdaCompiler.Lambda.cs` | Nested lambda compilation, closure creation |
| `Compiler/LambdaCompiler.Expressions.cs` | Expression-specific IL emission |
| `Compiler/CompilerScope.cs` | Closure/scope management, StrongBox<T> handling |
| `Compiler/StackSpiller.cs` | Stack spilling tree rewriter (~1000 lines) |
| `Compiler/StackSpiller.Generated.cs` | Expression type switch dispatcher |
| `Compiler/StackSpiller.Temps.cs` | TempMaker, temporary variable management |
| `Compiler/StackSpiller.ChildRewriter.cs` | Child expression rewriting, main alloc source |
| `Compiler/VariableBinder.cs` | Variable binding and scope analysis |

GitHub base URL:
`https://github.com/dotnet/runtime/tree/main/src/libraries/System.Linq.Expressions/src/System/Linq/Expressions/Compiler/`

### FastExpressionCompiler Source

| File | Description |
|---|---|
| `src/FastExpressionCompiler/FastExpressionCompiler.cs` | Entire compiler (single file) |
| `src/FastExpressionCompiler.LightExpression/` | Lightweight expression tree API |

GitHub: `https://github.com/dadhi/FastExpressionCompiler`

### Key .NET APIs

| API | Documentation |
|---|---|
| `DynamicMethod` constructors | https://learn.microsoft.com/en-us/dotnet/api/system.reflection.emit.dynamicmethod.-ctor |
| `ILGenerator` | https://learn.microsoft.com/en-us/dotnet/api/system.reflection.emit.ilgenerator |
| `UnsafeAccessorAttribute` | https://learn.microsoft.com/en-us/dotnet/api/system.runtime.compilerservices.unsafeaccessorattribute |
| CIL OpCodes | https://learn.microsoft.com/en-us/dotnet/api/system.reflection.emit.opcodes |

### Benchmarks and Discussion

| Resource | URL |
|---|---|
| FEC benchmarks | https://github.com/dadhi/FastExpressionCompiler#benchmarks |
| FEC issue #495 (failure case) | https://github.com/dadhi/FastExpressionCompiler/issues/495 |

---

## 14. Testing and Validation Strategy

### 14.1 Testing Philosophy

Correctness is the primary goal of Hyperbee.ExpressionCompiler. Three
complementary validation approaches ensure it:

- **Correctness testing** -- the same expression must produce the same result
  when compiled by System and Hyperbee compilers across all input values
- **Regression testing** -- expression patterns that cause FEC to fail silently
  or produce incorrect IL must succeed correctly in Hyperbee
- **Performance testing** -- compilation speed and heap allocation benchmarks
  run against all three compilers to verify the performance targets are met and
  guard against regressions

### 14.2 Reference Test Suites

Two open-source test suites serve as primary references. Both are MIT licensed
and can be adapted directly into the Hyperbee test projects.

#### System Expression Compiler Tests (dotnet/runtime)

```
dotnet/runtime/src/libraries/System.Linq.Expressions/tests/
```

GitHub: `https://github.com/dotnet/runtime/tree/main/src/libraries/System.Linq.Expressions/tests/`

**Scale and framework:** ~26,000 tests, xUnit with `[Theory, ClassData(typeof(CompilationTypes))]`.

**Parameterization:** A single `bool useInterpreter` parameter causes each test
to run twice -- once with compiled IL, once with the interpreter. This maps
directly to the `CompilerType` enum pattern used in this project.

**Organization:** One folder per expression type category:

```
tests/
  BinaryOperators/
    Arithmetic/     BinaryAddTests.cs, BinarySubtractTests.cs, ...
    Bitwise/        BinaryAndTests.cs, BinaryOrTests.cs, ...
    Comparison/     BinaryEqualTests.cs, BinaryLessThanTests.cs, ...
    Logical/        BinaryAndAlsoTests.cs, ...
  UnaryOperators/
  ConditionalExpression/
  ExceptionHandling/
  MemberAccess/
  IndexExpression/
  Cast/
  ...
```

**Key patterns in the dotnet/runtime suite:**

- *Verifier helper methods* -- each test delegates to a `Verify*` helper
  (e.g., `VerifyByteAdd(byte a, byte b, bool useInterpreter)`) so the test
  logic is written once and called with multiple data rows
- *Boundary value coverage* -- every numeric type test includes `MinValue`,
  `MaxValue`, `0`, `1`, `-1`, and `NaN`/`Infinity` for floats
- *Checked vs unchecked variants* -- arithmetic tests cover both
  `Expression.Add()` (unchecked) and `Expression.AddChecked()` (throws on
  overflow) separately
- *Error path tests* -- dedicated tests verify that invalid arguments
  (`null`, type mismatches) produce the correct exceptions at tree-construction
  time, not at compile time

**Porting strategy:** Adapt to MSTest `[DataRow]`, replace `bool useInterpreter`
with `CompilerType compiler`, and add `CompilerType.Hyperbee` as an additional
data row. See the porting guide in [Section 14.9](#149-test-porting-guide).

#### FastExpressionCompiler Tests (dadhi/FastExpressionCompiler)

```
dadhi/FastExpressionCompiler/test/
  FastExpressionCompiler.UnitTests/     -- standard expression type coverage
  FastExpressionCompiler.IssueTests/    -- one file per GitHub issue (bug regressions)
```

GitHub: `https://github.com/dadhi/FastExpressionCompiler/tree/master/test/`

**Framework:** NUnit (`[TestFixture]`, `[Test]` attributes).

**Key patterns:**

- *Null-return testing* -- `CompileFast(ifFastFailedReturnNull: true)` returns
  `null` for unsupported patterns; tests use `Assert.IsNull()` to verify that
  failure is detected. Hyperbee's `TryCompile()` parallels this.
- *Strict mode* -- `CompilerFlags.ThrowOnNotSupportedExpression` makes FEC
  throw instead of returning `null`; used to verify the unsupported-pattern
  contract explicitly
- *Dual assertions* -- many tests assert both "FEC cannot compile this" and
  "System compiler produces the correct result for this" in the same method,
  documenting the exact correctness gap
- *Issue-named files* -- `FecIssue495Tests.cs`, `FecIssue372Tests.cs`, etc.;
  each file is a standalone reproduction of a specific bug

**The `IssueTests/` project is the most important reference.** Every issue file
is a documented expression pattern that FEC fails on. These become the Hyperbee
regression suite: patterns Hyperbee *must* handle where FEC cannot.

### 14.3 Test Project Structure

```
test/
├── Hyperbee.ExpressionCompiler.Tests/          -- correctness tests (primary)
│   ├── Expressions/
│   │   ├── BinaryTests.cs                      -- ported from dotnet/runtime BinaryOperators/
│   │   ├── UnaryTests.cs
│   │   ├── ConstantTests.cs
│   │   ├── ParameterTests.cs
│   │   ├── ConditionalTests.cs
│   │   ├── BlockTests.cs
│   │   ├── MemberAccessTests.cs
│   │   ├── MethodCallTests.cs
│   │   ├── NewObjectTests.cs
│   │   ├── TypeConversionTests.cs
│   │   ├── AssignmentTests.cs
│   │   ├── ExceptionHandlingTests.cs           -- try/catch/finally/fault/filter
│   │   ├── ClosureTests.cs                     -- captured variables, nested lambdas
│   │   ├── LoopTests.cs                        -- Loop, Break, Continue, Goto
│   │   ├── SwitchTests.cs
│   │   ├── LambdaTests.cs                      -- nested lambda compilation
│   │   └── ExtensionExpressionTests.cs         -- reducible expressions
│   │
│   ├── IR/                                     -- IR pass unit tests (unique to Hyperbee)
│   │   ├── IRBuilderTests.cs
│   │   ├── ExpressionLowererTests.cs
│   │   ├── StackSpillPassTests.cs
│   │   ├── ClosureAnalysisPassTests.cs
│   │   └── PeepholePassTests.cs
│   │
│   ├── Compatibility/
│   │   ├── CompilerCompatibilityTests.cs       -- known differences between all 3 compilers
│   │   └── DifferentialTests.cs               -- automated result comparison across compilers
│   │
│   └── TestSupport/
│       ├── ExpressionCompilerExtensions.cs     -- CompilerType enum + Compile() dispatch
│       ├── ExpressionVerifier.cs               -- differential testing helpers
│       └── ExpressionGenerator.cs             -- random expression tree generator (fuzzing)
│
├── Hyperbee.ExpressionCompiler.IssueTests/     -- FEC failure regression suite
│   ├── FecIssue495Tests.cs                     -- incorrect delegate for compound assign in TryCatch
│   ├── FecIssue[N]Tests.cs                     -- one file per FEC issue Hyperbee must handle
│   └── SystemCompilerEdgeCaseTests.cs          -- rare-but-valid patterns from dotnet/runtime issues
│
└── Hyperbee.ExpressionCompiler.Benchmarks/     -- BenchmarkDotNet performance tests
    ├── CompilationBenchmarks.cs                -- time from LambdaExpression → Delegate
    ├── ExecutionBenchmarks.cs                  -- time to invoke compiled delegate
    ├── AllocationBenchmarks.cs                 -- [MemoryDiagnoser] heap allocation counts
    └── BenchmarkConfig.cs
```

### 14.4 CompilerType Enum and Dispatch

The `CompilerType` enum follows the pattern established in the existing
Hyperbee.Expressions test infrastructure. A fourth variant is added:

```csharp
public enum CompilerType
{
    Fast,       // FastExpressionCompiler.CompileFast()
    System,     // Expression.Compile()
    Interpret,  // Expression.Compile( preferInterpretation: true )
    Hyperbee    // HyperbeeCompiler.Compile()
}
```

`ExpressionCompilerExtensions.Compile()` dispatches based on the enum:

```csharp
public static TDelegate Compile<TDelegate>( this Expression<TDelegate> expression,
    CompilerType compilerType )
    where TDelegate : Delegate
{
    return compilerType switch
    {
        CompilerType.System    => expression.Compile(),
        CompilerType.Interpret => expression.Compile( preferInterpretation: true ),
        CompilerType.Hyperbee  => HyperbeeCompiler.Compile( expression ),
        CompilerType.Fast      => CompileFast( expression ),
        _ => throw new ArgumentOutOfRangeException( nameof(compilerType) )
    };
}
```

Standard test template:

```csharp
[TestClass]
public class BinaryTests
{
    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Add_Int32_ShouldReturnCorrectResult( CompilerType compiler )
    {
        var a = Expression.Parameter( typeof(int), "a" );
        var b = Expression.Parameter( typeof(int), "b" );
        var lambda = Expression.Lambda<Func<int, int, int>>( Expression.Add( a, b ), a, b );

        var fn = lambda.Compile( compiler );

        // Boundary values matching dotnet/runtime coverage
        Assert.AreEqual( 0, fn(0, 0) );
        Assert.AreEqual( int.MaxValue, fn(int.MaxValue, 0) );
        Assert.AreEqual( -2, fn(int.MaxValue, int.MinValue) ); // unchecked wraps
    }
}
```

### 14.5 IR Pass Unit Tests

Because the IR passes operate on a flat instruction list rather than an
expression tree, they can be tested in complete isolation -- no expression
tree construction needed, no DynamicMethod required. This is a major advantage
over the System compiler, where the passes (StackSpiller, VariableBinder,
LambdaCompiler) are tightly coupled and cannot be independently exercised.

```csharp
[TestClass]
public class StackSpillPassTests
{
    [TestMethod]
    public void Run_ShouldInsertSpillInstructions_WhenStackNonEmptyAtBeginTry()
    {
        // Arrange: a push before BeginTry -- stack not empty at try boundary
        var ir = new IRBuilder();
        ir.Emit( IROp.LoadConst, ir.AddOperand( 42 ) ); // +1 on stack
        ir.Emit( IROp.BeginTry );                        // stack must be 0 here
        ir.Emit( IROp.Nop );
        ir.Emit( IROp.EndTryCatch );
        ir.Emit( IROp.Ret );

        // Act
        bool modified = StackSpillPass.Run( ir );

        // Assert: spill inserted (StoreLocal before BeginTry, LoadLocal after EndTryCatch)
        Assert.IsTrue( modified );
        Assert.AreEqual( IROp.StoreLocal, ir.Instructions[1].Op );
        Assert.AreEqual( IROp.BeginTry,   ir.Instructions[2].Op );
    }

    [TestMethod]
    public void Run_ShouldReturnFalse_WhenNoTryBlocks()
    {
        // Arrange: no try blocks -- fast-exit path
        var ir = new IRBuilder();
        ir.Emit( IROp.LoadConst, ir.AddOperand( 1 ) );
        ir.Emit( IROp.LoadConst, ir.AddOperand( 2 ) );
        ir.Emit( IROp.Add );
        ir.Emit( IROp.Ret );

        // Act
        bool modified = StackSpillPass.Run( ir );

        // Assert: no changes, zero allocations beyond the fast-exit scan
        Assert.IsFalse( modified );
        Assert.AreEqual( 4, ir.Instructions.Count );
    }
}
```

Every pass has a corresponding test class. Pass tests run in milliseconds and
catch regressions without needing end-to-end compilation.

### 14.6 Differential Testing

The most powerful correctness guarantee is to compile the same expression with
the System compiler and Hyperbee, invoke both with the same inputs, and assert
the outputs match. An `ExpressionVerifier` helper automates this:

```csharp
public static class ExpressionVerifier
{
    /// <summary>
    /// Compiles the expression with both System and Hyperbee compilers,
    /// invokes each with the provided input sets, and asserts results match.
    /// </summary>
    public static void Verify<TDelegate>(
        Expression<TDelegate> lambda,
        params object[][] inputs )
        where TDelegate : Delegate
    {
        var system   = lambda.Compile();
        var hyperbee = HyperbeeCompiler.Compile( lambda );

        foreach ( var args in inputs )
        {
            var expected = system.DynamicInvoke( args );
            var actual   = hyperbee.DynamicInvoke( args );
            Assert.AreEqual( expected, actual,
                $"Mismatch for input ({string.Join( ", ", args )}): " +
                $"System={expected}, Hyperbee={actual}" );
        }
    }
}
```

Usage:

```csharp
ExpressionVerifier.Verify(
    (Expression<Func<int, int, int>>)( (a, b) => a + b ),
    new object[] { 1, 2 },
    new object[] { int.MaxValue, 0 },
    new object[] { -1, -1 }
);
```

Differential tests are especially important for closure expressions and
exception handling, where subtle IL differences can produce correct results on
some inputs but not others.

### 14.7 FEC Failure Regression Suite

FEC's `IssueTests/` project is the most valuable external reference for
Hyperbee correctness. Each issue file documents an expression pattern where:

- **FEC returns `null`** (detected as unsupported -- good), and Hyperbee must
  succeed, or
- **FEC returns an incorrect delegate** (not detected -- silent corruption),
  and Hyperbee must return a correct delegate

The `Hyperbee.ExpressionCompiler.IssueTests/` project mirrors this structure.
One file per issue:

```csharp
[TestClass]
public class FecIssue495Tests
{
    // FEC #495: compound assignment (Assign) inside TryCatch produces
    // incorrect IL rather than returning null. System compiler is correct.
    // Source: https://github.com/dadhi/FastExpressionCompiler/issues/495

    [TestMethod]
    public void Issue495_HyperbeeCompiler_ShouldProduceCorrectResult()
    {
        var result = Expression.Variable( typeof(int), "result" );
        var lambda = Expression.Lambda<Func<int>>(
            Expression.Block(
                new[] { result },
                Expression.TryCatch(
                    Expression.Assign( result, Expression.Constant( 42 ) ),
                    Expression.Catch( typeof(Exception), Expression.Constant( 0 ) )
                ),
                result
            ) );

        // FEC produces invalid IL here; Hyperbee must be correct
        Assert.AreEqual( 42, HyperbeeCompiler.Compile<Func<int>>( lambda )() );
    }

    [TestMethod]
    public void Issue495_TryCompile_ShouldNotReturnNull()
    {
        // Verify Hyperbee can compile what FEC cannot, without fallback
        var result = Expression.Variable( typeof(int), "result" );
        var lambda = Expression.Lambda<Func<int>>(
            Expression.Block(
                new[] { result },
                Expression.TryCatch(
                    Expression.Assign( result, Expression.Constant( 42 ) ),
                    Expression.Catch( typeof(Exception), Expression.Constant( 0 ) )
                ),
                result
            ) );

        var compiled = HyperbeeCompiler.TryCompile( lambda );

        Assert.IsNotNull( compiled, "Hyperbee should compile this pattern correctly." );
    }
}
```

Seeding priority (highest impact first):

1. FEC issues involving `TryCatch` with `Assign` (e.g., #495) -- FEC silent
   corruption; most dangerous failure mode
2. FEC issues involving nested lambdas with captured mutable variables
3. FEC issues involving `Return` goto from inside try blocks
4. FEC issues involving by-ref arguments with stack spilling
5. FEC issues that FEC correctly returns `null` for -- verify Hyperbee succeeds

### 14.8 Phased Test Rollout

Test phases align with implementation phases. Tests for a given expression
feature are added in the same phase that implements the feature.

| Phase | Test Focus | Primary Reference Source |
|---|---|---|
| 0 | Test infrastructure: enum, extensions, differential verifier, benchmark baseline | N/A |
| 1 | Binary, Unary, Constant, Parameter, Conditional, Block, MemberAccess, MethodCall, NewObject, TypeConversion | dotnet/runtime `BinaryOperators/`, `Unary/`, `ConditionalExpression/`, `MemberAccess/` |
| 2 | ExceptionHandling: try/catch/finally/fault/filter, Throw, Rethrow | dotnet/runtime `ExceptionHandling/` + FEC issues #495, and TryCatch issue filings |
| 3 | Closures: captured variables (immutable and mutable), nested lambdas, multi-level captures | dotnet/runtime closure tests + FEC nested-lambda and closure issue filings |
| 4 | Loop, Goto, Switch, ListInit, MemberInit, RuntimeVariables, Quote, DebugInfo | dotnet/runtime remaining test folders |
| 5 | Performance regression CI gate; allocation budgets; peephole correctness | BenchmarkDotNet baselines from Phase 0 |
| 6 | CompileToMethod: MethodBuilder emission, ILVerify round-trips, PersistedAssemblyBuilder save/load | dotnet/runtime CompileToMethod tests (.NET Framework tests, adapted) |
| 7 | Random expression tree fuzzing, thread safety, integration tests with real libraries | N/A (new infrastructure) |

### 14.9 Test Porting Guide

The dotnet/runtime tests use xUnit; the Hyperbee project uses MSTest. Porting
is mechanical:

| dotnet/runtime (xUnit) | Hyperbee (MSTest) |
|---|---|
| `[Fact]` | `[TestMethod]` |
| `[Theory, ClassData(typeof(CompilationTypes))]` | `[DataRow( CompilerType.System )]` + `[DataRow( CompilerType.Hyperbee )]` |
| `bool useInterpreter` parameter | `CompilerType compiler` parameter |
| `lambda.Compile( useInterpreter )` | `lambda.Compile( compiler )` via extension method |
| `Assert.Equal( expected, actual )` | `Assert.AreEqual( expected, actual )` |
| `Assert.Throws<T>( () => ... )` | `Assert.ThrowsException<T>( () => ... )` |

**What to port (and what to skip):** Not all 26,000 dotnet/runtime tests need
to be imported. Focus on:

1. One test per expression type per supported operation (breadth first)
2. Boundary value tests for every numeric type tested (coverage depth)
3. Exception-path tests validating argument validation at tree-build time
4. Any test that explicitly documents a known compiler behavior (these act as
   a verified reference for differential testing)

Skip tests for platform-specific behavior, COM interop, and .NET Framework
legacy scenarios not relevant to .NET 8+.

The FEC `UnitTests/` project is also a useful porting source and is often
more concise than the dotnet/runtime tests. Prefer it for quick coverage of
a new expression type before going deeper with dotnet/runtime's exhaustive
boundary-value tests.

### 14.10 Benchmark Design

Three benchmark classes, each targeting a different question:

#### Compilation Speed (primary metric)

Time from `LambdaExpression` to a callable `Delegate`. Four expression tiers
exercise different compiler paths:

```csharp
[MemoryDiagnoser]
[SimpleJob( RuntimeMoniker.Net90 )]
public class CompilationBenchmarks
{
    // Tier 1: Simple -- constants, binary ops, method calls. No closures.
    [Benchmark] public Delegate Simple_System()   => _simple.Compile();
    [Benchmark] public Delegate Simple_Fec()      => _simple.CompileFast();
    [Benchmark] public Delegate Simple_Hyperbee() => HyperbeeCompiler.Compile( _simple );

    // Tier 2: Closure -- one or more captured variables.
    [Benchmark] public Delegate Closure_System()   => _closure.Compile();
    [Benchmark] public Delegate Closure_Fec()      => _closure.CompileFast();
    [Benchmark] public Delegate Closure_Hyperbee() => HyperbeeCompiler.Compile( _closure );

    // Tier 3: Exception handling -- try/catch/finally with stack spilling.
    [Benchmark] public Delegate TryCatch_System()   => _tryCatch.Compile();
    [Benchmark] public Delegate TryCatch_Fec()      => _tryCatch.CompileFast();
    [Benchmark] public Delegate TryCatch_Hyperbee() => HyperbeeCompiler.Compile( _tryCatch );

    // Tier 4: Complex -- realistic ORM/IoC workload (closures + conditionals + casts).
    [Benchmark] public Delegate Complex_System()   => _complex.Compile();
    [Benchmark] public Delegate Complex_Fec()      => _complex.CompileFast();
    [Benchmark] public Delegate Complex_Hyperbee() => HyperbeeCompiler.Compile( _complex );
}
```

#### Execution Speed (secondary metric)

Delegates produced by all three compilers should execute at equivalent speed.
Any regression vs. System compiler is a bug.

```csharp
public class ExecutionBenchmarks
{
    private Func<int, int, int> _systemFn, _fecFn, _hyperbeeFn;

    [GlobalSetup]
    public void Setup()
    {
        _systemFn   = _simple.Compile();
        _fecFn      = _simple.CompileFast();
        _hyperbeeFn = HyperbeeCompiler.Compile( _simple );
    }

    [Benchmark( Baseline = true )]
    public int Execute_System()   => _systemFn( 1, 2 );

    [Benchmark]
    public int Execute_Fec()      => _fecFn( 1, 2 );

    [Benchmark]
    public int Execute_Hyperbee() => _hyperbeeFn( 1, 2 );
}
```

#### Allocation Count (regression guard)

`[MemoryDiagnoser]` reports Gen0 collections, Gen1 collections, and total bytes
allocated per operation. The target is an 80%+ reduction in allocated bytes
versus the System compiler for the same expression tier.

CI baseline: store BenchmarkDotNet JSON output artifacts from Phase 0. Any
subsequent run that increases allocated bytes by more than 10% fails the build.

---

## Appendix A: StackSpiller Deep Dive

### What It Does

The StackSpiller ensures CLR evaluation stack is empty at try/catch/loop/goto
boundaries. This is a requirement of CIL verification -- you cannot enter a
`try` block with values on the evaluation stack.

### The Immutability Problem

Expression tree nodes are immutable. When a single node deep in the tree needs
spilling, the `RewriteAction.Copy` propagates to every ancestor:

```
Root (must Copy because child changed)
  └── BinaryExpression (must Copy because child changed)
        ├── Left: MethodCall (unchanged)
        └── Right: ConditionalExpression (must Copy because child changed)
              ├── Test: unchanged
              ├── IfTrue: TryExpression ← SPILL HERE
              └── IfFalse: unchanged
```

Every "must Copy" node allocates: new expression node + new ReadOnlyCollection +
new backing array. For a tree 10 levels deep, that is 10 x 3 = 30 allocations
just from Copy propagation for a single spill point.

### The No-Op Traversal Problem

For expression trees WITHOUT try/catch (the most common case), the StackSpiller:
1. Visits every single node via the RewriteExpression switch statement
2. Each visit involves a virtual method call and stack frame
3. For each node, creates a Result struct (stack allocation, but still work)
4. Returns `RewriteAction.None` at every level
5. The final result is the original, unmodified expression tree

This is pure overhead -- the tree is untouched, but we paid for a full traversal.

### Key Source Code Sections

**Entry point** (StackSpiller.cs:90-93):
```csharp
internal static LambdaExpression AnalyzeLambda(LambdaExpression lambda)
{
    return lambda.Accept(new StackSpiller(Stack.Empty));
}
```

**The Rewrite method** (StackSpiller.cs:101-126) -- if no changes needed, returns
original lambda. If any changes, creates a new lambda with a Block wrapper for
the temporary variables.

**ChildRewriter** -- creates arrays for child expressions and List<Expression>
for the "comma" block when spilling. Each ChildRewriter instance in a spill
scenario allocates these collections.

**TempMaker** -- manages a pool of ParameterExpression temporaries with a
watermark-based free list. Reasonably efficient but still allocates
ParameterExpression objects that are only used during this single pass.

---

## Appendix B: DynamicMethod Constructor Differences

### System Compiler Uses: Anonymously Hosted

```csharp
new DynamicMethod(name, returnType, parameterTypes, true)
// DynamicMethod(String, Type, Type[], Boolean)
```

From Microsoft documentation:
> "The dynamic method created by this constructor is associated with an anonymous
> assembly instead of an existing type or module. The anonymous assembly exists only
> to provide a sandbox environment for dynamic methods."

### FEC Uses: Type-Associated

```csharp
new DynamicMethod("", returnType, closureAndParamTypes, typeof(ArrayClosure), true)
// DynamicMethod(String, Type, Type[], Type, Boolean)
```

From Microsoft documentation:
> "The dynamic method has access to all members of the type [owner]. This gives
> it access to all members, public and private."

### Why It Matters

The type-associated overload:
- Avoids the anonymous assembly infrastructure
- Associates the method with an existing module (no cross-assembly resolution needed)
- Allows access to all members of the associated type (no additional permission checks)

The anonymous hosting overload was designed for .NET Framework's Code Access Security
(CAS) model, which is not present in .NET Core/5+. The sandbox overhead remains as
vestigial cost with no security benefit.

### Recommendation for Hyperbee

Use the type-associated overload, associating with the compiler's own type:

```csharp
new DynamicMethod("", returnType, paramTypes, typeof(HyperbeeCompiler), true)
```

---

## Appendix C: Closure Strategy Comparison

### System Compiler: StrongBox<T> Chain

```
Closure
  ├── Constants: object[] { constA, constB, nestedDelegate1 }
  └── Locals: object[] {
        StrongBox<int> { Value = capturedInt },
        StrongBox<string> { Value = capturedString },
        object[] {  // parent scope hoisted locals
            StrongBox<double> { Value = outerCapturedDouble }
        }
      }
```

**IL to access capturedInt:**
```
ldarg.0                          // load Closure
ldfld    Closure::Locals         // load object[] array
ldc.i4.0                        // index 0
ldelem.ref                       // load element (object)
castclass StrongBox<int>         // cast to StrongBox<int>
ldfld    StrongBox<int>::Value   // finally load the int value
```
**6 instructions, 1 type cast.**

### FEC: ArrayClosure

```
ArrayClosure
  └── ConstantsAndNestedLambdas: object[] {
        constA,
        constB,
        capturedInt (boxed),
        capturedString,
        nestedDelegate1
      }
```

**IL to access capturedInt:**
```
ldarg.0                                      // load ArrayClosure
ldfld    ArrayClosure::ConstantsAndNestedLambdas  // load object[] array
ldc.i4.2                                     // index 2
ldelem.ref                                   // load element (object)
unbox.any int32                              // unbox to int
```
**5 instructions, 1 unbox.** No StrongBox indirection.

### Proposed Hyperbee: Typed Closures (for small capture sets)

```csharp
// Pre-defined typed closure for 2 captured values
internal sealed class Closure<T1, T2>
{
    public T1 V1;
    public T2 V2;
}

// Instance: Closure<int, string> { V1 = capturedInt, V2 = capturedString }
```

**IL to access capturedInt:**
```
ldarg.0                                   // load Closure<int, string>
ldfld    Closure<int,string>::V1          // load the int directly
```
**2 instructions, 0 casts, 0 boxing.** This is the optimal representation for
small capture sets.

For larger capture sets or mutable variables, fall back to ArrayClosure.

---

## Appendix D: CompileToMethod History in .NET

### Timeline

| Version | CompileToMethod Status | AssemblyBuilder.Save Status |
|---|---|---|
| .NET Framework 4.0-4.8.1 | Available | Available (native implementation) |
| .NET Core 1.0-3.1 | **Removed** | **Removed** |
| .NET 5-8 | Removed | Removed |
| .NET 9 | Removed | **Restored** (`PersistedAssemblyBuilder`) |
| .NET 10 | Removed | Available |

### API in .NET Framework

```csharp
// .NET Framework 4.x
public class LambdaExpression
{
    public void CompileToMethod(MethodBuilder method);
    public void CompileToMethod(MethodBuilder method, DebugInfoGenerator debugInfoGenerator);
}
```

Constraints:
- The `MethodBuilder` must be a **static** method
- The `MethodBuilder`'s `DeclaringType` must be a `TypeBuilder`
- Non-embeddable constants (object references) caused exceptions

### Why Removed

The `FEATURE_COMPILE_TO_METHODBUILDER` conditional compilation flag is **defined nowhere**
in .NET Core/modern .NET builds. The code still exists in the source but is compiled out.

**Primary reason** (Jan Kotas, dotnet/runtime):
> "It was excluded because of layering. Reflection.Emit is not in .NET Standard, so public
> methods that take Reflection.Emit types like MethodBuilder cannot be in .NET Standard either."

**Secondary reasons:**
- `AssemblyBuilder.Save()` was not available in .NET Core (Windows-specific native code)
- Architectural concern: CompileToMethod couples compiler runtime to target runtime

### GitHub Issues Requesting Restoration

| Issue | Year | Status |
|---|---|---|
| [dotnet/runtime#19943](https://github.com/dotnet/runtime/issues/19943) | 2017 | Closed (explanation only) |
| [dotnet/runtime#20270](https://github.com/dotnet/runtime/issues/20270) | 2017 | Closed (2020, no resolution) |
| [dotnet/runtime#22025](https://github.com/dotnet/runtime/issues/22025) | 2017 | Open (no team action) |
| [dotnet/runtime#88555](https://github.com/dotnet/runtime/discussions/88555) | 2023 | Unanswered |
| [dotnet/runtime#113583](https://github.com/dotnet/runtime/issues/113583) | 2025 | Open (api-suggestion) |

### PersistedAssemblyBuilder (.NET 9+)

.NET 9 introduced `PersistedAssemblyBuilder`, a fully managed `Reflection.Emit`
implementation supporting `Save()`. This removes the primary blocker that made
CompileToMethod less useful in .NET Core.

```csharp
var ab = new PersistedAssemblyBuilder(new AssemblyName("MyAssembly"), typeof(object).Assembly);
ModuleBuilder mob = ab.DefineDynamicModule("Module");
TypeBuilder tb = mob.DefineType("MyType", TypeAttributes.Public);
MethodBuilder mb = tb.DefineMethod("Execute", MethodAttributes.Public | MethodAttributes.Static,
    typeof(int), new[] { typeof(int) });

// Emit IL into mb.GetILGenerator()...

tb.CreateType();
ab.Save("MyAssembly.dll");  // Now possible in .NET 9+
```

---

## Appendix E: Benchmarking and Testing Strategy

### Benchmark Design

Benchmarks must compare all three compilers (System, FEC, Hyperbee) across multiple
dimensions using [BenchmarkDotNet](https://github.com/dotnet/BenchmarkDotNet).

#### Benchmark Categories

**1. Compilation Speed** (the primary metric)

Measures the time to go from `LambdaExpression` to a callable `Delegate`:

```csharp
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net90)]
public class CompilationBenchmarks
{
    private Expression<Func<int, int, int>> _simpleExpr;
    private Expression<Func<int, int>> _closureExpr;
    private Expression<Func<int>> _nestedLambdaExpr;
    private Expression<Func<int, int>> _tryCatchExpr;
    private LambdaExpression _largeExpr;  // 100+ nodes

    [GlobalSetup]
    public void Setup()
    {
        // Build expression trees of varying complexity
        _simpleExpr = (a, b) => a + b * 2;
        _closureExpr = BuildClosureExpression();
        _nestedLambdaExpr = BuildNestedLambdaExpression();
        _tryCatchExpr = BuildTryCatchExpression();
        _largeExpr = BuildLargeExpression(nodeCount: 100);
    }

    [Benchmark(Baseline = true)]
    public Delegate SystemCompile() => _simpleExpr.Compile();

    [Benchmark]
    public Delegate FecCompile() => _simpleExpr.CompileFast();

    [Benchmark]
    public Delegate HyperbeeCompile() => _simpleExpr.CompileHyperbee();

    // Repeat for each expression complexity level...
}
```

**2. Delegate Execution Speed**

Measures the runtime performance of the compiled delegate itself:

```csharp
public class ExecutionBenchmarks
{
    private Func<int, int, int> _systemDelegate;
    private Func<int, int, int> _fecDelegate;
    private Func<int, int, int> _hyperbeeDelegate;

    [GlobalSetup]
    public void Setup()
    {
        Expression<Func<int, int, int>> expr = (a, b) => a + b * 2;
        _systemDelegate = expr.Compile();
        _fecDelegate = expr.CompileFast();
        _hyperbeeDelegate = expr.CompileHyperbee();
    }

    [Benchmark(Baseline = true)]
    public int SystemExecute() => _systemDelegate(42, 7);

    [Benchmark]
    public int FecExecute() => _fecDelegate(42, 7);

    [Benchmark]
    public int HyperbeeExecute() => _hyperbeeDelegate(42, 7);
}
```

**3. Memory Allocation**

BenchmarkDotNet's `[MemoryDiagnoser]` attribute captures Gen0/Gen1/Gen2 collections
and total bytes allocated. This is critical for measuring the IR approach's allocation
advantage.

**4. Compilation + Execution Combined**

Measures the total cost when an expression is compiled and executed once (cold-path
scenario common in DI containers and ORMs):

```csharp
[Benchmark]
public int SystemCompileAndRun()
{
    var del = _expr.Compile();
    return del(42);
}
```

#### Expression Complexity Tiers

| Tier | Description | Example |
|---|---|---|
| Trivial | Constant, parameter, simple arithmetic | `(a, b) => a + b` |
| Simple | Method calls, conditionals, type conversions | `(a) => a > 0 ? a.ToString() : "negative"` |
| Medium | Closures, member access, multiple statements | Block with captured variables |
| Complex | Nested lambdas, try/catch, loops | Try/catch inside method call arguments |
| Large | 100+ node trees | Auto-generated deep expression trees |
| Pathological | Patterns that cause FEC to fail | Return from TryCatch with compound assignment |

#### Benchmark Report Format

```
|         Method |      Tier |       Mean |    StdDev | Ratio | Allocated |
|--------------- |---------- |-----------:|----------:|------:|----------:|
| SystemCompile  |   Trivial |  35.42 us  |  0.82 us  |  1.00 |    4.2 KB |
|    FecCompile  |   Trivial |   9.81 us  |  0.34 us  |  0.28 |    1.1 KB |
|HyperbeeCompile |   Trivial |  ?.?? us   |  ?.?? us  |  ?.?? |    ?.? KB |
|                |           |            |           |       |           |
| SystemCompile  |   Complex | 415.09 us  | 12.31 us  |  1.00 |   42.1 KB |
|    FecCompile  |   Complex |  11.12 us  |  0.87 us  |  0.03 |    2.8 KB |
|HyperbeeCompile |   Complex |  ?.?? us   |  ?.?? us  |  ?.?? |    ?.? KB |
```

### Testing Strategy

#### Test Source 1: dotnet/runtime Expression Tests (MIT Licensed)

The .NET runtime's test suite for `System.Linq.Expressions` is comprehensive
and MIT-licensed. It is located at:

```
https://github.com/dotnet/runtime/tree/main/src/libraries/System.Linq.Expressions/tests
```

**Structure:**
- **24+ test directories** organized by expression type (Array, BinaryOperators,
  Block, Call, Cast, Conditional, Constant, Convert, ExceptionHandling, Goto,
  IndexExpression, Invoke, Label, Lambda, Lifted, ListInit, Loop, Member,
  MemberInit, New, Switch, TypeBinary, Unary, Variables)
- **Key test files:** CompilerTests.cs, StackSpillerTests.cs, InterpreterTests.cs
- **Test framework:** xUnit

**Borrowing approach:**

These tests validate the *behavior* of compiled expressions (input → output
correctness). We can adapt them to run against all three compilers:

```csharp
public abstract class ExpressionTestBase
{
    protected abstract Delegate CompileExpression(LambdaExpression lambda);

    [Fact]
    public void Add_Int32_ReturnsCorrectResult()
    {
        var a = Expression.Parameter(typeof(int));
        var b = Expression.Parameter(typeof(int));
        var expr = Expression.Lambda<Func<int, int, int>>(
            Expression.Add(a, b), a, b);

        var del = (Func<int, int, int>)CompileExpression(expr);
        Assert.Equal(5, del(2, 3));
        Assert.Equal(0, del(-1, 1));
        Assert.Equal(int.MinValue, del(int.MaxValue, 1)); // overflow
    }
}

// Three derived test classes -- same tests, different compiler
public class SystemCompilerTests : ExpressionTestBase
{
    protected override Delegate CompileExpression(LambdaExpression lambda)
        => lambda.Compile();
}

public class FecCompilerTests : ExpressionTestBase
{
    protected override Delegate CompileExpression(LambdaExpression lambda)
        => lambda.CompileFast();
}

public class HyperbeeCompilerTests : ExpressionTestBase
{
    protected override Delegate CompileExpression(LambdaExpression lambda)
        => HyperbeeCompiler.Compile(lambda);
}
```

This approach guarantees that all three compilers are validated against the same
test cases, making behavioral differences immediately visible.

#### Test Source 2: FEC Issue-Driven Tests

FEC's GitHub issues document specific expression patterns that FEC fails on.
These are high-value test cases for Hyperbee because they represent the patterns
that motivated this project:

```
https://github.com/dadhi/FastExpressionCompiler/issues
```

Key failure-pattern issues to port as tests:
- Issue #495: Return goto from TryCatch with compound assignment
- Issues around nested lambda capture edge cases
- By-ref argument handling with stack spilling
- Complex MemberInit/ListInit patterns

#### Test Source 3: Differential Testing (Fuzzing)

Generate random expression trees and verify that all three compilers produce
delegates with identical behavior:

```csharp
public class DifferentialTests
{
    [Theory]
    [MemberData(nameof(GenerateRandomExpressions))]
    public void AllCompilersProduceSameResult(
        LambdaExpression expr, object[] args, string description)
    {
        var systemResult = Execute(expr.Compile(), args);
        var hyperbeeResult = Execute(HyperbeeCompiler.Compile(expr), args);

        Assert.Equal(systemResult, hyperbeeResult,
            $"Mismatch for: {description}");

        // FEC may return null for unsupported patterns -- that's OK
        var fecDelegate = expr.CompileFast(ifFastFailedReturnNull: true);
        if (fecDelegate != null)
        {
            var fecResult = Execute(fecDelegate, args);
            Assert.Equal(systemResult, fecResult,
                $"FEC mismatch for: {description}");
        }
    }

    private static object Execute(Delegate del, object[] args)
    {
        try { return del.DynamicInvoke(args); }
        catch (TargetInvocationException ex) { return ex.InnerException.GetType(); }
    }
}
```

#### Test Source 4: IL Verification

For `CompileToMethod` output, run the persisted assembly through ILVerify:

```csharp
[Fact]
public void CompileToMethod_ProducesVerifiableIL()
{
    var expr = BuildTestExpression();
    string tempPath = Path.GetTempFileName() + ".dll";

    try
    {
        HyperbeeCompiler.CompileToType(expr, savePath: tempPath);

        // Run ILVerify on the output
        var result = ILVerify(tempPath);
        Assert.True(result.Success, $"IL verification failed: {result.Errors}");
    }
    finally
    {
        File.Delete(tempPath);
    }
}
```

#### Test Source 5: Round-Trip Validation

Compile to MethodBuilder, save to disk, load back, execute, and compare:

```csharp
[Fact]
public void CompileToMethod_RoundTrip_ProducesCorrectResults()
{
    var expr = Expression.Lambda<Func<int, int>>(
        Expression.Multiply(param, Expression.Constant(2)), param);

    // Compile via DynamicMethod
    var directResult = HyperbeeCompiler.Compile(expr);

    // Compile via MethodBuilder → Save → Load → Execute
    string path = Path.GetTempFileName() + ".dll";
    var type = HyperbeeCompiler.CompileToType(expr, savePath: path);
    var assembly = Assembly.LoadFrom(path);
    var method = assembly.GetType("CompiledExpression").GetMethod("Execute");
    var roundTripResult = method.Invoke(null, new object[] { 21 });

    Assert.Equal(directResult.DynamicInvoke(21), roundTripResult);
}
```

### Test Project Structure

```
Hyperbee.ExpressionCompiler/
  src/
    Hyperbee.ExpressionCompiler/          -- Main library
  test/
    Hyperbee.ExpressionCompiler.Tests/    -- Correctness tests
      Compiler/
        SystemCompilerTests.cs            -- Base tests via System.Compile
        FecCompilerTests.cs               -- Same tests via FEC
        HyperbeeCompilerTests.cs          -- Same tests via Hyperbee
      ExpressionTypes/
        BinaryTests.cs                    -- Ported from dotnet/runtime
        UnaryTests.cs
        CallTests.cs
        ConditionalTests.cs
        TryCatchTests.cs
        ClosureTests.cs
        NestedLambdaTests.cs
        LoopTests.cs
        SwitchTests.cs
        ...                               -- One file per expression category
      EdgeCases/
        FecFailureTests.cs                -- Patterns FEC fails on
        StackSpillerTests.cs              -- Complex spilling scenarios
        DeepTreeTests.cs                  -- Stack overflow protection
      CompileToMethod/
        BasicCompileToMethodTests.cs
        ConstantHandlingTests.cs
        RoundTripTests.cs
        ILVerificationTests.cs
      Differential/
        RandomExpressionTests.cs          -- Fuzzing/differential testing
    Hyperbee.ExpressionCompiler.Benchmarks/
      CompilationBenchmarks.cs            -- Compilation speed
      ExecutionBenchmarks.cs              -- Delegate execution speed
      AllocationBenchmarks.cs             -- Memory allocation
      CompileAndRunBenchmarks.cs          -- Combined cold-path
      ScalingBenchmarks.cs                -- Performance vs tree size
```

---

## Document History

- **2026-02-22** -- Initial creation. Research and analysis of System Expression
  Compiler, FastExpressionCompiler, and proposed IR-based architecture.
- **2026-02-22** -- Added graceful fallback strategy (Section 7), CompileToMethod
  support (Section 8), CompileToMethod history (Appendix D), and comprehensive
  benchmarking and testing strategy (Appendix E).
