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
        public Config()
        {
            AddJob( Job.ShortRun
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
}
