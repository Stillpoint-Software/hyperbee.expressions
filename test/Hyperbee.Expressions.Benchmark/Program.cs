using BenchmarkDotNet.Running;

namespace Hyperbee.Expressions.Benchmark;

internal class Program
{
    static void Main( string[] args )
    {
        BenchmarkSwitcher.FromAssembly( typeof( Program ).Assembly ).Run( args, new BenchmarkConfig.Config() );
    }
}
