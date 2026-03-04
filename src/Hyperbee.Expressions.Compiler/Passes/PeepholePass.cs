using Hyperbee.Expressions.Compiler.IR;

namespace Hyperbee.Expressions.Compiler.Passes;

/// <summary>
/// Small-window pattern matching over the IR instruction list.
/// Removes redundant load/store pairs, dead loads, identity box/unbox
/// round-trips, and branches to the immediately following label.
/// </summary>
public static class PeepholePass
{
    /// <summary>
    /// Run the peephole optimization pass. Returns true if any modifications were made.
    /// </summary>
    public static bool Run( IRBuilder ir )
    {
        var modified = false;

        for ( var i = 0; i < ir.Instructions.Count - 1; i++ )
        {
            var a = ir.Instructions[i];
            var b = ir.Instructions[i + 1];

            // Pattern 1: StoreLocal X; LoadLocal X -> Dup; StoreLocal X
            // Saves an unnecessary reload from the local variable slot.
            if ( a.Op == IROp.StoreLocal && b.Op == IROp.LoadLocal && a.Operand == b.Operand )
            {
                ir.InsertAt( i, new IRInstruction( IROp.Dup ) );
                ir.RemoveAt( i + 2 ); // remove the LoadLocal
                modified = true;
                continue;
            }

            // Pattern 2: LoadConst; Pop -> remove both (dead constant load)
            if ( a.Op == IROp.LoadConst && b.Op == IROp.Pop )
            {
                ir.RemoveAt( i );
                ir.RemoveAt( i ); // b is now at position i
                i--;
                modified = true;
                continue;
            }

            // Pattern 3: LoadNull; Pop -> remove both (dead null load)
            if ( a.Op == IROp.LoadNull && b.Op == IROp.Pop )
            {
                ir.RemoveAt( i );
                ir.RemoveAt( i );
                i--;
                modified = true;
                continue;
            }

            // Pattern 4: Box T; UnboxAny T -> nop (identity roundtrip when same operand)
            if ( a.Op == IROp.Box && b.Op == IROp.UnboxAny && a.Operand == b.Operand )
            {
                ir.RemoveAt( i );
                ir.RemoveAt( i );
                i--;
                modified = true;
                continue;
            }

            // Pattern 5: LoadLocal X; Pop -> remove both (dead local load)
            if ( a.Op == IROp.LoadLocal && b.Op == IROp.Pop )
            {
                ir.RemoveAt( i );
                ir.RemoveAt( i );
                i--;
                modified = true;
                continue;
            }

            // Pattern 6: Dup; Pop -> remove both (pointless duplicate)
            if ( a.Op == IROp.Dup && b.Op == IROp.Pop )
            {
                ir.RemoveAt( i );
                ir.RemoveAt( i );
                i--;
                modified = true;
                continue;
            }

            // Pattern 7: Branch to next instruction -> remove (branch to fallthrough)
            if ( a.Op == IROp.Branch && i + 1 < ir.Instructions.Count
                && b.Op == IROp.Label && b.Operand == a.Operand )
            {
                ir.RemoveAt( i );
                i--;
                modified = true;
                continue;
            }

            // Pattern 8: Dup; StoreLocal X; Pop -> StoreLocal X
            // When an assignment's value is duplicated for "returns a value" semantics
            // but the caller immediately discards it (e.g. block in void context).
            if ( a.Op == IROp.Dup && b.Op == IROp.StoreLocal
                && i + 2 < ir.Instructions.Count
                && ir.Instructions[i + 2].Op == IROp.Pop )
            {
                ir.RemoveAt( i + 2 ); // remove Pop
                ir.RemoveAt( i );     // remove Dup (StoreLocal shifts to i)
                i--;
                modified = true;
                continue;
            }

            // Pattern 9: Comparison + BranchTrue/BranchFalse -> fused branch opcode
            // Maps 2-instruction compare-then-branch into a single CIL fused branch.
            var fused = TryFuseComparisonBranch( a.Op, b.Op );
            if ( fused != IROp.Nop )
            {
                ir.ReplaceAt( i, new IRInstruction( fused, b.Operand ) );
                ir.RemoveAt( i + 1 );
                modified = true;
                continue;
            }
        }

        return modified;
    }

    /// <summary>
    /// Returns the fused IROp for a comparison followed by a conditional branch,
    /// or IROp.Nop if no fusion is possible.
    /// </summary>
    private static IROp TryFuseComparisonBranch( IROp compare, IROp branch )
    {
        return (compare, branch) switch
        {
            (IROp.Ceq, IROp.BranchTrue)    => IROp.BranchEqual,
            (IROp.Ceq, IROp.BranchFalse)   => IROp.BranchNotEqual,
            (IROp.Clt, IROp.BranchTrue)    => IROp.BranchLessThan,
            (IROp.CltUn, IROp.BranchTrue)  => IROp.BranchLessThanUn,
            (IROp.Cgt, IROp.BranchTrue)    => IROp.BranchGreaterThan,
            (IROp.CgtUn, IROp.BranchTrue)  => IROp.BranchGreaterThanUn,
            (IROp.Clt, IROp.BranchFalse)   => IROp.BranchGreaterEqual,
            (IROp.CltUn, IROp.BranchFalse) => IROp.BranchGreaterEqualUn,
            (IROp.Cgt, IROp.BranchFalse)   => IROp.BranchLessEqual,
            (IROp.CgtUn, IROp.BranchFalse) => IROp.BranchLessEqualUn,
            _ => IROp.Nop
        };
    }
}
