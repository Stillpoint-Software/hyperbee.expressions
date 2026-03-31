using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Reports;
using BenchmarkDotNet.Running;

namespace Hyperbee.Expressions.Compiler.Benchmarks;

/// <summary>
/// Custom BenchmarkDotNet column that shows the ratio of this benchmark's time or
/// per-operation allocation versus a named compiler baseline, matched by method name suffix.
///
/// Example: For baselineSuffix "_System", "Loop_Hyperbee" is compared to "Loop_System"
/// within the same benchmark class, giving a clean "vs System" ratio per tier.
/// </summary>
public sealed class RatioToColumn : IColumn
{
    private static readonly string[] CompilerSuffixes = ["_System", "_Fec", "_Hyperbee"];

    private readonly string _baselineSuffix;
    private readonly bool _isAlloc;

    public string Id => $"RatioTo{_baselineSuffix.TrimStart( '_' )}{(_isAlloc ? "Alloc" : "")}";
    public string ColumnName { get; }
    public bool AlwaysShow => true;
    public ColumnCategory Category => ColumnCategory.Custom;
    public int PriorityInCategory => _isAlloc ? 1 : 0;
    public bool IsNumeric => true;
    public UnitType UnitType => UnitType.Dimensionless;
    public string Legend => $"Ratio of {(_isAlloc ? "bytes allocated/op" : "mean time")} vs {_baselineSuffix.TrimStart( '_' )} compiler";

    public RatioToColumn( string baselineSuffix, bool isAlloc = false )
    {
        _baselineSuffix = baselineSuffix;
        _isAlloc = isAlloc;
        ColumnName = isAlloc
            ? $"Alloc vs {baselineSuffix.TrimStart( '_' )}"
            : $"vs {baselineSuffix.TrimStart( '_' )}";
    }

    public bool IsAvailable( Summary summary ) => true;
    public bool IsDefault( Summary summary, BenchmarkCase benchmarkCase ) => false;

    public string GetValue( Summary summary, BenchmarkCase benchmarkCase )
        => GetValue( summary, benchmarkCase, SummaryStyle.Default );

    public string GetValue( Summary summary, BenchmarkCase benchmarkCase, SummaryStyle style )
    {
        var methodName = benchmarkCase.Descriptor.WorkloadMethod.Name;
        var tierName = StripCompilerSuffix( methodName );
        if ( tierName == null ) return "N/A";

        // Find the matching baseline benchmark in the same class
        var baselineMethodName = tierName + _baselineSuffix;
        var baselineCase = summary.BenchmarksCases.FirstOrDefault( c =>
            c.Descriptor.WorkloadMethod.Name == baselineMethodName &&
            c.Descriptor.Type == benchmarkCase.Descriptor.Type );

        if ( baselineCase == null ) return "N/A";

        var report = summary[benchmarkCase];
        var baselineReport = summary[baselineCase];

        if ( report?.ResultStatistics == null || baselineReport?.ResultStatistics == null )
            return "?";

        if ( _isAlloc )
        {
            var alloc = report.GcStats.GetBytesAllocatedPerOperation( benchmarkCase ) ?? 0;
            var baselineAlloc = baselineReport.GcStats.GetBytesAllocatedPerOperation( baselineCase ) ?? 0;
            if ( baselineAlloc == 0 ) return alloc == 0 ? "1.00x" : "∞";
            return $"{(double) alloc / baselineAlloc:F2}x";
        }
        else
        {
            var mean = report.ResultStatistics.Mean;
            var baselineMean = baselineReport.ResultStatistics.Mean;
            if ( baselineMean <= 0 ) return "N/A";
            return $"{mean / baselineMean:F2}x";
        }
    }

    private static string? StripCompilerSuffix( string methodName )
    {
        foreach ( var suffix in CompilerSuffixes )
        {
            if ( methodName.EndsWith( suffix ) )
                return methodName[..^suffix.Length];
        }
        return null;
    }
}
