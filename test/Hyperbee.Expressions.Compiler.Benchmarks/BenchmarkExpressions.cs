using System.Linq.Expressions;

namespace Hyperbee.Expressions.Compiler.Benchmarks;

/// <summary>
/// Shared expression trees used by both CompilationBenchmarks and ExecutionBenchmarks.
/// </summary>
internal static class BenchmarkExpressions
{
    private static readonly int _captured = 42;

    // Tier 1: Simple — binary op, no closures
    public static readonly Expression<Func<int, int, int>> Simple = ( a, b ) => a + b;

    // Tier 2: Closure — captures an outer variable
    public static readonly Expression<Func<int, int>> Closure;

    // Tier 3: TryCatch — exception handling
    public static readonly Expression<Func<int>> TryCatch;

    // Tier 4: Complex — conditional + cast + method call
    public static readonly Expression<Func<object, string>> Complex;

    // Tier 5: Loop — while loop with break
    public static readonly Expression<Func<int, int>> Loop;

    // Tier 6: Switch — switch with multiple cases
    public static readonly Expression<Func<int, string>> Switch;

    static BenchmarkExpressions()
    {
        // Closure
        var p = Expression.Parameter( typeof( int ), "x" );
        var c = Expression.Constant( _captured );
        Closure = Expression.Lambda<Func<int, int>>( Expression.Add( p, c ), p );

        // TryCatch
        var result = Expression.Variable( typeof( int ), "result" );
        TryCatch = Expression.Lambda<Func<int>>(
            Expression.Block(
                new[] { result },
                Expression.TryCatch(
                    Expression.Assign( result, Expression.Constant( 42 ) ),
                    Expression.Catch( typeof( Exception ), Expression.Assign( result, Expression.Constant( -1 ) ) )
                ),
                result
            ) );

        // Complex
        var obj = Expression.Parameter( typeof( object ), "obj" );
        Complex = Expression.Lambda<Func<object, string>>(
            Expression.Condition(
                Expression.TypeIs( obj, typeof( string ) ),
                Expression.Call( Expression.Convert( obj, typeof( string ) ), typeof( string ).GetMethod( "ToUpper", Type.EmptyTypes )! ),
                Expression.Constant( "(not a string)" )
            ),
            obj );

        // Loop: sum 1..n
        var n = Expression.Parameter( typeof( int ), "n" );
        var sum = Expression.Variable( typeof( int ), "sum" );
        var i = Expression.Variable( typeof( int ), "i" );
        var breakLabel = Expression.Label( typeof( int ), "break" );
        Loop = Expression.Lambda<Func<int, int>>(
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
        var val = Expression.Parameter( typeof( int ), "val" );
        Switch = Expression.Lambda<Func<int, string>>(
            Expression.Switch(
                val,
                Expression.Constant( "other" ),
                Expression.SwitchCase( Expression.Constant( "one" ), Expression.Constant( 1 ) ),
                Expression.SwitchCase( Expression.Constant( "two" ), Expression.Constant( 2 ) ),
                Expression.SwitchCase( Expression.Constant( "three" ), Expression.Constant( 3 ) )
            ),
            val );
    }
}
