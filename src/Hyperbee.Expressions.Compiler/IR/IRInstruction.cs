using System.Runtime.InteropServices;

namespace Hyperbee.Expressions.Compiler.IR;

/// <summary>
/// A single IR instruction. Value type for cache-friendly storage in lists.
/// </summary>
[StructLayout( LayoutKind.Sequential )]
public readonly struct IRInstruction
{
    /// <summary>The operation.</summary>
    public readonly IROp Op;

    /// <summary>
    /// Operand whose meaning depends on Op:
    ///   LoadConst       -> index into operand table
    ///   LoadLocal       -> local variable index
    ///   StoreLocal      -> local variable index
    ///   LoadArg         -> argument index
    ///   Call/CallVirt   -> index into operand table (MethodInfo)
    ///   NewObj          -> index into operand table (ConstructorInfo)
    ///   Branch*         -> label index
    ///   LoadField       -> index into operand table (FieldInfo)
    ///   Box/Unbox       -> index into operand table (Type)
    ///   Convert         -> index into operand table (Type)
    ///   CastClass/IsInst-> index into operand table (Type)
    /// </summary>
    public readonly int Operand;

    public IRInstruction( IROp op, int operand = 0 )
    {
        Op = op;
        Operand = operand;
    }

    public override string ToString() => Operand != 0 ? $"{Op} {Operand}" : $"{Op}";
}
