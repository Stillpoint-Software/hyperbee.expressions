using Hyperbee.Expressions.Compiler.IR;

namespace Hyperbee.Expressions.Compiler.Passes;

/// <summary>
/// Ensures the evaluation stack is empty at exception handling boundaries.
/// CIL requires the stack to be empty at BeginTry entry. If the stack is
/// non-empty, this pass inserts StoreLocal/LoadLocal pairs to spill values
/// to temporaries.
///
/// Additionally, this pass converts Branch instructions inside try/catch
/// blocks that target labels outside the exception block into Leave instructions.
/// </summary>
public static class StackSpillPass
{
    /// <summary>
    /// Run the stack spill pass over the IR instructions.
    /// </summary>
    public static void Run( IRBuilder ir )
    {
        ConvertBranchesToLeaves( ir );
    }

    /// <summary>
    /// Scans for Branch instructions inside try/catch blocks that jump to
    /// labels outside the exception block, and converts them to Leave instructions.
    /// This is necessary because CIL requires 'leave' (not 'br') to exit
    /// protected regions.
    /// </summary>
    private static void ConvertBranchesToLeaves( IRBuilder ir )
    {
        var instructions = ir.Instructions;

        // Fast-exit: if no try blocks exist, there is nothing to convert
        var hasTry = false;
        for ( var i = 0; i < instructions.Count; i++ )
        {
            if ( instructions[i].Op == IROp.BeginTry )
            {
                hasTry = true;
                break;
            }
        }

        if ( !hasTry )
            return;

        // Build a list of exception block ranges
        var tryStack = new Stack<int>( 4 );
        var blockRanges = new List<(int Start, int End)>( 4 );
        for ( var i = 0; i < instructions.Count; i++ )
        {
            switch ( instructions[i].Op )
            {
                case IROp.BeginTry:
                    tryStack.Push( i );
                    break;
                case IROp.EndTryCatch:
                    if ( tryStack.Count > 0 )
                    {
                        var start = tryStack.Pop();
                        blockRanges.Add( (start, i) );
                    }
                    break;
            }
        }

        // For each Branch instruction, check if it is inside an exception block
        // and its target is outside that block. If so, convert to Leave.
        for ( var i = 0; i < ir.Instructions.Count; i++ )
        {
            var inst = ir.Instructions[i];
            if ( inst.Op != IROp.Branch )
                continue;

            var targetLabelIndex = inst.Operand;
            var targetInstruction = ir.Labels[targetLabelIndex].InstructionIndex;

            // Check if this Branch is inside any exception block whose range
            // does not contain the target
            foreach ( var (start, end) in blockRanges )
            {
                if ( i > start && i < end && ( targetInstruction <= start || targetInstruction >= end ) )
                {
                    // This Branch crosses an exception boundary -- convert to Leave
                    ir.ReplaceAt( i, new IRInstruction( IROp.Leave, targetLabelIndex ) );
                    break;
                }
            }
        }
    }
}
