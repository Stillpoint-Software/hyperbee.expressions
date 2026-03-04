using BenchmarkDotNet.Attributes;
using FastExpressionCompiler;
using Hyperbee.Expressions.Compiler;

namespace Hyperbee.Expressions.Compiler.Benchmarks;

/// <summary>
/// Measures time and allocations to go from LambdaExpression to a callable Delegate.
/// Primary metric for the Hyperbee.Expressions.Compiler project.
/// </summary>
[Config( typeof( BenchmarkConfig.Config ) )]
[MemoryDiagnoser]
public class CompilationBenchmarks
{
    // --- Tier 1: Simple ---

    [Benchmark( Description = "Simple | System" )]
    public Delegate Simple_System() => BenchmarkExpressions.Simple.Compile();

    [Benchmark( Description = "Simple | FEC" )]
    public Delegate Simple_Fec() => BenchmarkExpressions.Simple.CompileFast();

    [Benchmark( Description = "Simple | Hyperbee" )]
    public Delegate Simple_Hyperbee() => HyperbeeCompiler.Compile( BenchmarkExpressions.Simple );

    // --- Tier 2: Closure ---

    [Benchmark( Description = "Closure | System" )]
    public Delegate Closure_System() => BenchmarkExpressions.Closure.Compile();

    [Benchmark( Description = "Closure | FEC" )]
    public Delegate Closure_Fec() => BenchmarkExpressions.Closure.CompileFast();

    [Benchmark( Description = "Closure | Hyperbee" )]
    public Delegate Closure_Hyperbee() => HyperbeeCompiler.Compile( BenchmarkExpressions.Closure );

    // --- Tier 3: TryCatch ---

    [Benchmark( Description = "TryCatch | System" )]
    public Delegate TryCatch_System() => BenchmarkExpressions.TryCatch.Compile();

    [Benchmark( Description = "TryCatch | FEC" )]
    public Delegate TryCatch_Fec() => BenchmarkExpressions.TryCatch.CompileFast();

    [Benchmark( Description = "TryCatch | Hyperbee" )]
    public Delegate TryCatch_Hyperbee() => HyperbeeCompiler.Compile( BenchmarkExpressions.TryCatch );

    // --- Tier 4: Complex ---

    [Benchmark( Description = "Complex | System" )]
    public Delegate Complex_System() => BenchmarkExpressions.Complex.Compile();

    [Benchmark( Description = "Complex | FEC" )]
    public Delegate Complex_Fec() => BenchmarkExpressions.Complex.CompileFast();

    [Benchmark( Description = "Complex | Hyperbee" )]
    public Delegate Complex_Hyperbee() => HyperbeeCompiler.Compile( BenchmarkExpressions.Complex );

    // --- Tier 5: Loop ---

    [Benchmark( Description = "Loop | System" )]
    public Delegate Loop_System() => BenchmarkExpressions.Loop.Compile();

    [Benchmark( Description = "Loop | FEC" )]
    public Delegate Loop_Fec() => BenchmarkExpressions.Loop.CompileFast();

    [Benchmark( Description = "Loop | Hyperbee" )]
    public Delegate Loop_Hyperbee() => HyperbeeCompiler.Compile( BenchmarkExpressions.Loop );

    // --- Tier 6: Switch ---

    [Benchmark( Description = "Switch | System" )]
    public Delegate Switch_System() => BenchmarkExpressions.Switch.Compile();

    [Benchmark( Description = "Switch | FEC" )]
    public Delegate Switch_Fec() => BenchmarkExpressions.Switch.CompileFast();

    [Benchmark( Description = "Switch | Hyperbee" )]
    public Delegate Switch_Hyperbee() => HyperbeeCompiler.Compile( BenchmarkExpressions.Switch );
}
