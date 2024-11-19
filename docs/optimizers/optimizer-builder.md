---
layout: default
title: Optimizer Builder
parent: optimizers
nav_order: 2
---
# Optimizer Builder
The `OptimizerBuilder` constructs a composite optimizer, and orders optimizers based on their dependencies and priorities, ensuring proper execution.

## **Dependency Ordering**
The builder:
1. Constructs a dependency graph based on declared dependencies.
2. Topologically sorts the graph to resolve dependency order.
3. Weaves standalone optimizers into the sequence based on priority.

## **Workflow**
- **Input Optimizers**: 
  - `OperatorReductionOptimizer`: Depends on `ConstantFoldingVisitor` and `OperatorReductionVisitor`.
  - `StructuralReductionOptimizer`: Depends on `StructuralReductionVisitor`.

- **Dependency Graph**:
  - Nodes: `{ConstantFoldingVisitor, OperatorReductionVisitor, StructuralReductionVisitor}`
  - Edges: `{ConstantFoldingVisitor -> OperatorReductionVisitor}`

- **Sorted Execution Order**:
  1. `ConstantFoldingVisitor`
  2. `OperatorReductionVisitor`
  3. `StructuralReductionVisitor`

## **Usage Example**
### **Builder Setup**
```csharp
var optimizer = new OptimizerBuilder()
    .With<ConstantFoldingOptimizer>()
    .With<OperatorReductionOptimizer>()
    .With<StructuralReductionOptimizer>()
    .Build();

var optimizedExpression = optimizer.Optimize(expression);
```
