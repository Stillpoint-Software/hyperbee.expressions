# Welcome to Hyperbee Expressions

`Hyperbee.Expressions` is a C# library that extends the capabilities of expression trees to handle asynchronous 
workflows and other language constructs.

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

* **Other Expressions**
    * `StringFormatExpression`: An expression that creates a string using a supplied format string and parameters.
    * `DebugExpression`: An expression that helps when debugging expression trees.

## Examples

### Asynchronous Expressions

The following example demonstrates how to create an asynchronous expression tree.

When the expression tree is compiled, the `AsyncBlockExpression` will auto-generate a state machine that executes 
`AwaitExpressions` in the block asynchronously.

```csharp

public class AsyncExample
{
    public async Task ExampleAsync()
    {
        // Create an async block that calls async methods and assigns their results

        var instance = Constant( this );
        var result1 = Variable( typeof(int), "result1" );
        var result2 = Variable( typeof(int), "result2" );

        var asyncBlock = BlockAsync(
            [result1, result2],
            Assign( result1, Await(
                Call( instance, nameof(FirstAsyncMethod), Type.EmptyTypes )
            ) ),
            Assign( result2, Await(
                Call( instance, nameof(SecondAsyncMethod), Type.EmptyTypes, result1 )
            ) )
        );

        // Compile and execute the async block
        var lambda = Lambda<Func<Task<int>>>( asyncBlock );
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

### Using Expression

The following example demonstrates how to create a Using expression.

```csharp
public class UsingExample
{
    private class DisposableResource : IDisposable
    {
        public bool IsDisposed { get; private set; }
        public void Dispose() => IsDisposed = true;
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
- [Fast Expression Compiler](https://github.com/dadhi/FastExpressionCompiler) for improved performance. :heart:
- [Just The Docs](https://github.com/just-the-docs/just-the-docs) for the documentation theme.

## Contributing

We welcome contributions! Please see our [Contributing Guide](https://github.com/Stillpoint-Software/.github/blob/main/.github/CONTRIBUTING.md) 
for more details.

