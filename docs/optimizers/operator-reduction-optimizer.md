---
layout: default
title: Operator Reduction Optimizer
parent: optimizers
nav_order: 4
---
# Operator Reduction Optimizer
The **Operator Reduction Optimizer** simplifies arithmetic and logical expressions.

## **Purpose**
- **Issue**: Expression trees often contain redundant operations, such as adding zero or multiplying by one.
- **Solution**: By removing or simplifying these expressions, the optimizer reduces tree size and computation complexity.
- **Result**: Improved runtime performance through reduced instruction execution and simplified interpretation.

## **Usage Example**
### **Before Optimization**
```csharp
.Lambda #Lambda1<System.Func<int>> {
    .Add(.Parameter(x), .Constant(0))
}
```

### **After Optimization**
```csharp
.Lambda #Lambda1<System.Func<int>> {
    .Parameter(x)
}
```