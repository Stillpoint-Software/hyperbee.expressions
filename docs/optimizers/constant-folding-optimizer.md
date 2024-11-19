---
layout: default
title: Constant Folding Optimizer
parent: optimizers
nav_order: 3
---
# Constant Folding Optimizer
The **Constant Folding Optimizer** precomputes constant expressions.

## **Purpose**
- **Issue**: Expressions involving only constants can unnecessarily consume runtime resources for evaluation.
- **Solution**: Replaces constant expressions with their precomputed values.
- **Result**: Simplifies trees and improves runtime performance by removing redundant evaluations.

## **Usage Example**
### **Before Optimization**
```csharp
.Lambda #Lambda1<System.Func<int>> {
    .Add(.Constant(3), .Constant(5))
}
```

### **After Optimization**
```csharp
.Lambda #Lambda1<System.Func<int>> {
    .Constant(8)
}
```