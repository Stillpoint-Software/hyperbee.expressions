using System.Linq.Expressions;
using BenchmarkDotNet.Attributes;
using Hyperbee.Expressions.Compiler;

namespace Hyperbee.Expressions.Compiler.Benchmarks;

/// <summary>
/// Measures time and allocations to compile coroutine expression trees.
/// </summary>
/// <remarks>
/// Every invocation gets a tree that has never been reduced. A coroutine block caches its
/// reduction, so compiling the same instance twice measures a compile once and a cache hit
/// after -- and the two compilers would not even agree on which, because HEC rewrites
/// captured coroutine bodies and hands its own pass a fresh tree. Rebuilding in
/// [IterationSetup] keeps both sides cold and comparable; tree construction is setup and is
/// not part of the measurement.
/// </remarks>
[Config( typeof( BenchmarkConfig.ColdConfig ) )]
[MemoryDiagnoser]
public class CoroutineCompilationBenchmarks
{
    private Expression<Func<int, Task<int>>> _asyncNoCapture_System = null!;
    private Expression<Func<int, Task<int>>> _asyncNoCapture_Hyperbee = null!;
    private Expression<Func<int, Task<int>>> _asyncNoCaptureDelegate_Hyperbee = null!;

    private Expression<Func<int, Task<int>>> _asyncCapture_System = null!;
    private Expression<Func<int, Task<int>>> _asyncCapture_Hyperbee = null!;

    private Expression<Func<int, IEnumerable<int>>> _enumerableNoCapture_System = null!;
    private Expression<Func<int, IEnumerable<int>>> _enumerableNoCapture_Hyperbee = null!;

    private Expression<Func<int, IEnumerable<int>>> _enumerableCapture_System = null!;
    private Expression<Func<int, IEnumerable<int>>> _enumerableCapture_Hyperbee = null!;

    private Expression<Func<int, int>> _nestedClosure_System = null!;
    private Expression<Func<int, int>> _nestedClosure_Hyperbee = null!;

    [IterationSetup]
    public void IterationSetup()
    {
        _asyncNoCapture_System = CoroutineExpressions.AsyncNoCapture();
        _asyncNoCapture_Hyperbee = CoroutineExpressions.AsyncNoCapture();
        _asyncNoCaptureDelegate_Hyperbee = CoroutineExpressions.AsyncNoCapture( CoroutineExpressions.DelegateMoveNext() );

        _asyncCapture_System = CoroutineExpressions.AsyncCapture();
        _asyncCapture_Hyperbee = CoroutineExpressions.AsyncCapture();

        _enumerableNoCapture_System = CoroutineExpressions.EnumerableNoCapture();
        _enumerableNoCapture_Hyperbee = CoroutineExpressions.EnumerableNoCapture();

        _enumerableCapture_System = CoroutineExpressions.EnumerableCapture();
        _enumerableCapture_Hyperbee = CoroutineExpressions.EnumerableCapture();

        _nestedClosure_System = CoroutineExpressions.NestedClosure();
        _nestedClosure_Hyperbee = CoroutineExpressions.NestedClosure();
    }

    [Benchmark( Description = "AsyncNoCapture | System" )]
    public Delegate AsyncNoCapture_System() => _asyncNoCapture_System.Compile();

    [Benchmark( Description = "AsyncNoCapture | Hyperbee" )]
    public Delegate AsyncNoCapture_Hyperbee() => HyperbeeCompiler.Compile( _asyncNoCapture_Hyperbee );

    [Benchmark( Description = "AsyncNoCapture (delegate MoveNext) | Hyperbee" )]
    public Delegate AsyncNoCaptureDelegate_Hyperbee() => HyperbeeCompiler.Compile( _asyncNoCaptureDelegate_Hyperbee );

    [Benchmark( Description = "AsyncCapture | System" )]
    public Delegate AsyncCapture_System() => _asyncCapture_System.Compile();

    [Benchmark( Description = "AsyncCapture | Hyperbee" )]
    public Delegate AsyncCapture_Hyperbee() => HyperbeeCompiler.Compile( _asyncCapture_Hyperbee );

