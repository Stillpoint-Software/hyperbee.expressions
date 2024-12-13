using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using BenchmarkDotNet.Attributes;
using DotNext.Linq.Expressions;
using DotNext.Metaprogramming;
using FastExpressionCompiler;
using static System.Linq.Expressions.Expression;
using static Hyperbee.Expressions.ExpressionExtensions;

namespace Hyperbee.Expressions.Benchmark;

public class AsyncBenchmarks
{
    private Func<Task<int>> _warmCompiled = null!;
    private Func<Task<int>> _warmFastCompiled = null!;
    private Func<Task<int>> _warmNextCompiled = null!;
    private Func<Task<int>> _warmNextFastCompiled = null!;

    private Func<Task<int>> _firstCompiled = null!;
    private Func<Task<int>> _firstFastCompiled = null!;
    private Func<Task<int>> _firstNextCompiled = null!;
    private Func<Task<int>> _firstNextFastCompiled = null!;

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

        _nextlambda = CodeGenerator.AsyncLambda<Func<Task<int>>>( ( fun, result ) =>
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
        _warmCompiled = _lambda.Compile();
        _warmFastCompiled = _lambda.CompileFast();
        _warmNextCompiled = _nextlambda.Compile();
        //_warmFastNextCompiled = _nextlambda.CompileFast();

        Warmup( _warmCompiled, _warmFastCompiled, _warmNextCompiled );

        RuntimeHelpers.PrepareDelegate( _firstCompiled );
        RuntimeHelpers.PrepareDelegate( _firstFastCompiled );

        GcCollect();

        return;

        // Helpers

        void Warmup( params Func<Task<int>>[] funcs )
        {
            foreach ( var func in funcs )
            {
                func().Wait();
            }
        }

        void GcCollect()
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }
    }

    [IterationSetup]
    public void IterationSetup()
    {
        // build and don't call - to capture first time hit
        _firstCompiled = _lambda.Compile();
        _firstFastCompiled = _lambda.CompileFast();
        _firstNextCompiled = _nextlambda.Compile();
        //_firstFastNextCompiled = _nextlambda.CompileFast();
    }

    // Compile


    [BenchmarkCategory( "Compile" )]
    [Benchmark( Description = "Hyperbee Compile") ]
    public void Hyperbee_AsyncBlock_Compile()
    {
        _lambda.Compile();
    }

    [BenchmarkCategory( "Compile" )]
    [Benchmark( Description = "Hyperbee Fast Compile" )]
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

    // First Execute

    [BenchmarkCategory( "First Execute" )]
    [Benchmark( Description = "Hyperbee First Execute" )]
    public async Task Hyperbee_AsyncBlock_FirstExecute()
    {
        await _firstCompiled();
    }

    [BenchmarkCategory( "First Execute" )]
    [Benchmark( Description = "Hyperbee First Fast Execute" )]
    public async Task Hyperbee_AsyncBlock_FirstFastExecute()
    {
        await _firstFastCompiled();
    }

    [BenchmarkCategory( "First Execute" )]
    [Benchmark( Description = "DotNext First Execute" )]
    public async Task DotNext_AsyncLambda_FirstExecute()
    {
        await _firstNextCompiled();
    }

    // Execute

    [BenchmarkCategory( "Execute" )]
    [Benchmark( Description = "Native Execute" )]
    public async Task Native_Async_Execute()
    {
        await NativeTestAsync();
    }

    [BenchmarkCategory( "Execute" )]
    [Benchmark( Description = "Hyperbee Execute" )]
    public async Task Hyperbee_AsyncBlock_Execute()
    {
        await _warmCompiled();
    }

    [BenchmarkCategory( "Execute" )]
    [Benchmark( Description = "Hyperbee Fast Execute" )]
    public async Task Hyperbee_AsyncBlock_FastExecute()
    {
        await _warmFastCompiled();
    }

    [BenchmarkCategory( "Execute" )]
    [Benchmark( Description = "DotNext Execute" )]
    public async Task DotNext_AsyncLambda_Execute()
    {
        await _warmNextCompiled();
    }

    // Helpers

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

    public static async Task<int> NativeTestAsync()
    {
        var variable = await InitVariableAsync();
        if ( await IsTrueAsync() )
        {
            variable = await AddAsync( variable, variable );
        }

        return variable;
    }
}
