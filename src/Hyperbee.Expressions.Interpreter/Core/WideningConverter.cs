namespace Hyperbee.Expressions.Interpreter.Core;

public static class WideningConverter
{
    private static readonly Dictionary<Type, HashSet<Type>> WideningConversions = new()
    {
        { typeof(byte), [typeof(short), typeof(ushort), typeof(int), typeof(uint), typeof(long), typeof(ulong), typeof(float), typeof(double), typeof(decimal)] },
        { typeof(sbyte), [typeof(short), typeof(int), typeof(long), typeof(float), typeof(double), typeof(decimal)] },
        { typeof(short), [typeof(int), typeof(long), typeof(float), typeof(double), typeof(decimal)] },
        { typeof(ushort), [typeof(int), typeof(uint), typeof(long), typeof(ulong), typeof(float), typeof(double), typeof(decimal)] },
        { typeof(int), [typeof(long), typeof(float), typeof(double), typeof(decimal)] },
        { typeof(uint), [typeof(long), typeof(ulong), typeof(float), typeof(double), typeof(decimal)] },
        { typeof(long), [typeof(float), typeof(double), typeof(decimal)] },
        { typeof(ulong), [typeof(float), typeof(double), typeof(decimal)] },
        { typeof(char), [typeof(ushort), typeof(int), typeof(uint), typeof(long), typeof(ulong), typeof(float), typeof(double), typeof(decimal)] },
        { typeof(float), [typeof(double)] }
    };

    internal static Type ToWidenedType( Type leftType, Type rightType )
    {
        if ( leftType == rightType )
            return leftType;

        if ( CanConvertTo( leftType, rightType ) )
            return rightType;

        if ( CanConvertTo( rightType, leftType ) )
            return leftType;

        throw new InvalidOperationException( $"No valid widening conversion between {leftType} and {rightType}." );
    }

    internal static bool CanConvertTo( Type from, Type to )
    {
        return WideningConversions.TryGetValue( from, out var targets ) && targets.Contains( to );
    }
}