    [Benchmark( Description = "EnumerableNoCapture | System" )]
    public Delegate EnumerableNoCapture_System() => _enumerableNoCapture_System.Compile();

    [Benchmark( Description = "EnumerableNoCapture | Hyperbee" )]
    public Delegate EnumerableNoCapture_Hyperbee() => HyperbeeCompiler.Compile( _enumerableNoCapture_Hyperbee );

    [Benchmark( Description = "EnumerableCapture | System" )]
    public Delegate EnumerableCapture_System() => _enumerableCapture_System.Compile();

    [Benchmark( Description = "EnumerableCapture | Hyperbee" )]
    public Delegate EnumerableCapture_Hyperbee() => HyperbeeCompiler.Compile( _enumerableCapture_Hyperbee );

    [Benchmark( Description = "NestedClosure | System" )]
    public Delegate NestedClosure_System() => _nestedClosure_System.Compile();

    [Benchmark( Description = "NestedClosure | Hyperbee" )]
    public Delegate NestedClosure_Hyperbee() => HyperbeeCompiler.Compile( _nestedClosure_Hyperbee );
}

/// <summary>
/// Measures execution speed and allocations of compiled coroutine delegates.
/// All delegates are pre-compiled in GlobalSetup — only invocation cost is measured.
/// </summary>
[Config( typeof( BenchmarkConfig.StableConfig ) )]
[MemoryDiagnoser]
public class CoroutineExecutionBenchmarks
{
    private Func<int, Task<int>> _asyncNoCapture_System = null!;
    private Func<int, Task<int>> _asyncNoCapture_Hyperbee = null!;
    private Func<int, Task<int>> _asyncNoCaptureDelegate_Hyperbee = null!;

    private Func<int, Task<int>> _asyncCapture_System = null!;
    private Func<int, Task<int>> _asyncCapture_Hyperbee = null!;

    private Func<int, IEnumerable<int>> _enumerableNoCapture_System = null!;
    private Func<int, IEnumerable<int>> _enumerableNoCapture_Hyperbee = null!;

    private Func<int, IEnumerable<int>> _enumerableCapture_System = null!;
    private Func<int, IEnumerable<int>> _enumerableCapture_Hyperbee = null!;

    private Func<int, int> _nestedClosure_System = null!;
    private Func<int, int> _nestedClosure_Hyperbee = null!;

    private Func<int, Task<int>> _suspendingEmitted_Hyperbee = null!;
    private Func<int, Task<int>> _suspendingDelegate_Hyperbee = null!;

    [GlobalSetup]
    public void Setup()
    {
        _asyncNoCapture_System = CoroutineExpressions.AsyncNoCapture().Compile();
        _asyncNoCapture_Hyperbee = Compile( CoroutineExpressions.AsyncNoCapture, emittedIntoType: true );
        _asyncNoCaptureDelegate_Hyperbee = Compile( CoroutineExpressions.AsyncNoCapture, emittedIntoType: false );

        _asyncCapture_System = CoroutineExpressions.AsyncCapture().Compile();
        _asyncCapture_Hyperbee = HyperbeeCompiler.Compile( CoroutineExpressions.AsyncCapture() );

        _enumerableNoCapture_System = CoroutineExpressions.EnumerableNoCapture().Compile();
        _enumerableNoCapture_Hyperbee = HyperbeeCompiler.Compile( CoroutineExpressions.EnumerableNoCapture() );

        _enumerableCapture_System = CoroutineExpressions.EnumerableCapture().Compile();
        _enumerableCapture_Hyperbee = HyperbeeCompiler.Compile( CoroutineExpressions.EnumerableCapture() );

        _nestedClosure_System = CoroutineExpressions.NestedClosure().Compile();
        _nestedClosure_Hyperbee = HyperbeeCompiler.Compile( CoroutineExpressions.NestedClosure() );

        _suspendingEmitted_Hyperbee = Compile( CoroutineExpressions.AsyncSuspending, emittedIntoType: true );
        _suspendingDelegate_Hyperbee = Compile( CoroutineExpressions.AsyncSuspending, emittedIntoType: false );
    }

