# Hyperbee.AsyncExpressions

`Hyperbee.AsyncExpressions` is a library for creating c# expression trees that support asynchronous operations using `async` and `await`.
This library extends the capabilities of standard expression trees to handle asynchronous workflows, making it easier to build 
complex asynchronous logic dynamically.

Features
* Asynchronous Expression Trees: Create expression trees that can handle easily handle complex `async` and `await` operations.
* State Machine Generation: Automatically transforms `async` expressions in to awaitable state machines that support complex control flows.

Async Expressions are supported using two classes:
* `AwaitExpression`: An expression that represents an await operation.
* `AsyncBlockExpression`: An expression that represents an asyncronous code block.

## Usage

The following example demonstrates how to create an asynchronous expression tree that performs a simple asynchronous operation.

```csharp
```


