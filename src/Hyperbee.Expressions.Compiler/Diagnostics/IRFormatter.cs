using System.Reflection;
using System.Text;
using Hyperbee.Expressions.Compiler.IR;

namespace Hyperbee.Expressions.Compiler.Diagnostics;

/// <summary>
/// Formats an <see cref="IRBuilder"/> into a human-readable IR listing.
/// </summary>
public static class IRFormatter
{
    /// <summary>
    /// Formats the instruction stream of <paramref name="ir"/> into a multi-line string.
    /// </summary>
    public static string Format( IRBuilder ir )
    {
        var sb = new StringBuilder();
        var instructions = ir.Instructions;
        var operands = ir.Operands;
        var locals = ir.Locals;
        var labels = ir.Labels;

        for ( var i = 0; i < instructions.Count; i++ )
        {
            var instr = instructions[i];
            var operandText = FormatOperand( instr, operands, locals, labels );

            sb.Append( $"{i:D4}  {instr.Op,-22}" );
            if ( operandText.Length > 0 )
                sb.Append( $"  {operandText}" );
            sb.AppendLine();
        }

        if ( locals.Count > 0 )
        {
            sb.AppendLine();
            sb.AppendLine( "Locals:" );
            for ( var i = 0; i < locals.Count; i++ )
            {
                var local = locals[i];
                sb.AppendLine( $"  [{i}] {local.Type.Name} {local.Name ?? $"local_{i}"} (scope {local.ScopeDepth})" );
            }
        }

        return sb.ToString();
    }

    private static string FormatOperand(
        IRInstruction instr,
        IReadOnlyList<object> operands,
        IReadOnlyList<LocalInfo> locals,
        IReadOnlyList<LabelInfo> labels )
    {
        switch ( instr.Op )
        {
            case IROp.LoadConst:
            {
                var obj = operands[instr.Operand];
                return obj switch
                {
                    MethodInfo m => $"[{instr.Operand}] {m.DeclaringType?.Name}.{m.Name}()",
                    ConstructorInfo c => $"[{instr.Operand}] new {c.DeclaringType?.Name}()",
                    FieldInfo f => $"[{instr.Operand}] {f.DeclaringType?.Name}.{f.Name}",
                    Type t => $"[{instr.Operand}] typeof({t.Name})",
                    Delegate d => $"[{instr.Operand}] delegate<{d.GetType().Name}>",
                    _ => $"[{instr.Operand}] {obj}"
                };
            }

            case IROp.Call:
            case IROp.CallVirt:
            case IROp.Constrained:
            {
                var obj = operands[instr.Operand];
                return obj switch
                {
                    MethodInfo m => $"[{instr.Operand}] {m.DeclaringType?.Name}.{m.Name}()",
                    Type t => $"[{instr.Operand}] typeof({t.Name})",
                    _ => $"[{instr.Operand}] {obj}"
                };
            }

            case IROp.NewObj:
            {
                var obj = operands[instr.Operand];
                return obj is ConstructorInfo c2
                    ? $"[{instr.Operand}] new {c2.DeclaringType?.Name}()"
                    : $"[{instr.Operand}] {obj}";
            }

            case IROp.LoadField:
            case IROp.StoreField:
            case IROp.LoadStaticField:
            case IROp.StoreStaticField:
            case IROp.LoadFieldAddress:
            {
                var obj = operands[instr.Operand];
                return obj is FieldInfo f2
                    ? $"[{instr.Operand}] {f2.DeclaringType?.Name}.{f2.Name}"
                    : $"[{instr.Operand}] {obj}";
            }

            case IROp.Box:
            case IROp.Unbox:
            case IROp.UnboxAny:
            case IROp.CastClass:
            case IROp.IsInst:
            case IROp.Convert:
            case IROp.ConvertChecked:
            case IROp.ConvertCheckedUn:
            case IROp.InitObj:
            case IROp.NewArray:
            case IROp.LoadToken:
            case IROp.LoadElementAddress:
            {
                var obj = operands[instr.Operand];
                return obj is Type t2
                    ? $"[{instr.Operand}] {t2.Name}"
                    : $"[{instr.Operand}] {obj}";
            }

            case IROp.LoadLocal:
            case IROp.StoreLocal:
            case IROp.LoadAddress:
            {
                if ( instr.Operand < locals.Count )
                {
                    var local = locals[instr.Operand];
                    return $"[{instr.Operand}] {local.Name ?? $"local_{instr.Operand}"} ({local.Type.Name})";
                }

                return $"[{instr.Operand}]";
            }

            case IROp.LoadArg:
            case IROp.StoreArg:
            case IROp.LoadArgAddress:
                return $"{instr.Operand}";

            case IROp.Branch:
            case IROp.BranchTrue:
            case IROp.BranchFalse:
            case IROp.Leave:
            case IROp.Label:
            {
                var labelIdx = instr.Operand;
                var targetInstr = labelIdx < labels.Count ? labels[labelIdx].InstructionIndex : -1;
                return $"L{labelIdx:D4} -> {(targetInstr >= 0 ? targetInstr.ToString( "D4" ) : "?")}";
            }

            case IROp.Switch:
            {
                var obj = operands[instr.Operand];
                return obj is int[] cases
                    ? $"[{instr.Operand}] cases:[{string.Join( ", ", cases.Select( c => $"L{c:D4}" ) )}]"
                    : $"[{instr.Operand}] {obj}";
            }

            case IROp.BeginScope:
            case IROp.EndScope:
                return $"scope:{instr.Operand}";

            case IROp.Nop:
            case IROp.Ret:
            case IROp.Pop:
            case IROp.Dup:
            case IROp.LoadNull:
            case IROp.Throw:
            case IROp.Rethrow:
            case IROp.BeginTry:
            case IROp.BeginCatch:
            case IROp.BeginFilter:
            case IROp.BeginFilteredCatch:
            case IROp.BeginFinally:
            case IROp.BeginFault:
            case IROp.EndTryCatch:
            case IROp.LoadArrayLength:
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
            case IROp.Negate:
            case IROp.NegateChecked:
            case IROp.And:
            case IROp.Or:
            case IROp.Xor:
            case IROp.Not:
            case IROp.LeftShift:
            case IROp.RightShift:
            case IROp.RightShiftUn:
            case IROp.Ceq:
            case IROp.Clt:
            case IROp.Cgt:
            case IROp.CltUn:
            case IROp.CgtUn:
            case IROp.StoreElement:
            case IROp.LoadElement:
            default:
                return instr.Operand != 0 ? $"{instr.Operand}" : string.Empty;
        }
    }
}
