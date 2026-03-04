using System.Reflection;
using System.Reflection.Emit;
using System.Text;

namespace ILComparisonTool;

/// <summary>
/// Formats raw IL bytes into readable text without relying on Module.ResolveMember.
/// Works with DynamicMethod IL bytes where metadata resolution isn't available.
/// For token-based operands, resolves through the delegate's DynamicMethod resolver.
/// </summary>
internal static class RawILFormatter
{
    public static string Format( byte[] ilBytes, Delegate del )
    {
        var sb = new StringBuilder();
        var method = del.Method;
        var module = method.Module;

        // Try to get max stack info
        var maxStack = DynamicMethodILExtractor.TryGetMaxStack( del );
        if ( maxStack.HasValue )
            sb.AppendLine( $".maxstack {maxStack.Value}" );

        sb.AppendLine();

        var i = 0;
        var instructionCount = 0;
        while ( i < ilBytes.Length )
        {
            var offset = i;
            var op = ReadOpCode( ilBytes, ref i );
            var operandText = ReadAndFormatOperand( op, ilBytes, ref i, module );

            sb.AppendLine( $"{offset:X4}: {op.Name,-16} {operandText}".TrimEnd() );
            instructionCount++;
        }

        return sb.ToString();
    }

    static OpCode ReadOpCode( byte[] il, ref int i )
    {
        var b = il[i++];
        if ( b != 0xFE )
            return SingleByteOpCodes[b];

        var b2 = il[i++];
        return MultiByteOpCodes[b2];
    }

    static string ReadAndFormatOperand( OpCode op, byte[] il, ref int i, Module module )
    {
        switch ( op.OperandType )
        {
            case OperandType.InlineNone:
                return "";

            case OperandType.ShortInlineI:
                var si8 = (sbyte) il[i++];
                return si8.ToString();

            case OperandType.InlineI:
                var i32 = BitConverter.ToInt32( il, i );
                i += 4;
                return i32.ToString();

            case OperandType.InlineI8:
                var i64 = BitConverter.ToInt64( il, i );
                i += 8;
                return $"0x{i64:X}";

            case OperandType.ShortInlineR:
                var f32 = BitConverter.ToSingle( il, i );
                i += 4;
                return f32.ToString( "G" );

            case OperandType.InlineR:
                var f64 = BitConverter.ToDouble( il, i );
                i += 8;
                return f64.ToString( "G" );

            case OperandType.ShortInlineBrTarget:
                var rel8 = (sbyte) il[i++];
                var target8 = i + rel8;
                return $"IL_{target8:X4}";

            case OperandType.InlineBrTarget:
                var rel32 = BitConverter.ToInt32( il, i );
                i += 4;
                var target32 = i + rel32;
                return $"IL_{target32:X4}";

            case OperandType.ShortInlineVar:
                return il[i++].ToString();

            case OperandType.InlineVar:
                var u16 = BitConverter.ToUInt16( il, i );
                i += 2;
                return u16.ToString();

            case OperandType.InlineString:
                var strToken = BitConverter.ToInt32( il, i );
                i += 4;
                try
                {
                    return $"\"{module.ResolveString( strToken )}\"";
                }
                catch
                {
                    return $"string(0x{strToken:X8})";
                }

            case OperandType.InlineMethod:
                var methodToken = BitConverter.ToInt32( il, i );
                i += 4;
                try
                {
                    var member = module.ResolveMember( methodToken );
                    return FormatMember( member );
                }
                catch
                {
                    return $"method(0x{methodToken:X8})";
                }

            case OperandType.InlineField:
                var fieldToken = BitConverter.ToInt32( il, i );
                i += 4;
                try
                {
                    var member = module.ResolveMember( fieldToken );
                    return FormatMember( member );
                }
                catch
                {
                    return $"field(0x{fieldToken:X8})";
                }

            case OperandType.InlineType:
                var typeToken = BitConverter.ToInt32( il, i );
                i += 4;
                try
                {
                    var type = module.ResolveType( typeToken );
                    return FormatType( type );
                }
                catch
                {
                    return $"type(0x{typeToken:X8})";
                }

            case OperandType.InlineTok:
                var tok = BitConverter.ToInt32( il, i );
                i += 4;
                try
                {
                    var member = module.ResolveMember( tok );
                    return FormatMember( member );
                }
                catch
                {
                    return $"token(0x{tok:X8})";
                }

            case OperandType.InlineSig:
                var sigTok = BitConverter.ToInt32( il, i );
                i += 4;
                return $"sig(0x{sigTok:X8})";

            case OperandType.InlineSwitch:
                var count = BitConverter.ToInt32( il, i );
                i += 4;
                var baseOffset = i + count * 4;
                var targets = new string[count];
                for ( var n = 0; n < count; n++ )
                {
                    var delta = BitConverter.ToInt32( il, i );
                    i += 4;
                    targets[n] = $"IL_{baseOffset + delta:X4}";
                }
                return $"({string.Join( ", ", targets )})";

            default:
                return $"<?operand:{op.OperandType}>";
        }
    }

    static string FormatMember( MemberInfo? member )
    {
        return member switch
        {
            MethodInfo mi => $"{FormatType( mi.ReturnType )} {FormatType( mi.DeclaringType! )}::{mi.Name}({string.Join( ", ", mi.GetParameters().Select( p => FormatType( p.ParameterType ) ) )})",
            ConstructorInfo ci => $"void {FormatType( ci.DeclaringType! )}::.ctor({string.Join( ", ", ci.GetParameters().Select( p => FormatType( p.ParameterType ) ) )})",
            FieldInfo fi => $"{FormatType( fi.FieldType )} {FormatType( fi.DeclaringType! )}::{fi.Name}",
            Type t => FormatType( t ),
            _ => member?.ToString() ?? "null"
        };
    }

    static string FormatType( Type type )
    {
        if ( type == typeof( void ) ) return "void";
        if ( type == typeof( bool ) ) return "bool";
        if ( type == typeof( byte ) ) return "uint8";
        if ( type == typeof( sbyte ) ) return "int8";
        if ( type == typeof( short ) ) return "int16";
        if ( type == typeof( ushort ) ) return "uint16";
        if ( type == typeof( int ) ) return "int32";
        if ( type == typeof( uint ) ) return "uint32";
        if ( type == typeof( long ) ) return "int64";
        if ( type == typeof( ulong ) ) return "uint64";
        if ( type == typeof( float ) ) return "float32";
        if ( type == typeof( double ) ) return "float64";
        if ( type == typeof( string ) ) return "string";
        if ( type == typeof( object ) ) return "object";
        if ( type == typeof( char ) ) return "char";

        if ( type.IsArray )
            return FormatType( type.GetElementType()! ) + "[]";

        if ( type.IsByRef )
            return FormatType( type.GetElementType()! ) + "&";

        if ( type.IsGenericType )
        {
            var baseName = type.Name;
            var bt = baseName.IndexOf( '`' );
            if ( bt > 0 )
                baseName = baseName[..bt];
            var args = string.Join( ", ", type.GetGenericArguments().Select( FormatType ) );
            return $"{baseName}<{args}>";
        }

        return type.Name;
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
