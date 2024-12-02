using System.Linq.Expressions;
using BenchmarkDotNet.Attributes;
using Hyperbee.Expressions.Optimizers;

namespace Hyperbee.Expressions.Benchmark;

[MemoryDiagnoser]
public class OptimizerBenchmarks
{
    private ExpressionOptimizer _optimizer;
    private Expression _unoptimizedTree;

    /*

    // Inlining Optimizer
    [GlobalSetup( Targets =
    [
        nameof(BenchmarkInlining),
        nameof(ExecuteUnoptimizedInlining),
        nameof(ExecuteOptimizedInlining)
    ] )]
    public void SetupInlining()
    {
        const int Iterations = 1000;
        _optimizer = new InliningOptimizer();

        var labelTarget = Expression.Label( typeof( int ) );
        var paramX = Expression.Parameter( typeof( int ), "x" );
        var counter = Expression.Parameter( typeof( int ), "counter" );

        var lambda = Expression.Lambda(
            Expression.Add( paramX, Expression.Constant( 5 ) ),
            paramX
        );

        var targetExpr = Expression.Invoke( lambda, paramX );

        _unoptimizedTree = Expression.Block(
            [paramX, counter],
            Expression.Assign( paramX, Expression.Constant( 10 ) ),
            Expression.Assign( counter, Expression.Constant( 0 ) ),
            Expression.Loop(
                Expression.Block(
                    targetExpr,
                    Expression.IfThenElse(
                        Expression.LessThan( counter, Expression.Constant( Iterations ) ),
                        Expression.Assign( counter, Expression.Add( counter, Expression.Constant( 1 ) ) ),
                        Expression.Break( labelTarget, targetExpr )
                    )
                ),
                labelTarget
            )
        );
    }

    [Benchmark]
    public Expression BenchmarkInlining()
    {
        return _optimizer.Optimize( _unoptimizedTree );
    }

    [Benchmark]
    public int ExecuteUnoptimizedInlining()
    {
        var lambda = Expression.Lambda<Func<int>>( _unoptimizedTree );
        return lambda.Compile()();
    }

    [Benchmark]
    public int ExecuteOptimizedInlining()
    {
        var optimizedTree = _optimizer.Optimize( _unoptimizedTree );
        var lambda = Expression.Lambda<Func<int>>( optimizedTree );
        return lambda.Compile()();
    }

    // Operator Reduction Optimizer
    [GlobalSetup( Targets =
    [
        nameof(BenchmarkOperatorReduction),
        nameof(ExecuteUnoptimizedOperatorReduction),
        nameof(ExecuteOptimizedOperatorReduction)
    ] )]
    public void SetupOperatorReduction()
    {
        const int Iterations = 1000;
        _optimizer = new OperatorReductionOptimizer();

        var labelTarget = Expression.Label( typeof( int ) );
        var paramX = Expression.Parameter( typeof( int ), "x" );
        var counter = Expression.Parameter( typeof( int ), "counter" );

        var nestedExpr = Expression.Multiply(
            Expression.Add( paramX, Expression.Constant( 0 ) ),
            Expression.Add( Expression.Constant( 1 ), paramX )
        );

        var targetExpr = Expression.Add(
            nestedExpr,
            Expression.Multiply(
                Expression.Add( Expression.Constant( 2 ), Expression.Constant( 3 ) ),
                Expression.Constant( 1 )
            )
        );

        _unoptimizedTree = Expression.Block(
            [paramX, counter],
            Expression.Assign( paramX, Expression.Constant( 10 ) ),
            Expression.Assign( counter, Expression.Constant( 0 ) ),
            Expression.Loop(
                Expression.Block(
                    targetExpr,
                    Expression.IfThenElse(
                        Expression.LessThan( counter, Expression.Constant( Iterations ) ),
                        Expression.Assign( counter, Expression.Add( counter, Expression.Constant( 1 ) ) ),
                        Expression.Break( labelTarget, targetExpr )
                    )
                ),
                labelTarget
            )
        );
    }

    [Benchmark]
    public Expression BenchmarkOperatorReduction()
    {
        return _optimizer.Optimize( _unoptimizedTree );
    }

    [Benchmark]
    public int ExecuteUnoptimizedOperatorReduction()
    {
        var lambda = Expression.Lambda<Func<int>>( _unoptimizedTree );
        return lambda.Compile()();
    }

    [Benchmark]
    public int ExecuteOptimizedOperatorReduction()
    {
        var optimizedTree = _optimizer.Optimize( _unoptimizedTree );
        var lambda = Expression.Lambda<Func<int>>( optimizedTree );
        return lambda.Compile()();
    }

    //Structural Reduction Optimizer
    [GlobalSetup( Targets = [
       nameof(BenchmarkStructuralReduction),
        nameof(ExecuteUnoptimizedStructuralReduction),
        nameof(ExecuteOptimizedStructuralReduction)
    ] )]
    public void SetupStructuralReduction()
    {
        const int Iterations = 1000;
        _optimizer = new StructuralReductionOptimizer();

        var labelTarget = Expression.Label( typeof( int ) );
        var paramX = Expression.Parameter( typeof( int ), "x" );
        var counter = Expression.Parameter( typeof( int ), "counter" );

        var targetExpr = Expression.Block(
            [paramX, counter],
            Expression.Block(
                Expression.Block(
                    Expression.IfThen( // Constant conditional
                        Expression.Constant( true ),
                        Expression.Assign(
                            counter,
                            Expression.Add( counter, Expression.Constant( 30 + 12 ) )
                        )
                    ),
                    Expression.Goto( labelTarget, Expression.Constant( 0 ) ) // Redundant Goto
                )
            ),
            Expression.TryCatch( // Empty TryCatch
                Expression.Empty(),
                Expression.Catch( Expression.Parameter( typeof( Exception ), "ex" ), Expression.Empty() )
            )
        );

        _unoptimizedTree = Expression.Block(
            [paramX, counter],
            Expression.Assign( paramX, Expression.Constant( 10 ) ),
            Expression.Assign( counter, Expression.Constant( 0 ) ),
            Expression.Loop(
                Expression.IfThenElse(
                    Expression.LessThan( counter, Expression.Constant( Iterations ) ),
                    targetExpr,
                    Expression.Break( labelTarget, Expression.Constant( 0 ) )
                ),
                labelTarget
            )
        );
    }

    [Benchmark]
    public Expression BenchmarkStructuralReduction()
    {
        return _optimizer.Optimize( _unoptimizedTree );
    }

    [Benchmark]
    public int ExecuteUnoptimizedStructuralReduction()
    {
        var lambda = Expression.Lambda<Func<int>>( _unoptimizedTree );
        return lambda.Compile()();
    }

    [Benchmark]
    public int ExecuteOptimizedStructuralReduction()
    {
        var optimizedTree = _optimizer.Optimize( _unoptimizedTree );
        var lambda = Expression.Lambda<Func<int>>( optimizedTree );
        return lambda.Compile()();
    }

    */

