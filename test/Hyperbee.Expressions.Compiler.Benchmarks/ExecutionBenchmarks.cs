using System.Linq.Expressions;
using BenchmarkDotNet.Attributes;
using FastExpressionCompiler;
using Hyperbee.Expressions.Compiler;

namespace Hyperbee.Expressions.Compiler.Benchmarks;

/// <summary>
/// Measures execution speed of delegates compiled by each compiler.
/// </summary>
[MemoryDiagnoser]
public class ExecutionBenchmarks
{
    private static readonly Expression<Func<int, int, int>> _expr = ( a, b ) => a + b;

    private Func<int, int, int> _systemFn = null!;
    private Func<int, int, int> _fecFn = null!;
    private Func<int, int, int> _hyperbeeFn = null!;

    [GlobalSetup]
    public void Setup()
    {
        _systemFn   = _expr.Compile();
        _fecFn      = _expr.CompileFast()!;
        _hyperbeeFn = HyperbeeCompiler.CompileWithFallback( _expr );
    }

    [Benchmark( Baseline = true, Description = "Execute | System" )]
    public int Execute_System() => _systemFn( 3, 4 );

    [Benchmark( Description = "Execute | FEC" )]
    public int Execute_Fec() => _fecFn( 3, 4 );

    [Benchmark( Description = "Execute | Hyperbee" )]
    public int Execute_Hyperbee() => _hyperbeeFn( 3, 4 );
}
