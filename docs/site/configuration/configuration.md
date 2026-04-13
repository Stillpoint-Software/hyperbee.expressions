---
layout: default
title: Configuration
has_children: true
nav_order: 3
---

# Configuration

`Hyperbee.Expressions` provides several extension points for controlling how expression trees are
compiled and executed.

---

## Topics

| Topic | Description |
|-------|-------------|
| [Runtime Options](runtime-options.md) | `ExpressionRuntimeOptions` -- module providers, optimization, diagnostics |
| [Module Providers](module-providers.md) | `IModuleBuilderProvider` -- how dynamic types are generated |
| [Dependency Injection](dependency-injection.md) | Compiling expression trees with `IServiceProvider` |