    // Subexpression Caching Optimizer

    [GlobalSetup( Targets = [
        nameof(BenchmarkSubexpressionCaching),
        nameof(ExecuteUnoptimizedSubexpressionCaching),
        nameof(ExecuteOptimizedSubexpressionCaching)
    ] )]
    public void SetupSubexpressionCaching()
    {
        const int Iterations = 1000;
        _optimizer = new SubexpressionCachingOptimizer();

        var labelTarget = Expression.Label( typeof( int ) );
        var counter = Expression.Parameter( typeof( int ), "counter" );

        var repeatedExpr = Expression.Add(
            Expression.Multiply(
                Expression.Add( Expression.Constant( 3 ), Expression.Constant( 5 ) ),
                Expression.Add( Expression.Constant( 3 ), Expression.Constant( 5 ) )
            ),
            Expression.Add( Expression.Constant( 3 ), Expression.Constant( 5 ) )
        );

        var targetBlock = Expression.Block(
            repeatedExpr,
            Expression.TryCatch(
                Expression.Empty(),
                Expression.Catch( Expression.Parameter( typeof( Exception ), "ex" ), Expression.Empty() )
            )
        );

        _unoptimizedTree = Expression.Block(
            [counter],
            Expression.Assign( counter, Expression.Constant( 0 ) ),
            Expression.Loop(
                Expression.IfThenElse(
                    Expression.LessThan( counter, Expression.Constant( Iterations ) ),
                    Expression.Block(
                        Expression.Assign( counter, Expression.Add( counter, Expression.Constant( 1 ) ) ),
                        targetBlock
                    ),
                    Expression.Break( labelTarget, Expression.Constant( 0 ) )
                ),
                labelTarget
            )
        );
    }

