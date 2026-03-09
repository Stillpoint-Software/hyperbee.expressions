using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;

namespace ILComparisonTool;

/// <summary>
/// Extracts IL bytes from DynamicMethod delegates.
/// Uses UnsafeAccessor for ILGenerator fields (public types).
/// Uses reflection for DynamicResolver fields (internal types can't be named in UnsafeAccessor).
/// </summary>
internal static class DynamicMethodILExtractor
{
    public static byte[]? TryGetILBytes( Delegate del )
    {
        var dm = GetOwnerDynamicMethod( del.Method );
        if ( dm == null )
            return null;

        // Strategy 1: DynamicResolver.m_code (available after CreateDelegate bakes the method)
        var bytes = TryGetFromResolver( dm );
        if ( bytes != null )
            return bytes;

        // Strategy 2: ILGenerator.m_ILStream (fallback)
        return TryGetFromILGenerator( dm );
    }

    public static int? TryGetMaxStack( Delegate del )
    {
        var dm = GetOwnerDynamicMethod( del.Method );
        if ( dm == null )
            return null;

        var resolver = GetResolver( dm );
        if ( resolver == null )
            return null;

        return resolver.GetType()
            .GetField( "m_stackSize", BindingFlags.NonPublic | BindingFlags.Instance )
            ?.GetValue( resolver ) as int?;
    }

    // --- Owner resolution ---

    static DynamicMethod? GetOwnerDynamicMethod( MethodInfo method )
    {
        if ( method is DynamicMethod dm )
            return dm;

        // method is RTDynamicMethod (private nested class) — get m_owner via reflection
        // Can't use UnsafeAccessor because RTDynamicMethod isn't a public type
        return method.GetType()
            .GetField( "m_owner", BindingFlags.NonPublic | BindingFlags.Instance )
            ?.GetValue( method ) as DynamicMethod;
    }

    // --- Resolver path (reflection — DynamicResolver is internal) ---

    static object? GetResolver( DynamicMethod dm )
    {
        // .NET 9 uses _resolver; older versions used m_resolver
        return ( typeof( DynamicMethod ).GetField( "_resolver", BindingFlags.NonPublic | BindingFlags.Instance )
              ?? typeof( DynamicMethod ).GetField( "m_resolver", BindingFlags.NonPublic | BindingFlags.Instance ) )
            ?.GetValue( dm );
    }

    static byte[]? TryGetFromResolver( DynamicMethod dm )
    {
        try
        {
            var resolver = GetResolver( dm );
            if ( resolver == null )
                return null;

            return resolver.GetType()
                .GetField( "m_code", BindingFlags.NonPublic | BindingFlags.Instance )
                ?.GetValue( resolver ) as byte[];
        }
        catch
        {
            return null;
        }
    }

    // --- ILGenerator path (UnsafeAccessor for public types) ---

    static byte[]? TryGetFromILGenerator( DynamicMethod dm )
    {
        try
        {
            // _ilGenerator in .NET 9 (DynamicILGenerator : RuntimeILGenerator : ILGenerator)
            var ilGen = ( typeof( DynamicMethod ).GetField( "_ilGenerator", BindingFlags.NonPublic | BindingFlags.Instance )
                       ?? typeof( DynamicMethod ).GetField( "m_ilGenerator", BindingFlags.NonPublic | BindingFlags.Instance ) )
                ?.GetValue( dm ) as ILGenerator;

            if ( ilGen == null )
                return null;

            // These fields are declared on ILGenerator (public), so UnsafeAccessor works
            var stream = GetILStream( ilGen );
            var length = GetILLength( ilGen );

            if ( stream == null || length <= 0 )
                return null;

            var result = new byte[length];
            Array.Copy( stream, result, length );
            return result;
        }
        catch
        {
            return null;
        }
    }

    // UnsafeAccessor: ILGenerator.m_ILStream (byte[]) and ILGenerator.m_length (int)
    // These work because ILGenerator and byte[]/int are all public types.

    [UnsafeAccessor( UnsafeAccessorKind.Field, Name = "m_ILStream" )]
    static extern ref byte[]? GetILStreamRef( ILGenerator ilg );

    static byte[]? GetILStream( ILGenerator ilg ) => GetILStreamRef( ilg );

    [UnsafeAccessor( UnsafeAccessorKind.Field, Name = "m_length" )]
    static extern ref int GetILLengthRef( ILGenerator ilg );

    static int GetILLength( ILGenerator ilg ) => GetILLengthRef( ilg );
}
