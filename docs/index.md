---
layout: default
title: Hyperbee Expressions
nav_order: 1
---
# Welcome to Hyperbee Expressions

`Hyperbee.Expressions` is a library for creating c# expression trees that extends the capabilities of standard expression 
trees to handle asynchronous workflows and other constructs.

## Features

* **Async Expressions**
    * `AwaitExpression`: An expression that represents an await operation.
    * `AsyncBlockExpression`: An expression that represents an asynchronous code block.

* **Using Expression**
    * `UsingExpression`: An expression that automatically disposes IDisposable resources.

* **Looping Expressions**
    * `WhileExpression`: An expression that represents a while loop.
    * `ForExpression`: An expression that represents a for loop.
    * `ForEachExpression`: An expression that represents a foreach loop.

* Supports Fast Expression Compiler (FEC) for improved performance.

## Examples

### Asynchronous Expressions

The following example demonstrates how to create an asynchronous expression tree.

```csharp

public class AsyncExample
{
    public async Task ExampleAsync()
    {
        // Variables to store the results
        var result1 = Expression.Variable( typeof(int), "result1" );
        var result2 = Expression.Variable( typeof(int), "result2" );

        // Define two async method calls

        var instance = Expression.Constant( this );

        var awaitExpr1 = ExpressionExtensions.Await( 
            Expression.Call( instance, nameof(FirstAsyncMethod), Type.EmptyTypes ) 
        );

        var awaitExpr2 = ExpressionExtensions.Await( 
            Expression.Call( instance, nameof(SecondAsyncMethod), Type.EmptyTypes, result1 )
        );

        // Assign the results of the await expressions to the variables
        var assignResult1 = Expression.Assign( result1, awaitExpr1 );
        var assignResult2 = Expression.Assign( result2, awaitExpr2 );

        // Create an async block that calls both methods and assigns their results
        var asyncBlock = AsyncExpression.BlockAsync(
            [result1, result2],
            assignResult1,
            assignResult2
        );

        // Compile and execute the async block
        var lambda = Expression.Lambda<Func<Task<int>>>( asyncBlock );
        var compiledLambda = lambda.Compile();
        var resultValue2 = await compiledLambda();

        Console.WriteLine( $"Second async method result: {resultValue2}" );
    }

    public static async Task<int> FirstAsyncMethod()
    {
        await Task.Delay( 1000 ); // Simulate async work
        return 42; // Example result
    }

    public static async Task<int> SecondAsyncMethod( int value )
    {
        await Task.Delay( 1000 ); // Simulate async work
        return value * 2; // Example result
    }
}
```

### Disposable Expressions

The following example demonstrates how to create a Using expression.

```csharp
public class UsingExample
{
    private class DisposableResource : IDisposable
    {
        public bool IsDisposed { get; private set; }

        public void Dispose()
        {
            IsDisposed = true;
        }
    }

    public void UsingExpression_ShouldDisposeResource_AfterUse()
    {
        var resource = new TestDisposableResource();

        var disposableExpression = Expression.Constant( resource, typeof( TestDisposableResource ) );
        var bodyExpression = Expression.Empty(); // Actual body isn't important

        var usingExpression = ExpressionExtensions.Using( 
            disposableExpression, 
            bodyExpression 
        );

        var compiledLambda = Expression.Lambda<Action>( reducedExpression ).Compile();

        compiledLambda();

        Console.WriteLine( $"Resource was disposed {resource.IsDisposed}." );
    }
}
```

## Credits

Special thanks to:

- Sergey Tepliakov - [Dissecting the async methods in C#](https://devblogs.microsoft.com/premier-developer/dissecting-the-async-methods-in-c/).
- [Just The Docs](https://github.com/just-the-docs/just-the-docs) for the documentation theme.

## Contributing

We welcome contributions! Please see our [Contributing Guide](https://github.com/Stillpoint-Software/.github/blob/main/.github/CONTRIBUTING.md) 
for more details.