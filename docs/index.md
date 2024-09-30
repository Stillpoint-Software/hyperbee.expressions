---
layout: default
title: Hyperbee AsyncExpressions
nav_order: 1
---
# Welcome to Hyperbee AsyncExpressions

`Hyperbee.AsyncExpressions` is a library for creating c# expression trees that support asynchronous operations using `async` and `await`.
This library extends the capabilities of standard expression trees to handle asynchronous workflows.

## Features

* * Asynchronous Expression Trees: Create expression trees that can easily handle complex `async` and `await` operations.
* State Machine Generation: Automatically transforms `async` expressions in to awaitable state machines.

Async Expressions are supported using two classes:
* `AwaitExpression`: An expression that represents an await operation.
* `AsyncBlockExpression`: An expression that represents an asynchronous code block.

## Usage

The following example demonstrates how to create an asynchronous expression tree that performs an asynchronous operation.

```csharp
```

## Credits

Special thanks to:

- Sergey Tepliakov - [Dissecting the async methods in C#](https://devblogs.microsoft.com/premier-developer/dissecting-the-async-methods-in-c/).
- [Just The Docs](https://github.com/just-the-docs/just-the-docs) for the documentation theme.

## Contributing

We welcome contributions! Please see our [Contributing Guide](https://github.com/Stillpoint-Software/.github/blob/main/.github/CONTRIBUTING.md) 
for more details.

