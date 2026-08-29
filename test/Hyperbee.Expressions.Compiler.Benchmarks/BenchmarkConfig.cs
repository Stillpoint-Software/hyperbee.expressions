using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Environments;
using BenchmarkDotNet.Exporters;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Loggers;
using BenchmarkDotNet.Order;
using BenchmarkDotNet.Validators;

namespace Hyperbee.Expressions.Compiler.Benchmarks;

public class BenchmarkConfig
{
    public class Config : ManualConfig
    {
        public Config() : this( Job.ShortRun )
        {
        }

        protected Config( Job job )
        {
            AddJob( job
                .WithRuntime( CoreRuntime.Core90 )
                .WithId( ".NET 9" ) );

            AddExporter( MarkdownExporter.GitHub );
            AddValidator( JitOptimizationsValidator.DontFailOnError );
            AddLogger( ConsoleLogger.Default );

            AddColumnProvider(
                DefaultColumnProviders.Job,
                DefaultColumnProviders.Params,
                DefaultColumnProviders.Descriptor,
                DefaultColumnProviders.Metrics,
                DefaultColumnProviders.Statistics
            );

            AddDiagnoser( MemoryDiagnoser.Default );

            // Delta columns — time and allocation ratios vs each compiler baseline
            AddColumn( new RatioToColumn( "_System" ) );
            AddColumn( new RatioToColumn( "_Fec" ) );
            AddColumn( new RatioToColumn( "_System", isAlloc: true ) );
            AddColumn( new RatioToColumn( "_Fec", isAlloc: true ) );

            AddLogicalGroupRules( BenchmarkLogicalGroupRule.ByCategory );

            Orderer = new DefaultOrderer( SummaryOrderPolicy.Declared );
            ArtifactsPath = "benchmark";
        }
    }

    /// <summary>
    /// As <see cref="Config"/>, but one invocation per iteration so an [IterationSetup] can
    /// rebuild state the measured call consumes.
    /// </summary>
    /// <remarks>
    /// A coroutine block caches its reduction, so a second compile of the same instance is
    /// not a compile at all. Measuring compilation means handing each invocation a tree that
    /// has never been reduced, which rules out the usual many-invocations-per-iteration
    /// timing. Iteration count is raised to make up for the smaller sample per iteration.
    /// </remarks>
    public class ColdConfig() : Config( Job.ShortRun
        .WithInvocationCount( 1 )
        .WithUnrollFactor( 1 )
        .WithWarmupCount( 5 )
        .WithIterationCount( 25 ) );
}
