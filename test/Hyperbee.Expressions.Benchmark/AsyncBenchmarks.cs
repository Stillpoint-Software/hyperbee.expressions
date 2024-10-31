using System.Linq.Expressions;
using BenchmarkDotNet.Attributes;
using FastExpressionCompiler;
using Hyperbee.Expressions.Transformation;
using static System.Linq.Expressions.Expression;
using static Hyperbee.Expressions.ExpressionExtensions;

namespace Hyperbee.Expressions.Benchmark;

public class AsyncBenchmarks
{
    private Func<Task<int>> _compiledLambda = null!;
    private Func<Task<int>> _fastCompiledLambda = null!;
    private Expression<Func<Task<int>>> _lambda = null!;

    private Expression _expression = null!;

    [GlobalSetup]
    public void Setup()
    {
        var asyncAddMethodInfo = typeof( AsyncBenchmarks ).GetMethod( nameof( AddAsync ) )!;
        var asyncIsTrueMethodInfo = typeof( AsyncBenchmarks ).GetMethod( nameof( IsTrueAsync ) )!;
        var asyncInitVariableMethodInfo = typeof( AsyncBenchmarks ).GetMethod( nameof( InitVariableAsync ) )!;

        var variable = Variable( typeof( int ), "variable" );

        _expression =
            BlockAsync(
                [variable],
                Assign( variable, Await( Call( asyncInitVariableMethodInfo ) ) ),
                IfThen( Await( Call( asyncIsTrueMethodInfo ) ),
                    Assign( variable,
                        Await( Call( asyncAddMethodInfo, variable, variable ) ) ) ),
                variable );

        _lambda = (Lambda<Func<Task<int>>>( _expression ).Reduce() as Expression<Func<Task<int>>>)!;

        _compiledLambda = _lambda.Compile();

        _fastCompiledLambda = _lambda.CompileFast();
    }

    [Benchmark]
    public void Hyperbee_ComputeHash()
    {
        var hasher = new ExpressionTreeHasher();
        var hash = hasher.ComputeHash( Block( _expression ) );
    }

    [Benchmark]
    public async Task Hyperbee_AsyncBlock_CompileAndExecute()
    {
        var compiled = _lambda.Compile();
        await compiled();
    }

    [Benchmark]
    public async Task Hyperbee_AsyncBlock_FastCompileAndExecute()
    {
        var compiled = _lambda.CompileFast();
        await compiled();
    }

    [Benchmark]
    public void Hyperbee_AsyncBlock_Compile()
    {
        _lambda.Compile();
    }

    [Benchmark]
    public void Hyperbee_AsyncBlock_FastCompile()
    {
        _lambda.CompileFast();
    }

    [Benchmark]
    public async Task Hyperbee_AsyncBlock_Execute()
    {
        await _compiledLambda();
    }

    [Benchmark]
    public async Task Hyperbee_AsyncBlock_FastExecute()
    {
        await _fastCompiledLambda();
    }

    [Benchmark]
    public async Task Compiled_Async_Execute()
    {
        await CompiledTestAsync();
    }

    public static Task<int> InitVariableAsync()
    {
        return Task.FromResult( Random.Shared.Next( 0, 10 ) );
    }

    public static Task<bool> IsTrueAsync()
    {
        var value = Random.Shared.Next( 0, 10 );
        return Task.FromResult( value % 2 == 0 );
    }

    public static Task<int> AddAsync( int a, int b )
    {
        return Task.FromResult( a + b );
    }

    public static async Task<int> CompiledTestAsync()
    {
        var variable = await InitVariableAsync();
        if ( await IsTrueAsync() )
        {
            variable = await AddAsync( variable, variable );
        }

        return variable;
    }
}
