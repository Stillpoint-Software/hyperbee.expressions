using System.Reflection;
using System.Reflection.Emit;

namespace ILComparisonTool;

internal readonly record struct IlInstruction( int Offset, OpCode OpCode, object? Operand );

internal static class ILReader
{
    public static IReadOnlyList<IlInstruction> ReadIl( MethodInfo method )
    {
        ArgumentNullException.ThrowIfNull( method );

        var body = method.GetMethodBody();
        if ( body is null )
            throw new InvalidOperationException( "No method body." );

        var il = body.GetILAsByteArray();
        if ( il is null || il.Length == 0 )
            return [];

        var module = method.Module;
        var instructions = new List<IlInstruction>( il.Length / 2 );

        var i = 0;
        while ( i < il.Length )
        {
            var offset = i;
            var op = ReadOpCode( il, ref i );
            var operand = ReadOperand( op, il, ref i, module );
            instructions.Add( new IlInstruction( offset, op, operand ) );
        }

        return instructions;
    }

    static OpCode ReadOpCode( byte[] il, ref int i )
    {
        var b = il[i++];

        if ( b != 0xFE )
            return SingleByteOpCodes[b];

        var b2 = il[i++];
        return MultiByteOpCodes[b2];
    }

    static object? ReadOperand( OpCode op, byte[] il, ref int i, Module module )
    {
        switch ( op.OperandType )
        {
            case OperandType.InlineNone:
                return null;

            case OperandType.ShortInlineI:
                return (sbyte) il[i++];

            case OperandType.InlineI:
                var i32 = BitConverter.ToInt32( il, i );
                i += 4;
                return i32;

            case OperandType.InlineI8:
                var i64 = BitConverter.ToInt64( il, i );
                i += 8;
                return i64;

            case OperandType.ShortInlineR:
                var f32 = BitConverter.ToSingle( il, i );
                i += 4;
                return f32;

            case OperandType.InlineR:
                var f64 = BitConverter.ToDouble( il, i );
                i += 8;
                return f64;

            case OperandType.ShortInlineBrTarget:
                var rel8 = (sbyte) il[i++];
                return i + rel8; // absolute target offset

            case OperandType.InlineBrTarget:
                var rel32 = BitConverter.ToInt32( il, i );
                i += 4;
                return i + rel32;

            case OperandType.ShortInlineVar:
                return (int) il[i++];

            case OperandType.InlineVar:
                var u16 = BitConverter.ToUInt16( il, i );
                i += 2;
                return (int) u16;

            case OperandType.InlineString:
                var mdStr = BitConverter.ToInt32( il, i );
                i += 4;
                try { return module.ResolveString( mdStr ); }
                catch { return $"token:0x{mdStr:X8}"; }

            case OperandType.InlineMethod:
            case OperandType.InlineField:
            case OperandType.InlineType:
            case OperandType.InlineTok:
                var token = BitConverter.ToInt32( il, i );
                i += 4;
                try { return module.ResolveMember( token ); }
                catch { return $"token:0x{token:X8}"; }

            case OperandType.InlineSig:
                var sigToken = BitConverter.ToInt32( il, i );
                i += 4;
                return $"sig:0x{sigToken:X8}";

            case OperandType.InlineSwitch:
                var count = BitConverter.ToInt32( il, i );
                i += 4;
                var baseOffset = i + count * 4;
                var targets = new int[count];
                for ( var n = 0; n < count; n++ )
                {
                    var delta = BitConverter.ToInt32( il, i );
                    i += 4;
                    targets[n] = baseOffset + delta;
                }
                return targets;

            default:
                throw new NotSupportedException( $"Unsupported operand type: {op.OperandType}" );
        }
    }

    static readonly OpCode[] SingleByteOpCodes = BuildSingleByteOpCodes();
    static readonly OpCode[] MultiByteOpCodes = BuildMultiByteOpCodes();

    static OpCode[] BuildSingleByteOpCodes()
    {
        var arr = new OpCode[0x100];
        foreach ( var f in typeof( OpCodes ).GetFields( BindingFlags.Public | BindingFlags.Static ) )
        {
            if ( f.GetValue( null ) is not OpCode op )
                continue;

            var v = (ushort) op.Value;
            if ( v <= 0xFF )
                arr[v] = op;
        }
        return arr;
    }

    static OpCode[] BuildMultiByteOpCodes()
    {
        var arr = new OpCode[0x100];
        foreach ( var f in typeof( OpCodes ).GetFields( BindingFlags.Public | BindingFlags.Static ) )
        {
            if ( f.GetValue( null ) is not OpCode op )
                continue;

            var v = (ushort) op.Value;
            if ( (v & 0xFF00) == 0xFE00 )
                arr[v & 0xFF] = op;
        }
        return arr;
    }
}
