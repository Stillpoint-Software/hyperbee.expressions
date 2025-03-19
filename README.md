# Welcome to Hyperbee Expressions Interpreter

`Hyperbee.Expressions.Interpreter` is a C# library that extends the capabilities of expression trees to run without compilation.

## Features

This adds the extension method `Intrepet()` to the `LambdaExpression` class to allow for the interpretation of expression trees at runtime.  

This works the same way that the built in `lambda.Compile()` does, but without the need for compilation.

Additionally it's similar to `lambda.Compile( preferInterpretation: true )` but allows for async code execution and other extension expressions.

## Examples

```csharp
    var lambda = Expression.Lambda<Func<int>>(Expression.Constant(1));

    var interpetedLambda = lambda.Interpret();

    var result = interpetedLambda();
```

> **Note:** Every call to the interpreted lambda will walk the expression tree to determine
> the correct result. In cases where the lambda is not reused this may perform better than compiling and 
> running, but if the lambda is reused it may be better compile it once.  Always profile to determine the best approach.

## Credits

Special thanks to:

- [Just The Docs](https://github.com/just-the-docs/just-the-docs) for the documentation theme.

## Contributing

We welcome contributions! Please see our [Contributing Guide](https://github.com/Stillpoint-Software/.github/blob/main/.github/CONTRIBUTING.md) 
for more details.
