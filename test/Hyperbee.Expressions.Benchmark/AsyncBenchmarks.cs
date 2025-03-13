using System.Linq.Expressions;
using BenchmarkDotNet.Attributes;
using DotNext.Linq.Expressions;
using DotNext.Metaprogramming;
using FastExpressionCompiler;
using static System.Linq.Expressions.Expression;
using static Hyperbee.Expressions.ExpressionExtensions;

namespace Hyperbee.Expressions.Benchmark;

public class AsyncBenchmarks
{
    private Func<Task<int>> _preRunCompiled = null!;
    private Func<Task<int>> _preRunFastCompiled = null!;
    private Func<Task<int>> _preRunNextCompiled = null!;
    private Func<Task<int>> _preRunCompiledInterpret = null!;
    //private Func<Task<int>> _preRunNextFastCompiled = null!;

    private Expression<Func<Task<int>>> _lambda = null!;
    private Expression<Func<Task<int>>> _nextlambda = null!;

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

        _lambda = Lambda<Func<Task<int>>>( _expression );

        _nextlambda = CodeGenerator.AsyncLambda<Func<Task<int>>>( ( _, result ) =>
        {
            var isTrue = CodeGenerator.DeclareVariable( "isTrue", typeof( bool ).New() );

            CodeGenerator.Assign( result, typeof( AsyncBenchmarks )
                .CallStatic( nameof( InitVariableAsync ) )
                .Await()
            );

            CodeGenerator.Assign( isTrue, typeof( AsyncBenchmarks )
                .CallStatic( nameof( IsTrueAsync ) )
                .Await()
            );

            CodeGenerator.IfThen( isTrue, () =>
            {
                CodeGenerator.Assign( result, typeof( AsyncBenchmarks )
                    .CallStatic( nameof( AddAsync ), result, result )
                    .Await()
                );
            } );
        } );

        // build and call once for warmup

        _preRunCompiled = _lambda.Compile();
        _preRunCompiledInterpret = _lambda.Compile( preferInterpretation: true );
        _preRunFastCompiled = _lambda.CompileFast();
        _preRunNextCompiled = _nextlambda.Compile();
        //_preRunFastNextCompiled = _nextlambda.CompileFast();

        Warmup( _preRunCompiled, _preRunFastCompiled, _preRunNextCompiled );

        return;

        // Helpers

        static void Warmup( params Func<Task<int>>[] funcs )
        {
            foreach ( var func in funcs )
            {
                func().Wait();
            }
        }
    }

    // Compile

    [BenchmarkCategory( "Compile" )]
    [Benchmark( Description = "Hyperbee Compile" )]
    public void Hyperbee_AsyncBlock_Compile()
    {
        _lambda.Compile();
    }

    [BenchmarkCategory( "Compile" )]
    [Benchmark( Description = "Hyperbee Compile Interpret" )]
    public void Hyperbee_AsyncBlock_CompileInterpret()
    {
        _lambda.Compile( preferInterpretation: true );
    }

    [BenchmarkCategory( "Compile" )]
    [Benchmark( Description = "Hyperbee Fast Compile", Baseline = true )]
    public void Hyperbee_AsyncBlock_FastCompile()
    {
        _lambda.CompileFast();
    }

    [BenchmarkCategory( "Compile" )]
    [Benchmark( Description = "DotNext Compile" )]
    public void DotNext_AsyncLambda_Compile()
    {
        _nextlambda.Compile();
    }

    // Execute

    [BenchmarkCategory( "Execute" )]
    [Benchmark( Description = "Native Execute", Baseline = true )]
    public async Task Native_Async_Execute()
    {
        await NativeTestAsync();
    }

    [BenchmarkCategory( "Execute" )]
    [Benchmark( Description = "Hyperbee Execute" )]
    public async Task Hyperbee_AsyncBlock_Execute()
    {
        await _preRunCompiled();
    }

    [BenchmarkCategory( "Compile" )]
    [Benchmark( Description = "Hyperbee Execute Interpret" )]
    public async Task Hyperbee_AsyncBlock_ExecuteInterpret()
    {
        _preRunCompiledInterpret = _lambda.Compile( preferInterpretation: true );
        await _preRunCompiledInterpret();
    }

    [BenchmarkCategory( "Execute" )]
    [Benchmark( Description = "Hyperbee Fast Execute" )]
    public async Task Hyperbee_AsyncBlock_FastExecute()
    {
        await _preRunFastCompiled();
    }

    [BenchmarkCategory( "Execute" )]
    [Benchmark( Description = "DotNext Execute" )]
    public async Task DotNext_AsyncLambda_Execute()
    {
        await _preRunNextCompiled();
    }

    // Helpers

    public static async Task<int> NativeTestAsync()
    {
        var variable = await InitVariableAsync();
        if ( await IsTrueAsync() )
        {
            variable = await AddAsync( variable, variable );
        }

        return variable;
    }

    public static Task<int> InitVariableAsync()
    {
        return Task.FromResult( Random.Shared.Next( 0, 10 ) );
    }

    public static Task<bool> IsTrueAsync()
    {
        return Task.FromResult( true );
    }

    public static Task<int> AddAsync( int a, int b )
    {
        return Task.FromResult( a + b );
    }
}
