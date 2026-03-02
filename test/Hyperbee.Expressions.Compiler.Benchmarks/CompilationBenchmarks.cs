using System.Linq.Expressions;
using BenchmarkDotNet.Attributes;
using FastExpressionCompiler;
using Hyperbee.Expressions.Compiler;

namespace Hyperbee.Expressions.Compiler.Benchmarks;

/// <summary>
/// Measures time and allocations to go from LambdaExpression to a callable Delegate.
/// Primary metric for the Hyperbee.Expressions.Compiler project.
/// </summary>
[MemoryDiagnoser]
public class CompilationBenchmarks
{
    // Tier 1: Simple — binary op, no closures
    private static readonly Expression<Func<int, int, int>> _simple =
        ( a, b ) => a + b;

    // Tier 2: Closure — captures an outer variable
    private static readonly int _captured = 42;
    private static readonly Expression<Func<int, int>> _closure;

    // Tier 3: TryCatch — stack spilling required
    private static readonly Expression<Func<int>> _tryCatch;

    // Tier 4: Complex — conditional + cast + method call
    private static readonly Expression<Func<object, string>> _complex;

    // Tier 5: Loop — while loop with break
    private static readonly Expression<Func<int, int>> _loop;

    // Tier 6: Switch — switch with multiple cases
    private static readonly Expression<Func<int, string>> _switch;

    static CompilationBenchmarks()
    {
        // Closure
        var p = Expression.Parameter( typeof(int), "x" );
        var c = Expression.Constant( _captured );
        _closure = Expression.Lambda<Func<int, int>>( Expression.Add( p, c ), p );

        // TryCatch
        var result = Expression.Variable( typeof(int), "result" );
        _tryCatch = Expression.Lambda<Func<int>>(
            Expression.Block(
                new[] { result },
                Expression.TryCatch(
                    Expression.Assign( result, Expression.Constant( 42 ) ),
                    Expression.Catch( typeof(Exception), Expression.Assign( result, Expression.Constant( -1 ) ) )
                ),
                result
            ) );

        // Complex
        var obj = Expression.Parameter( typeof(object), "obj" );
        _complex = Expression.Lambda<Func<object, string>>(
            Expression.Condition(
                Expression.TypeIs( obj, typeof(string) ),
                Expression.Call( Expression.Convert( obj, typeof(string) ), typeof(string).GetMethod( "ToUpper", Type.EmptyTypes )! ),
                Expression.Constant( "(not a string)" )
            ),
            obj );

        // Loop: sum 1..n
        var n = Expression.Parameter( typeof(int), "n" );
        var sum = Expression.Variable( typeof(int), "sum" );
        var i = Expression.Variable( typeof(int), "i" );
        var breakLabel = Expression.Label( typeof(int), "break" );
        _loop = Expression.Lambda<Func<int, int>>(
            Expression.Block(
                new[] { sum, i },
                Expression.Assign( sum, Expression.Constant( 0 ) ),
                Expression.Assign( i, Expression.Constant( 1 ) ),
                Expression.Loop(
                    Expression.IfThenElse(
                        Expression.LessThanOrEqual( i, n ),
                        Expression.Block(
                            Expression.Assign( sum, Expression.Add( sum, i ) ),
                            Expression.Assign( i, Expression.Add( i, Expression.Constant( 1 ) ) )
                        ),
                        Expression.Break( breakLabel, sum )
                    ),
                    breakLabel
                )
            ),
            n );

        // Switch
        var val = Expression.Parameter( typeof(int), "val" );
        _switch = Expression.Lambda<Func<int, string>>(
            Expression.Switch(
                val,
                Expression.Constant( "other" ),
                Expression.SwitchCase( Expression.Constant( "one" ), Expression.Constant( 1 ) ),
                Expression.SwitchCase( Expression.Constant( "two" ), Expression.Constant( 2 ) ),
                Expression.SwitchCase( Expression.Constant( "three" ), Expression.Constant( 3 ) )
            ),
            val );
    }

    // --- Tier 1: Simple ---

    [Benchmark( Description = "Simple | System" )]
    public Delegate Simple_System() => _simple.Compile();

    [Benchmark( Description = "Simple | FEC" )]
    public Delegate Simple_Fec() => _simple.CompileFast();

    [Benchmark( Description = "Simple | Hyperbee" )]
    public Delegate Simple_Hyperbee() => HyperbeeCompiler.Compile( _simple );

    // --- Tier 2: Closure ---

    [Benchmark( Description = "Closure | System" )]
    public Delegate Closure_System() => _closure.Compile();

    [Benchmark( Description = "Closure | FEC" )]
    public Delegate Closure_Fec() => _closure.CompileFast();

    [Benchmark( Description = "Closure | Hyperbee" )]
    public Delegate Closure_Hyperbee() => HyperbeeCompiler.Compile( _closure );

    // --- Tier 3: TryCatch ---

    [Benchmark( Description = "TryCatch | System" )]
    public Delegate TryCatch_System() => _tryCatch.Compile();

    [Benchmark( Description = "TryCatch | FEC" )]
    public Delegate TryCatch_Fec() => _tryCatch.CompileFast();

    [Benchmark( Description = "TryCatch | Hyperbee" )]
    public Delegate TryCatch_Hyperbee() => HyperbeeCompiler.Compile( _tryCatch );

    // --- Tier 4: Complex ---

    [Benchmark( Description = "Complex | System" )]
    public Delegate Complex_System() => _complex.Compile();

    [Benchmark( Description = "Complex | FEC" )]
    public Delegate Complex_Fec() => _complex.CompileFast();

    [Benchmark( Description = "Complex | Hyperbee" )]
    public Delegate Complex_Hyperbee() => HyperbeeCompiler.Compile( _complex );

    // --- Tier 5: Loop ---

    [Benchmark( Description = "Loop | System" )]
    public Delegate Loop_System() => _loop.Compile();

    [Benchmark( Description = "Loop | FEC" )]
    public Delegate Loop_Fec() => _loop.CompileFast();

    [Benchmark( Description = "Loop | Hyperbee" )]
    public Delegate Loop_Hyperbee() => HyperbeeCompiler.Compile( _loop );

    // --- Tier 6: Switch ---

    [Benchmark( Description = "Switch | System" )]
    public Delegate Switch_System() => _switch.Compile();

    [Benchmark( Description = "Switch | FEC" )]
    public Delegate Switch_Fec() => _switch.CompileFast();

    [Benchmark( Description = "Switch | Hyperbee" )]
    public Delegate Switch_Hyperbee() => HyperbeeCompiler.Compile( _switch );
}
