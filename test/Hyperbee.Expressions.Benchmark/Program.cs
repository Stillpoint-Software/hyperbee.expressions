using BenchmarkDotNet.Running;

namespace Hyperbee.Expressions.Benchmark;

internal class Program
{
    static void Main( string[] args )
    {
        //try
        //{
        //    var bm = new OptimizerBenchmarks();

            //bm.SetupInlining();
            //bm.BenchmarkInlining();
            //bm.ExecuteUnoptimizedInlining();
            //bm.ExecuteOptimizedInlining();

            //bm.SetupOperatorReduction();
            //bm.BenchmarkOperatorReduction();
            //bm.ExecuteUnoptimizedOperatorReduction();
            //bm.ExecuteOptimizedOperatorReduction();

            //bm.SetupStructuralReduction();
            //bm.BenchmarkStructuralReduction();
            //bm.ExecuteUnoptimizedStructuralReduction();
            //bm.ExecuteOptimizedStructuralReduction();

            //bm.SetupSubexpressionCaching();
            //bm.BenchmarkSubexpressionCaching();
            //bm.ExecuteUnoptimizedSubexpressionCaching();
            //bm.ExecuteOptimizedSubexpressionCaching(); // execution of optimized throws

            //bm.SetupValueBinding();
            //bm.BenchmarkValueBinding(); // optimizer throws
            //bm.ExecuteUnoptimizedValueBinding();
            //bm.ExecuteOptimizedValueBinding();
        //}
        //catch ( Exception ex )
        //{
        //    Console.WriteLine( ex );
        //}

        BenchmarkSwitcher.FromAssembly( typeof( Program ).Assembly ).Run( args, new BenchmarkConfig.Config() );
    }
}
