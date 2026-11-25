using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Environments;
using BenchmarkDotNet.Exporters;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Loggers;
using BenchmarkDotNet.Order;
using BenchmarkDotNet.Toolchains;
using BenchmarkDotNet.Toolchains.CsProj;
using BenchmarkDotNet.Toolchains.DotNetCli;
using BenchmarkDotNet.Validators;

namespace Hyperbee.Expressions.Compiler.Benchmarks;

public class BenchmarkConfig
{
    public class Config : ManualConfig
    {
        public Config() : this( Job.ShortRun )
        {
        }

        // BenchmarkDotNet 0.15.8 is the latest stable and has no CoreRuntime constant past
        // Core10_0. CoreRuntime.CreateForNewVersion looks like the answer but is not: it
        // yields RuntimeMoniker.NotRecognized, and DotNetSdkValidator.ValidateCoreSdks then
        // throws NotImplementedException from GetRuntimeVersion before anything runs. A
        // toolchain pinned to the moniker is the supported route for a framework the package
        // does not know yet; the job's own Runtime is left alone so the validator stays happy.
        private static readonly IToolchain Net11 =
            CsProjCoreToolchain.From( new NetCoreAppSettings( "net11.0", null, ".NET 11" ) );

        protected Config( Job job )
        {
            // Both runtimes in one run: absolute times drift between runs, so a
            // net10-vs-net11 comparison is only meaningful measured side by side.
            AddJob( job
                .WithRuntime( CoreRuntime.Core10_0 )
                .WithId( ".NET 10" ) );

            AddJob( job
                .WithToolchain( Net11 )
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
    /// As <see cref="Config"/>, but with enough iterations to resolve a small difference.
    /// </summary>
    /// <remarks>
    /// ShortRun takes three iterations, which cannot separate two means that differ by less
    /// than roughly a fifth -- two runs of the same pair disagreed on which was faster. Tiers
    /// that exist to compare two close implementations need a real sample.
    /// </remarks>
    public class StableConfig() : Config( Job.ShortRun
        .WithWarmupCount( 8 )
        .WithIterationCount( 20 ) );

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
