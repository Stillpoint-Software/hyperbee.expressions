# Welcome to Hyperbee Expressions

This repository contains libraries for extending and compiling C# expression trees.

## Packages

| Package | Description |
| ------- | ----------- |
| **Hyperbee.Expressions** | Extended expression tree nodes for async workflows, iterators, resource management, and looping constructs. |
| **Hyperbee.Expressions.Compiler** | A high-performance, IR-based expression compiler that is 9-34x faster than the System compiler with correct IL across all expression tree patterns. |

## Hyperbee.Expressions

`Hyperbee.Expressions` extends the capabilities of standard expression trees to handle asynchronous workflows and other 
language constructs.

### Features

* **Async Expressions**
    * `AwaitExpression`: An expression that represents an await operation.
    * `AsyncBlockExpression`: An expression that represents an asynchronous code block.

* **Yield Expressions**
    * `YieldExpression`: An expression that represents a yield return or break statement.
    * `EnumerableBlockExpression`: An expression that represents an enumerable code block.

* **Using Expression**
    * `UsingExpression`: An expression that automatically disposes IDisposable resources.

* **Looping Expressions**
    * `WhileExpression`: An expression that represents a while loop.
    * `ForExpression`: An expression that represents a for loop.
    * `ForEachExpression`: An expression that represents a foreach loop.

* **Other Expressions**
    * `StringFormatExpression`: An expression that creates a string using a supplied format string and parameters.
    * `ConfigurationExpression`: An expression that allows access to IConfiguration.
    * `InjectExpression`: An expression that allows for depency inject from a IServiceProvider.
    * `DebugExpression`: An expression that helps when debugging expression trees.

* Supports Fast Expression Compiler (FEC) for improved performance.

* Supports interpreted expression trees using `lambda.Compile(preferInterpretation: true)`. 
    ```csharp
    var lambda = Expression.Lambda<Func<int>>(Expression.Constant(1));
    var interpetedLambda = lambda.Compile(preferInterpretation: true);
    ```

### Examples

#### Asynchronous Expressions

The following example demonstrates how to create an asynchronous expression tree.

When the expression tree is compiled, the `AsyncBlockExpression` will auto-generate a state machine that executes 
`AwaitExpressions` in the block asynchronously.

```csharp

public class Example
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

#### Yield Expressions

The following example demonstrates how to create a yield expression tree.

When the expression tree is compiled, the `EnumerableBlockExpression` will auto-generate a state machine that executes
`YieldExpressions` in the block.

```csharp
public class Example
{
    public void ExampleYield()
    {
        // Create an enumerable block that yields values
        var index = Variable( typeof(int), "index" );

        var enumerableBlock = BlockEnumerable(
            [index],
            For( Assign( index, Constant( 0 ) ), LessThan( index, Constant( 10 ) ), PostIncrementAssign( index ),
                Yield( index )
            )
        );

        // Compile and execute the enumerable block
        var lambda = Lambda<Func<IEnumerable<int>>>( enumerableBlock );
        var compiledLambda = lambda.Compile();
        var enumerable = compiledLambda();

        foreach( var value in enumerable )
        {
            Console.WriteLine( $"Yielded value: {value}" );
        }
    }
}
```

#### Using Expression

The following example demonstrates how to create a Using expression.

```csharp
public class Example
{
    private class DisposableResource : IDisposable
    {
        public bool IsDisposed { get; private set; }
        public void Dispose() => IsDisposed = true;
    }

    public void ExampleUsing()
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

## Hyperbee.Expressions.Compiler

A high-performance, IR-based expression compiler for .NET. Drop-in replacement for `Expression.Compile()` 
that is **9-34x faster and allocates up to 50% less than the System compiler** and supports **all expression 
tree patterns** -- including those that [FastExpressionCompiler](https://github.com/dadhi/FastExpressionCompiler) doesn't.

### Quick Start

```
dotnet add package Hyperbee.Expressions.Compiler
```

```csharp
using Hyperbee.Expressions.Compiler;

// Direct compilation -- drop-in replacement for Expression.Compile()
var lambda = Expression.Lambda<Func<int, int, int>>(
    Expression.Add( a, b ), a, b );

var fn = HyperbeeCompiler.Compile( lambda );
var result = fn( 1, 2 ); // 3

// Or use the extension method
var fn = lambda.CompileHyperbee();
```

For benchmarks, architecture details, and advanced usage, see the full [Hyperbee.Expressions.Compiler README](src/Hyperbee.Expressions.Compiler/README.md).

## Credits

Special thanks to:

- Sergey Tepliakov - [Dissecting the async methods in C#](https://devblogs.microsoft.com/premier-developer/dissecting-the-async-methods-in-c/).
- [Fast Expression Compiler](https://github.com/dadhi/FastExpressionCompiler) for improved performance. :heart:
- [Just The Docs](https://github.com/just-the-docs/just-the-docs) for the documentation theme.

## Contributing

We welcome contributions! Please see our [Contributing Guide](https://github.com/Stillpoint-Software/.github/blob/main/.github/CONTRIBUTING.md) 
for more details