    [Benchmark]
    public Expression BenchmarkSubexpressionCaching()
    {
        return _optimizer.Optimize( _unoptimizedTree );
    }

    [Benchmark]
    public int ExecuteUnoptimizedSubexpressionCaching()
    {
        var lambda = Expression.Lambda<Func<int>>( _unoptimizedTree );
        return lambda.Compile()();
    }

    //[Benchmark]
    public int ExecuteOptimizedSubexpressionCaching()
    {
        var optimizedTree = _optimizer.Optimize( _unoptimizedTree );
        var lambda = Expression.Lambda<Func<int>>( optimizedTree );
        return lambda.Compile()();
    }

    //// Value Binding Optimizer
    //[GlobalSetup( Targets = [
    //    nameof(BenchmarkValueBinding),
    //    nameof(ExecuteUnoptimizedValueBinding),
    //    nameof(ExecuteOptimizedValueBinding)
    //] )]
    //public void SetupValueBinding()
    //{
    //    _optimizer = new ValueBindingOptimizer();

    //    var labelTarget = Expression.Label( typeof(int) ); // Change to int
    //    var tempVar = Expression.Variable( typeof(string), "temp" );
    //    var counter = Expression.Variable( typeof(int), "counter" );

    //    var containerAccess = Expression.Constant( new Container() );
    //    var nestedProperty = Expression.Property( containerAccess, nameof(Container.Nested) );
    //    var nestedAccess = Expression.Property( nestedProperty, nameof(Container.Nested.Value) );

    //    _unoptimizedTree = Expression.Block(
    //        [tempVar, counter],
    //        Expression.Assign( tempVar, Expression.Constant( string.Empty ) ),
    //        Expression.Assign( counter, Expression.Constant( 0 ) ),
    //        Expression.Loop(
    //            Expression.Block(
    //                Expression.Assign( tempVar, nestedAccess ),
    //                Expression.IfThenElse(
    //                    Expression.LessThan( counter, Expression.Constant( 10 ) ),
    //                    Expression.Assign( counter, Expression.Add( counter, Expression.Constant( 1 ) ) ),
    //                    Expression.Break( labelTarget, counter )
    //                )
    //            ),
    //            labelTarget
    //        )
    //    );
    //}

    //[Benchmark]
    //public Expression BenchmarkValueBinding()
    //{
    //    return _optimizer.Optimize(_unoptimizedTree);
    //}

    //[Benchmark]
    //public int ExecuteUnoptimizedValueBinding()
    //{
    //    var lambda = Expression.Lambda<Func<int>>(_unoptimizedTree);
    //    return lambda.Compile()();
    //}

    //[Benchmark]
    //public int ExecuteOptimizedValueBinding()
    //{
    //    var optimizedTree = _optimizer.Optimize(_unoptimizedTree);
    //    var lambda = Expression.Lambda<Func<int>>(optimizedTree);
    //    return lambda.Compile()();
    //}

    private class TestClass
    {
        public string Method() => "Result";
    }

    private class Container
    {
        public NestedClass Nested { get; } = new ();
    }

    private class NestedClass
    {
        public string Value => "ExpectedValue";
    }
}
