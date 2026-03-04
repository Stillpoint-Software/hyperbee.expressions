using System.Reflection;
using System.Text;

namespace ILComparisonTool;

internal static class ILFormatter
{
    public static string Format( IReadOnlyList<IlInstruction> instructions, MethodInfo method )
    {
        var sb = new StringBuilder();
        var body = method.GetMethodBody();

        // Header: locals
        if ( body?.LocalVariables is { Count: > 0 } locals )
        {
            sb.AppendLine( ".locals (" );
            for ( var i = 0; i < locals.Count; i++ )
            {
                var pinned = locals[i].IsPinned ? " pinned" : "";
                sb.AppendLine( $"    [{i}] {FormatType( locals[i].LocalType )}{pinned}{(i < locals.Count - 1 ? "," : "")}" );
            }
            sb.AppendLine( ")" );
            sb.AppendLine();
        }

        // Max stack
        if ( body != null )
        {
            sb.AppendLine( $".maxstack {body.MaxStackSize}" );
            sb.AppendLine();
        }

        // Instructions
        foreach ( var ins in instructions )
        {
            var operandText = FormatOperand( ins.Operand );
            sb.AppendLine( $"{ins.Offset:X4}: {ins.OpCode.Name,-16} {operandText}".TrimEnd() );
        }

        return sb.ToString();
    }

    static string FormatOperand( object? operand )
    {
        return operand switch
        {
            null => "",
            int[] targets => $"({string.Join( ", ", targets.Select( t => $"0x{t:X4}" ) )})",
            MethodInfo mi => FormatMethodRef( mi ),
            ConstructorInfo ci => FormatConstructorRef( ci ),
            FieldInfo fi => $"{FormatType( fi.DeclaringType! )}.{fi.Name}",
            Type t => FormatType( t ),
            string s => $"\"{s}\"",
            sbyte sb => sb.ToString(),
            int i32 => $"0x{i32:X4}",
            long i64 => $"0x{i64:X}",
            float f32 => f32.ToString( "G" ),
            double f64 => f64.ToString( "G" ),
            _ => operand.ToString() ?? ""
        };
    }

    static string FormatMethodRef( MethodInfo mi )
    {
        var returnType = FormatType( mi.ReturnType );
        var declaringType = mi.DeclaringType != null ? FormatType( mi.DeclaringType ) + "::" : "";
        var parameters = string.Join( ", ", mi.GetParameters().Select( p => FormatType( p.ParameterType ) ) );
        return $"{returnType} {declaringType}{mi.Name}({parameters})";
    }

    static string FormatConstructorRef( ConstructorInfo ci )
    {
        var declaringType = ci.DeclaringType != null ? FormatType( ci.DeclaringType ) + "::" : "";
        var parameters = string.Join( ", ", ci.GetParameters().Select( p => FormatType( p.ParameterType ) ) );
        return $"void {declaringType}.ctor({parameters})";
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
            var backtick = baseName.IndexOf( '`' );
            if ( backtick > 0 )
                baseName = baseName[..backtick];

            var args = string.Join( ", ", type.GetGenericArguments().Select( FormatType ) );
            return $"{baseName}<{args}>";
        }

        return type.Name;
    }
}
