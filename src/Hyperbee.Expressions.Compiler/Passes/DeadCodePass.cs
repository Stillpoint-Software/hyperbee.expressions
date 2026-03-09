using Hyperbee.Expressions.Compiler.IR;

namespace Hyperbee.Expressions.Compiler.Passes;

/// <summary>
/// Removes unreachable instructions that follow unconditional control transfers
/// (Branch, Ret, Throw, Leave, Rethrow) up to the next Label or exception block marker.
/// </summary>
public static class DeadCodePass
{
    /// <summary>
    /// Run the dead code elimination pass. Returns true if any instructions were removed.
    /// </summary>
    public static bool Run( IRBuilder ir )
    {
        var modified = false;
        var instructions = ir.Instructions;

        for ( var i = 0; i < instructions.Count - 1; i++ )
        {
            var op = instructions[i].Op;

            if ( op is not (IROp.Branch or IROp.Ret or IROp.Throw or IROp.Leave or IROp.Rethrow) )
                continue;

            // Remove all instructions between this terminator and the next
            // label or exception block boundary.
            var j = i + 1;
            while ( j < instructions.Count && !IsBlockBoundary( instructions[j].Op ) )
            {
                j++;
            }

            if ( j <= i + 1 )
                continue;

            // Remove instructions from i+1 to j-1 (exclusive of j)
            var removeCount = j - (i + 1);
            for ( var k = 0; k < removeCount; k++ )
            {
                ir.RemoveAt( i + 1 );
            }

            modified = true;
        }

        return modified;
    }

    private static bool IsBlockBoundary( IROp op )
    {
        return op is IROp.Label
            or IROp.BeginTry
            or IROp.BeginCatch
            or IROp.BeginFilter
            or IROp.BeginFilteredCatch
            or IROp.BeginFinally
            or IROp.BeginFault
            or IROp.EndTryCatch;
    }
}
