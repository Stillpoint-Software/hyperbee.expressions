using System.Diagnostics;
using Hyperbee.Expressions.Compiler.IR;

namespace Hyperbee.Expressions.Compiler.Passes;

/// <summary>
/// Validates the IR instruction stream for structural correctness.
/// Catches malformed IR before IL emission -- the same class of bugs
/// that cause InvalidProgramException in FEC.
///
/// Decorated with [Conditional("DEBUG")] so all call sites are stripped
/// in Release builds (zero cost).
/// </summary>
public static class IRValidator
{
    /// <summary>
    /// Validate the IR instruction stream. Throws <see cref="InvalidOperationException"/>
    /// on the first error found.
    /// </summary>
    [Conditional( "DEBUG" )]
    public static void Validate( IRBuilder ir, bool isVoidReturn = false )
    {
        ValidateCore( ir, isVoidReturn );
    }

    /// <summary>
    /// Validate the IR instruction stream regardless of build configuration.
    /// Use for opt-in production diagnostics.
    /// </summary>
    public static void ValidateAlways( IRBuilder ir, bool isVoidReturn = false )
    {
        ValidateCore( ir, isVoidReturn );
    }

    private static void ValidateCore( IRBuilder ir, bool isVoidReturn )
    {
        var instructions = ir.Instructions;
        var localCount = ir.Locals.Count;
        var labelCount = ir.Labels.Count;

        var stackDepth = 0;
        var tryDepth = 0;
        var referencedLabels = new HashSet<int>();
        var labelDepths = new Dictionary<int, int>(); // expected stack depth at each label

        for ( var i = 0; i < instructions.Count; i++ )
        {
            var inst = instructions[i];

            switch ( inst.Op )
            {
                // --- Stack pushes (+1) ---
                case IROp.LoadConst:
                case IROp.LoadNull:
                case IROp.LoadLocal:
                case IROp.LoadArg:
                case IROp.LoadStaticField:
                case IROp.Dup:
                case IROp.LoadAddress:
                case IROp.LoadArgAddress:
                case IROp.LoadToken:
                    stackDepth++;
                    break;

                // --- Stack pops (-1) ---
                case IROp.Pop:
                case IROp.StoreLocal:
                case IROp.StoreArg:
                case IROp.StoreStaticField:
                case IROp.Throw:
                    stackDepth--;
                    break;

                case IROp.BranchTrue:
                case IROp.BranchFalse:
                {
                    stackDepth--;
                    // Record expected depth at branch target (after pop)
                    referencedLabels.Add( inst.Operand );
                    labelDepths[inst.Operand] = stackDepth;
                    break;
                }

                // --- Stack neutral (pop+push) ---
                case IROp.Negate:
                case IROp.NegateChecked:
                case IROp.Not:
                case IROp.Convert:
                case IROp.ConvertChecked:
                case IROp.ConvertCheckedUn:
                case IROp.Box:
                case IROp.Unbox:
                case IROp.UnboxAny:
                case IROp.CastClass:
                case IROp.IsInst:
                case IROp.LoadArrayLength: // pop array, push length => net 0
                    break;

                // --- Binary ops: pop 2, push 1 => net -1 ---
                case IROp.Add:
                case IROp.Sub:
                case IROp.Mul:
                case IROp.Div:
                case IROp.Rem:
                case IROp.AddChecked:
                case IROp.SubChecked:
                case IROp.MulChecked:
                case IROp.AddCheckedUn:
                case IROp.SubCheckedUn:
                case IROp.MulCheckedUn:
                case IROp.And:
                case IROp.Or:
                case IROp.Xor:
                case IROp.LeftShift:
                case IROp.RightShift:
                case IROp.RightShiftUn:
                case IROp.Ceq:
                case IROp.Clt:
                case IROp.Cgt:
                case IROp.CltUn:
                case IROp.CgtUn:
                    stackDepth--;
                    break;

                // --- Field load/store (instance) ---
                case IROp.LoadField:
                    // pop instance, push value => net 0
                    break;

                case IROp.LoadFieldAddress:
                    // pop instance, push managed pointer to field => net 0
                    break;
                case IROp.StoreField:
                    // pop instance + value => -2
                    stackDepth -= 2;
                    break;

                // --- Array operations ---
                case IROp.LoadElement:
                case IROp.LoadElementAddress:
                    // pop array + index, push element/pointer => net -1
                    stackDepth--;
                    break;
                case IROp.StoreElement:
                    // pop array + index + value => -3
                    stackDepth -= 3;
                    break;
                case IROp.NewArray:
                    // pop size, push array => net 0
                    break;

                // --- Method calls ---
                // Track stack effects by inspecting method signatures from the operand table.
                case IROp.Call:
                case IROp.CallVirt:
                {
                    var method = (System.Reflection.MethodInfo) ir.Operands[inst.Operand];
                    stackDepth -= method.GetParameters().Length;
                    if ( !method.IsStatic )
                        stackDepth--; // pop instance
                    if ( method.ReturnType != typeof( void ) )
                        stackDepth++; // push return value
                    break;
                }

                case IROp.NewObj:
                {
                    var ctor = (System.Reflection.ConstructorInfo) ir.Operands[inst.Operand];
                    stackDepth -= ctor.GetParameters().Length; // pop args
                    stackDepth++; // push new instance
                    break;
                }

                case IROp.Constrained:
                    // Prefix only — no stack effect
                    break;

                // --- Control flow ---
                case IROp.Branch:
                    ValidateLabel( inst.Operand, labelCount, i, "Branch" );
                    referencedLabels.Add( inst.Operand );
                    labelDepths[inst.Operand] = stackDepth; // record depth at branch target
                    stackDepth = 0; // unreachable after unconditional branch
                    break;

                case IROp.Label:
                    ValidateLabel( inst.Operand, labelCount, i, "Label" );
                    // Restore the expected stack depth from branch sites; default 0 for unreferenced labels
                    stackDepth = labelDepths.GetValueOrDefault( inst.Operand, 0 );
                    break;

                case IROp.Leave:
                    ValidateLabel( inst.Operand, labelCount, i, "Leave" );
                    referencedLabels.Add( inst.Operand );
                    if ( tryDepth <= 0 )
                    {
                        throw new InvalidOperationException(
                            $"IR validation error at instruction {i}: " +
                            "Leave instruction outside of try/catch block." );
                    }
                    stackDepth = 0; // unreachable after Leave
                    break;

                // --- Exception handling ---
                case IROp.BeginTry:
                    if ( stackDepth != 0 )
                    {
                        throw new InvalidOperationException(
                            $"IR validation error at instruction {i}: " +
                            $"Stack must be empty at BeginTry, but depth is {stackDepth}." );
                    }
                    tryDepth++;
                    break;

                case IROp.BeginCatch:
                    stackDepth = 1; // catch pushes exception object
                    break;

                case IROp.BeginFilter:
                    stackDepth = 1; // filter pushes exception object
                    break;

                case IROp.BeginFilteredCatch:
                    stackDepth = 1; // filtered catch pushes exception object
                    break;

                case IROp.BeginFinally:
                case IROp.BeginFault:
                    stackDepth = 0;
                    break;

                case IROp.EndTryCatch:
                    tryDepth--;
                    stackDepth = 0;
                    break;

                case IROp.Rethrow:
                    stackDepth = 0; // unreachable after rethrow
                    break;

                // --- Local validation ---
                case IROp.InitObj:
                    break;

                // --- Ret ---
                case IROp.Ret:
                {
                    var expectedDepth = isVoidReturn ? 0 : 1;
                    if ( stackDepth != expectedDepth )
                    {
                        throw new InvalidOperationException(
                            $"IR validation error at instruction {i}: " +
                            $"Stack depth at Ret must be {expectedDepth} " +
                            $"(void={isVoidReturn}), but depth is {stackDepth}." );
                    }
                    stackDepth = 0;
                    break;
                }

                // --- Scope markers ---
                case IROp.BeginScope:
                case IROp.EndScope:
                case IROp.Nop:
                    break;

                // --- Switch ---
                case IROp.Switch:
                    stackDepth--; // pops the switch value
                    break;

            }

            // Validate local references
            if ( inst.Op is IROp.LoadLocal or IROp.StoreLocal or IROp.LoadAddress )
            {
                if ( inst.Operand < 0 || inst.Operand >= localCount )
                {
                    throw new InvalidOperationException(
                        $"IR validation error at instruction {i}: " +
                        $"{inst.Op} references local index {inst.Operand}, " +
                        $"but only {localCount} locals are declared." );
                }
            }

            // Validate branch label references
            if ( inst.Op is IROp.BranchTrue or IROp.BranchFalse )
            {
                ValidateLabel( inst.Operand, labelCount, i, inst.Op.ToString() );
                referencedLabels.Add( inst.Operand );
            }

            if ( stackDepth < 0 )
            {
                throw new InvalidOperationException(
                    $"IR validation error at instruction {i}: " +
                    $"Stack underflow (depth={stackDepth}) after {inst.Op}." );
            }
        }

        if ( tryDepth != 0 )
        {
            throw new InvalidOperationException(
                $"IR validation error: Unbalanced exception blocks. " +
                $"Try depth is {tryDepth} at end of instruction stream." );
        }
    }

    private static void ValidateLabel( int labelIndex, int labelCount, int instructionIndex, string context )
    {
        if ( labelIndex < 0 || labelIndex >= labelCount )
        {
            throw new InvalidOperationException(
                $"IR validation error at instruction {instructionIndex}: " +
                $"{context} references label index {labelIndex}, " +
                $"but only {labelCount} labels are defined." );
        }
    }
}
