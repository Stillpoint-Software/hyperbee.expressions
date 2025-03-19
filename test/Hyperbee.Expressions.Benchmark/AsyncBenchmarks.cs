using System.Linq.Expressions;
using BenchmarkDotNet.Attributes;
using DotNext.Linq.Expressions;
using DotNext.Metaprogramming;
using FastExpressionCompiler;
using Hyperbee.Expressions.Interpreter;
using static System.Linq.Expressions.Expression;
using static Hyperbee.Expressions.ExpressionExtensions;

namespace Hyperbee.Expressions.Benchmark;

public class AsyncBenchmarks
{
    private Func<Task<int>> _preRunCompiled = null!;
    private Func<Task<int>> _preRunInterpret = null!;
    private Func<Task<int>> _preRunFastCompiled = null!;
    private Func<Task<int>> _preRunNextCompiled = null!;
    private Func<Task<int>> _preRunCompiledInterpret = null!;
    //private Func<Task<int>> _preRunNextFastCompiled = null!;
    //private Func<Task<int>> _preRunNextCompiledInterpret = null!;

    private Expression<Func<Task<int>>> _lambda = null!;
    private Expression<Func<Task<int>>> _nextLambda = null!;

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

        _nextLambda = CodeGenerator.AsyncLambda<Func<Task<int>>>( ( _, result ) =>
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
        _preRunInterpret = _lambda.Interpret();

        _preRunNextCompiled = _nextLambda.Compile();
        //_preRunNextFastCompiled = _nextLambda.CompileFast();
        //_preRunNextCompiledInterpret = _nextLambda.Compile( preferInterpretation: true );

        Warmup(
            _preRunCompiled,
            _preRunCompiledInterpret,
            _preRunFastCompiled,
            _preRunInterpret,
            _preRunNextCompiled
        /*, _preRunNextFastCompiled */
        /*, _preRunNextCompiledInterpret */
        );

        return;

        // Helpers

        static void Warmup( params Func<Task<int>>[] functions )
        {
            foreach ( var func in functions )
            {
                func().Wait();
            }
        }
    }

    // Compile

    [BenchmarkCategory( "Compile" )]
    [Benchmark( Description = "Hyperbee System Compile" )]
    public void Hyperbee_AsyncBlock_Compile()
    {
        _lambda.Compile();
    }

    [BenchmarkCategory( "Compile" )]
    [Benchmark( Description = "Hyperbee Fast Compile", Baseline = true )]
    public void Hyperbee_AsyncBlock_FastCompile()
    {
        _lambda.CompileFast();
    }

    [BenchmarkCategory( "Compile" )]
    [Benchmark( Description = "DotNext System Compile" )]
    public void DotNext_AsyncLambda_Compile()
    {
        _nextLambda.Compile();
    }

    [BenchmarkCategory( "Compile" )]
    [Benchmark( Description = "DotNext Fast Compile" )]
    public void DotNext_AsyncLambda_FastCompile()
    {
        _nextLambda.CompileFast();
    }

    // Execute

    [BenchmarkCategory( "Execute" )]
    [Benchmark( Description = "Native Execute", Baseline = true )]
    public async Task Native_Async_Execute()
    {
        await NativeTestAsync();
    }

    [BenchmarkCategory( "Execute" )]
    [Benchmark( Description = "Hyperbee System Execute" )]
    public async Task Hyperbee_AsyncBlock_SystemExecute()
    {
        await _preRunCompiled();
    }

    [BenchmarkCategory( "Execute" )]
    [Benchmark( Description = "Hyperbee Fast Execute" )]
    public async Task Hyperbee_AsyncBlock_FastExecute()
    {
        await _preRunFastCompiled();
    }

    [BenchmarkCategory( "Execute" )]
    [Benchmark( Description = "DotNext System Execute" )]
    public async Task DotNext_AsyncLambda_SystemExecute()
    {
        await _preRunNextCompiled();
    }

    //[BenchmarkCategory( "Execute" )]
    //[Benchmark( Description = "DotNext Fast Execute" )]
    //public async Task DotNext_AsyncLambda_FastExecute()
    //{
    //    await _preRunNextFastCompiled();
    //}

    [BenchmarkCategory( "Execute" )]
    [Benchmark( Description = "Hyperbee System Interpret Execute" )]
    public async Task System_AsyncBlock_Execute_Interpret()
    {
        await _preRunCompiledInterpret();
    }

    [BenchmarkCategory( "Execute" )]
    [Benchmark( Description = "Hyperbee Interpret Execute" )]
    public async Task Hyperbee_AsyncBlock_Execute_Interpret()
    {
        await _preRunInterpret();
    }

    //[BenchmarkCategory( "Execute" )]
    //[Benchmark( Description = "DotNext System Interpret Execute" )]
    //public async Task DotNext_AsyncBlock_Execute_Interpret()
    //{
    //    await _preRunNextCompiledInterpret();
    //}


    // Interpret

    [BenchmarkCategory( "Interpret" )]
    [Benchmark( Description = "Hyperbee System Interpret" )]
    public void System_AsyncBlock_CompileInterpret()
    {
        _lambda.Compile( preferInterpretation: true );
    }

    [BenchmarkCategory( "Interpret" )]
    [Benchmark( Description = "Hyperbee Interpret" )]
    public void Hyperbee_AsyncBlock_Interpret()
    {
        _lambda.Interpret();
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
