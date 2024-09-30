using System.Linq.Expressions;
using BenchmarkDotNet.Attributes;
using FastExpressionCompiler;
using static System.Linq.Expressions.Expression;
using static Hyperbee.AsyncExpressions.AsyncExpression;

namespace Hyperbee.AsyncExpressions.Benchmark;

public class AsyncBenchmarks
{
    private Func<Task<int>> _compileLambda = null!;
    private Expression<Func<Task<int>>> _lambda = null!;

    [GlobalSetup]
    public void Setup()
    {
        var asyncAddMethodInfo = typeof( AsyncBenchmarks ).GetMethod( nameof( AddAsync ) )!;
        var asyncIsTrueMethodInfo = typeof( AsyncBenchmarks ).GetMethod( nameof( IsTrueAsync ) )!;
        var asyncInitVariableMethodInfo = typeof(AsyncBenchmarks).GetMethod( nameof(InitVariableAsync) )!;

        var variable = Variable( typeof( int ), "variable" );

        var asyncBlock =
            BlockAsync(
                [variable],
                Assign( variable, Await( Call( asyncInitVariableMethodInfo ) ) ),
                IfThen( Await( Call( asyncIsTrueMethodInfo ) ),
                    Assign( variable,
                        Await( Call( asyncAddMethodInfo, variable, variable ) ) ) ),
                variable );

        _lambda = Lambda<Func<Task<int>>>( asyncBlock );

        _compileLambda = _lambda.Compile();
        
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
    public async Task Hyperbee_AsyncBlock_Execute()
    {
        await _compileLambda();
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

    public async Task<int> CompiledTestAsync()
    {
        var variable = await InitVariableAsync();
        if ( await IsTrueAsync() )
        {
            variable = await AddAsync( variable, variable );
        }

        return variable;
    }
}