    [Benchmark( Description = "AsyncSuspending x16 | Hyperbee" )]
    public int AsyncSuspending_Hyperbee() => _suspendingEmitted_Hyperbee( 3 ).GetAwaiter().GetResult();

    [Benchmark( Description = "AsyncSuspending x16 (delegate MoveNext) | Hyperbee" )]
    public int AsyncSuspendingDelegate_Hyperbee() => _suspendingDelegate_Hyperbee( 3 ).GetAwaiter().GetResult();

    [Benchmark( Description = "AsyncNoCapture | System" )]
    public int AsyncNoCapture_System() => _asyncNoCapture_System( 3 ).GetAwaiter().GetResult();

    [Benchmark( Description = "AsyncNoCapture | Hyperbee" )]
    public int AsyncNoCapture_Hyperbee() => _asyncNoCapture_Hyperbee( 3 ).GetAwaiter().GetResult();

    [Benchmark( Description = "AsyncNoCapture (delegate MoveNext) | Hyperbee" )]
    public int AsyncNoCaptureDelegate_Hyperbee() => _asyncNoCaptureDelegate_Hyperbee( 3 ).GetAwaiter().GetResult();

    [Benchmark( Description = "AsyncCapture | System" )]
    public int AsyncCapture_System() => _asyncCapture_System( 3 ).GetAwaiter().GetResult();

    [Benchmark( Description = "AsyncCapture | Hyperbee" )]
    public int AsyncCapture_Hyperbee() => _asyncCapture_Hyperbee( 3 ).GetAwaiter().GetResult();

    [Benchmark( Description = "EnumerableNoCapture | System" )]
    public int EnumerableNoCapture_System() => Sum( _enumerableNoCapture_System( 3 ) );

    [Benchmark( Description = "EnumerableNoCapture | Hyperbee" )]
    public int EnumerableNoCapture_Hyperbee() => Sum( _enumerableNoCapture_Hyperbee( 3 ) );

    [Benchmark( Description = "EnumerableCapture | System" )]
    public int EnumerableCapture_System() => Sum( _enumerableCapture_System( 3 ) );

    [Benchmark( Description = "EnumerableCapture | Hyperbee" )]
    public int EnumerableCapture_Hyperbee() => Sum( _enumerableCapture_Hyperbee( 3 ) );

    [Benchmark( Description = "NestedClosure | System" )]
    public int NestedClosure_System() => _nestedClosure_System( 3 );

    [Benchmark( Description = "NestedClosure | Hyperbee" )]
    public int NestedClosure_Hyperbee() => _nestedClosure_Hyperbee( 3 );

    // Compiles under HEC and proves which MoveNext form was produced.
    //
    // The state machine falls back to the delegate form for a body that reaches a non-public
    // member, and a type being internal is enough to trigger it. That would quietly turn the
    // pair of tiers below into the same measurement, so it is checked rather than assumed.

    private static Func<int, Task<int>> Compile(
        Func<ExpressionRuntimeOptions, Expression<Func<int, Task<int>>>> factory,
        bool emittedIntoType )
    {
        const string delegateField = "__moveNextDelegate<>";

        var source = "";

        var options = emittedIntoType
            ? CoroutineExpressions.EmittedMoveNext( text => source = text )
            : CoroutineExpressions.DelegateMoveNext( text => source = text );

        var compiled = HyperbeeCompiler.Compile( factory( options ) );

        if ( source.Contains( delegateField ) == emittedIntoType )
        {
            throw new InvalidOperationException(
                $"expected MoveNext {( emittedIntoType ? "emitted into the type" : "as a delegate" )}, got the other form." );
        }

        return compiled;
    }

    private static int Sum( IEnumerable<int> source )
    {
        var total = 0;

        foreach ( var value in source )
            total += value;

        return total;
    }
}
