using BenchmarkDotNet.Attributes;
using FastExpressionCompiler;
using Hyperbee.Expressions.Compiler;

namespace Hyperbee.Expressions.Compiler.Benchmarks;

/// <summary>
/// Measures execution speed and allocations of delegates compiled by each compiler.
/// All delegates are pre-compiled in GlobalSetup — only invocation cost is measured.
/// </summary>
[Config( typeof( BenchmarkConfig.Config ) )]
[MemoryDiagnoser]
public class ExecutionBenchmarks
{
    // --- Tier 1: Simple ---
    private Func<int, int, int> _simple_System = null!;
    private Func<int, int, int> _simple_Fec = null!;
    private Func<int, int, int> _simple_Hyperbee = null!;

    // --- Tier 2: Closure ---
    private Func<int, int> _closure_System = null!;
    private Func<int, int> _closure_Fec = null!;
    private Func<int, int> _closure_Hyperbee = null!;

    // --- Tier 3: TryCatch ---
    private Func<int> _tryCatch_System = null!;
    private Func<int> _tryCatch_Fec = null!;
    private Func<int> _tryCatch_Hyperbee = null!;

    // --- Tier 4: Complex ---
    private Func<object, string> _complex_System = null!;
    private Func<object, string> _complex_Fec = null!;
    private Func<object, string> _complex_Hyperbee = null!;

    // --- Tier 5: Loop ---
    private Func<int, int> _loop_System = null!;
    private Func<int, int> _loop_Fec = null!;
    private Func<int, int> _loop_Hyperbee = null!;

    // --- Tier 6: Switch ---
    private Func<int, string> _switch_System = null!;
    private Func<int, string> _switch_Fec = null!;
    private Func<int, string> _switch_Hyperbee = null!;

    [GlobalSetup]
    public void Setup()
    {
        _simple_System   = BenchmarkExpressions.Simple.Compile();
        _simple_Fec      = BenchmarkExpressions.Simple.CompileFast()!;
        _simple_Hyperbee = HyperbeeCompiler.Compile( BenchmarkExpressions.Simple );

        _closure_System   = BenchmarkExpressions.Closure.Compile();
        _closure_Fec      = BenchmarkExpressions.Closure.CompileFast()!;
        _closure_Hyperbee = HyperbeeCompiler.Compile( BenchmarkExpressions.Closure );

        _tryCatch_System   = BenchmarkExpressions.TryCatch.Compile();
        _tryCatch_Fec      = BenchmarkExpressions.TryCatch.CompileFast()!;
        _tryCatch_Hyperbee = HyperbeeCompiler.Compile( BenchmarkExpressions.TryCatch );

        _complex_System   = BenchmarkExpressions.Complex.Compile();
        _complex_Fec      = BenchmarkExpressions.Complex.CompileFast()!;
        _complex_Hyperbee = HyperbeeCompiler.Compile( BenchmarkExpressions.Complex );

        _loop_System   = BenchmarkExpressions.Loop.Compile();
        _loop_Fec      = BenchmarkExpressions.Loop.CompileFast()!;
        _loop_Hyperbee = HyperbeeCompiler.Compile( BenchmarkExpressions.Loop );

        _switch_System   = BenchmarkExpressions.Switch.Compile();
        _switch_Fec      = BenchmarkExpressions.Switch.CompileFast()!;
        _switch_Hyperbee = HyperbeeCompiler.Compile( BenchmarkExpressions.Switch );
    }

    // --- Tier 1: Simple ---

    [Benchmark( Description = "Simple | System" )]
    public int Simple_System() => _simple_System( 3, 4 );

    [Benchmark( Description = "Simple | FEC" )]
    public int Simple_Fec() => _simple_Fec( 3, 4 );

    [Benchmark( Description = "Simple | Hyperbee" )]
    public int Simple_Hyperbee() => _simple_Hyperbee( 3, 4 );

    // --- Tier 2: Closure ---

    [Benchmark( Description = "Closure | System" )]
    public int Closure_System() => _closure_System( 5 );

    [Benchmark( Description = "Closure | FEC" )]
    public int Closure_Fec() => _closure_Fec( 5 );

    [Benchmark( Description = "Closure | Hyperbee" )]
    public int Closure_Hyperbee() => _closure_Hyperbee( 5 );

    // --- Tier 3: TryCatch ---

    [Benchmark( Description = "TryCatch | System" )]
    public int TryCatch_System() => _tryCatch_System();

    [Benchmark( Description = "TryCatch | FEC" )]
    public int TryCatch_Fec() => _tryCatch_Fec();

    [Benchmark( Description = "TryCatch | Hyperbee" )]
    public int TryCatch_Hyperbee() => _tryCatch_Hyperbee();

    // --- Tier 4: Complex (allocates — string.ToUpper) ---

    [Benchmark( Description = "Complex | System" )]
    public string Complex_System() => _complex_System( "hello" );

    [Benchmark( Description = "Complex | FEC" )]
    public string Complex_Fec() => _complex_Fec( "hello" );

    [Benchmark( Description = "Complex | Hyperbee" )]
    public string Complex_Hyperbee() => _complex_Hyperbee( "hello" );

    // --- Tier 5: Loop ---

    [Benchmark( Description = "Loop | System" )]
    public int Loop_System() => _loop_System( 100 );

    [Benchmark( Description = "Loop | FEC" )]
    public int Loop_Fec() => _loop_Fec( 100 );

    [Benchmark( Description = "Loop | Hyperbee" )]
    public int Loop_Hyperbee() => _loop_Hyperbee( 100 );

    // --- Tier 6: Switch ---

    [Benchmark( Description = "Switch | System" )]
    public string Switch_System() => _switch_System( 2 );

    [Benchmark( Description = "Switch | FEC" )]
    public string Switch_Fec() => _switch_Fec( 2 );

    [Benchmark( Description = "Switch | Hyperbee" )]
    public string Switch_Hyperbee() => _switch_Hyperbee( 2 );
}
