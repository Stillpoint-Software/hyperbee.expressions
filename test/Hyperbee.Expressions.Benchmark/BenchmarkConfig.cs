using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Environments;
using BenchmarkDotNet.Exporters;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Loggers;
using BenchmarkDotNet.Order;
using BenchmarkDotNet.Toolchains.CsProj;
using BenchmarkDotNet.Toolchains.DotNetCli;
using BenchmarkDotNet.Validators;

namespace Hyperbee.Expressions.Benchmark;

public class BenchmarkConfig
{
    public class Config : ManualConfig
    {
        public Config()
        {
            // net8.0 and net9.0 both reached end of support on 2026-11-10 and this
            // repo no longer targets either, so they are no longer measured.
            AddJob( Job.ShortRun
                .WithRuntime( CoreRuntime.Core10_0 )
                .WithId( ".NET 10" ) );

            // BenchmarkDotNet 0.15.8 has no CoreRuntime constant past Core10_0, and
            // CoreRuntime.CreateForNewVersion trips DotNetSdkValidator. A toolchain pinned
            // to the moniker is the supported route; see the compiler benchmarks' config.
            AddJob( Job.ShortRun
                .WithToolchain( CsProjCoreToolchain.From( new NetCoreAppSettings( "net11.0", null, ".NET 11" ) ) )
                .WithId( ".NET 11" ) );


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

            AddLogicalGroupRules( BenchmarkLogicalGroupRule.ByCategory );

            Orderer = new DefaultOrderer( SummaryOrderPolicy.FastestToSlowest );
            ArtifactsPath = "benchmark";
        }
    }
}
