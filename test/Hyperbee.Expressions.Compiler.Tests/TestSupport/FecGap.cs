using System;

namespace Hyperbee.Expressions.Compiler.Tests.TestSupport;

/// <summary>
/// Records an expression pattern that FastExpressionCompiler does not handle,
/// graded by what a caller actually experiences.
/// </summary>
/// <remarks>
/// <para>
/// The grade is the point. A decline and a miscompile are not the same defect:
/// when FEC declines, <c>CompileFast()</c> falls back to <c>Expression.Compile()</c>
/// and the caller gets correct results with no speedup, which is a performance
/// footnote. When FEC miscompiles, it believes it succeeded, so no fallback fires
/// and the caller gets a wrong answer, an exception, or a dead process. Recording
/// both as one undifferentiated "unsupported" hides the only distinction that
/// matters when deciding whether to use FEC.
/// </para>
/// <para>
/// Set <c>HYPERBEE_FEC_RECHECK=1</c> to run tiers 1 to 3 anyway. That is how you
/// learn which gaps an FEC release has closed, without hand-editing guards:
/// a recorded gap that now passes can have its guard deleted. Tier 4 stays
/// skipped even then, because it takes the test host with it; use
/// <c>HYPERBEE_FEC_RECHECK=all</c> to include it, outside CI.
/// </para>
/// <para>
/// This exists because the guards went stale silently. Checked against FEC 6.0.0,
/// eight recorded gaps had already been fixed and twenty-three more were present
/// but unrecorded, the latter hidden by <c>CompileFast()</c>'s fallback passing
/// them off as successes. See ExpressionCompilerExtensions for that trap.
/// </para>
/// </remarks>
internal static class FecGap
{
    private static readonly string? Mode =
        Environment.GetEnvironmentVariable( "HYPERBEE_FEC_RECHECK" );

    private static readonly bool RecheckSafe = Mode is "1" or "all";
    private static readonly bool RecheckAll = Mode is "all";

    /// <summary>
    /// Tier 1. FEC returns null, so <c>CompileFast()</c> falls back to the System
    /// compiler. Correct results, no speedup. Safe to call.
    /// </summary>
    public static void Declines( string note )
        => Record( 1, "declines; CompileFast() falls back to the System compiler", note, RecheckSafe );

    /// <summary>
    /// Tier 2. FEC emits invalid IL. The runtime rejects it at execution with
    /// InvalidProgramException. No fallback fires.
    /// </summary>
    public static void MiscompilesThrows( string note )
        => Record( 2, "miscompiles; throws InvalidProgramException at execution", note, RecheckSafe );

    /// <summary>
    /// Tier 3. FEC emits IL that runs and returns the wrong value. No fallback, no
    /// exception, nothing for a caller to notice.
    /// </summary>
    public static void MiscompilesSilently( string note )
        => Record( 3, "miscompiles; returns the wrong value silently", note, RecheckSafe );

    /// <summary>
    /// Tier 4. FEC emits IL that faults the process. Uncatchable, and it aborts the
    /// test run, so this one stays skipped even under HYPERBEE_FEC_RECHECK=1.
    /// </summary>
    public static void AbortsProcess( string note )
        => Record( 4, "miscompiles; faults the process, uncatchable", note, RecheckAll );

    private static void Record( int tier, string effect, string note, bool recheck )
    {
        if ( recheck )
            return;

        Assert.Inconclusive( $"FEC gap tier {tier} -- {effect}. {note}" );
    }
}
