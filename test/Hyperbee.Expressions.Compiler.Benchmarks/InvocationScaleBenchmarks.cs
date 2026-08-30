using BenchmarkDotNet.Attributes;
using FastExpressionCompiler;
using Hyperbee.Expressions.Compiler;

namespace Hyperbee.Expressions.Compiler.Benchmarks;

/// <summary>
/// The same tiers as <see cref="ExecutionBenchmarks"/>, invoked many times per operation.
/// </summary>
/// <remarks>
/// A tier like <c>(a, b) =&gt; a + b</c> runs in less time than the harness spends reaching
/// it: the means are a few nanoseconds against roughly three nanoseconds of overhead that
/// gets subtracted, so the per-call tiers in <see cref="ExecutionBenchmarks"/> are too close
/// to the floor to read. Calling in a loop puts the body far enough above it to compare.
///
/// The gap those tiers appear to show is real, not an artifact: with the overhead amortized
/// the ordering holds, and HEC costs roughly two nanoseconds a call more than the System
/// compiler on a small body, with FEC between them. That is worth an explanation this
/// benchmark does not give -- it says the difference exists and is stable, not where it is.
/// </remarks>
[Config( typeof( BenchmarkConfig.Config ) )]
public class InvocationScaleBenchmarks
{
    private const int Count = 1000;

    private Func<int, int, int> _simple_System = null!;
    private Func<int, int, int> _simple_Fec = null!;
    private Func<int, int, int> _simple_Hyperbee = null!;

    private Func<int> _tryCatch_System = null!;
    private Func<int> _tryCatch_Fec = null!;
    private Func<int> _tryCatch_Hyperbee = null!;

    private Func<int, string> _switch_System = null!;
    private Func<int, string> _switch_Fec = null!;
    private Func<int, string> _switch_Hyperbee = null!;

    [GlobalSetup]
    public void Setup()
    {
        _simple_System = BenchmarkExpressions.Simple.Compile();
        _simple_Fec = BenchmarkExpressions.Simple.CompileFast()!;
        _simple_Hyperbee = HyperbeeCompiler.Compile( BenchmarkExpressions.Simple );

        _tryCatch_System = BenchmarkExpressions.TryCatch.Compile();
        _tryCatch_Fec = BenchmarkExpressions.TryCatch.CompileFast()!;
        _tryCatch_Hyperbee = HyperbeeCompiler.Compile( BenchmarkExpressions.TryCatch );

        _switch_System = BenchmarkExpressions.Switch.Compile();
        _switch_Fec = BenchmarkExpressions.Switch.CompileFast()!;
        _switch_Hyperbee = HyperbeeCompiler.Compile( BenchmarkExpressions.Switch );
    }

    private static int Run( Func<int, int, int> compiled )
    {
        var total = 0;

        for ( var index = 0; index < Count; index++ )
            total += compiled( index, 4 );

        return total;
    }

    private static int Run( Func<int> compiled )
    {
        var total = 0;

        for ( var index = 0; index < Count; index++ )
            total += compiled();

        return total;
    }

    private static int Run( Func<int, string> compiled )
    {
        var total = 0;

        for ( var index = 0; index < Count; index++ )
            total += compiled( index & 3 ).Length;

        return total;
    }

    [Benchmark( Description = "Simple x1000 | System" )]
    public int Simple_System() => Run( _simple_System );

    [Benchmark( Description = "Simple x1000 | FEC" )]
    public int Simple_Fec() => Run( _simple_Fec );

    [Benchmark( Description = "Simple x1000 | Hyperbee" )]
    public int Simple_Hyperbee() => Run( _simple_Hyperbee );

    [Benchmark( Description = "TryCatch x1000 | System" )]
    public int TryCatch_System() => Run( _tryCatch_System );

    [Benchmark( Description = "TryCatch x1000 | FEC" )]
    public int TryCatch_Fec() => Run( _tryCatch_Fec );

    [Benchmark( Description = "TryCatch x1000 | Hyperbee" )]
    public int TryCatch_Hyperbee() => Run( _tryCatch_Hyperbee );

    [Benchmark( Description = "Switch x1000 | System" )]
    public int Switch_System() => Run( _switch_System );

    [Benchmark( Description = "Switch x1000 | FEC" )]
    public int Switch_Fec() => Run( _switch_Fec );

    [Benchmark( Description = "Switch x1000 | Hyperbee" )]
    public int Switch_Hyperbee() => Run( _switch_Hyperbee );
}
