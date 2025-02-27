namespace Hyperbee.Expressions.Interpreter.Core;

public sealed class TypeResolver
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

    internal static bool IsWideningConversion( Type from, Type to )
    {
        return WideningConversions.TryGetValue( from, out var targets ) && targets.Contains( to );
    }
}
