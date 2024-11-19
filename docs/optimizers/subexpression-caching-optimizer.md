---
layout: default
title: Subexpression Caching Optimizer
parent: optimizers
nav_order: 7
---
# Subexpression Caching Optimizer
The **Subexpression Caching Optimizer** identifies and caches repeated subexpressions within a tree.

## **Purpose**
- **Issue**: Identical subexpressions evaluated multiple times can lead to redundant computations.
- **Solution**: Creates temporary variables for reusable subexpressions and updates the tree to use these variables.
- **Result**: Reduced computation time by minimizing repeated evaluation.

## **Usage Example**
### **Before Optimization**
```csharp
.Lambda #Lambda1<System.Func<int>> {
    .Add(.Multiply(5, (3 + 2)), .Multiply(5, (3 + 2)))
}
```

### **After Optimization**
```csharp
.Lambda #Lambda1<System.Func<int>> {
    .Block(System.Int32 $cacheVar) {
        $cacheVar = .Multiply(5, (3 + 2));
        .Add($cacheVar, $cacheVar)
    }
}
```
