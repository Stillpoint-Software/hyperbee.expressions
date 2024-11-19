---
layout: default
title: Value Binding Optimizer
parent: optimizers
nav_order: 6
---
# Value Binding Optimizer
The **Value Binding Optimizer** simplifies and inlines, variable, constant, and member access operations.

## **Purpose**
- **Issue**: Expression trees often include temporary variables or member accesses that can be resolved to constants or direct values.
- **Solution**: Inlines single-use variables, evaluates constant member accesses, and simplifies bindings.
- **Result**: Reduced memory usage and improved evaluation speed.

## **Usage Example**
### **Before Optimization**
```csharp
.Lambda #Lambda1<System.Func<int>> {
    .Block(
        .Assign(.Parameter(x), .Constant(5)),
        .Parameter(x)
    )
}
```

### **After Optimization**
```csharp
.Lambda #Lambda1<System.Func<int>> {
    .Constant(5)
}
```