using System.Reflection;
using System.Runtime.CompilerServices;

namespace Hyperbee.Expressions.Factory;

public delegate bool MethodMatchDelegate( Type[] parameterTypes, int? argCount = null );

internal static class Reflection
{
    internal static bool OpenGenericIsOrInherits( Type baseType, Type checkType )
    {
        if ( !baseType.IsGenericTypeDefinition || !checkType.IsGenericTypeDefinition )
            throw new ArgumentException( $"Both {nameof(baseType)} and {nameof(checkType)} should be generic type definitions." );

        if ( baseType == checkType )
            return true;

        var currentType = checkType;

        while ( currentType != null && currentType != typeof(object) )
        {
            if ( currentType.IsGenericType && currentType.GetGenericTypeDefinition() == baseType )
                return true;

            currentType = currentType.BaseType;
        }

        return false;
    }

    // find open-generic and non-generic methods

    public static void GetMethods( Type target, BindingFlags bindingFlags, Action<string, MethodInfo, MethodMatchDelegate> matchCallback )
    {
        var methods = target.GetMethods( bindingFlags );

        foreach ( var method in methods )
        {
            var methodName = method.Name;

            matchCallback( methodName, method, Matches );

            continue;

            bool Matches( Type[] parameterTypes, int? argCount = null )
            {
                if ( argCount.HasValue )
                {
                    if ( !method.IsGenericMethodDefinition || method.GetGenericArguments().Length != argCount.Value )
                        return false;
                }

                var parameters = method.GetParameters();

                if ( parameters.Length != parameterTypes.Length ) 
                    return false;

                for ( var i = 0; i < parameters.Length; i++ )
                {
                    var paramType = parameters[i].ParameterType;
                    var matchType = parameterTypes[i];

                    // If the method's parameter is generic and our type is null (open generic match)

                    if ( matchType == null )
                    {
                        if ( paramType.IsGenericParameter )
                            continue;

                        return false;
                    }

                    // If the parameter is a generic type, check the generic definition

                    if ( paramType.IsGenericType )
                    {
                        if ( matchType.IsGenericType && paramType.GetGenericTypeDefinition() == matchType.GetGenericTypeDefinition() )
                            continue;

                        return false;
                    }

                    // Compare non-generic types directly

                    if ( paramType == parameterTypes[i] )
                    {
                        continue;
                    }

                    return false;
                }

                return true;
            }
        }
    }

    internal static MethodInfo FindExtensionMethod( Type targetType, string methodName )
    {
        // Search for an extension method with the specified name that extends the specified target type.
        //
        // Extension searching is a very expensive operation. To minimize the performance impact, we
        // will search in a specific order to try and reduce the number of assemblies that need to be
        // searched.
        //
        // The search order is:
        //
        // * Calling assembly
        // * Entry assembly
        // * Target assembly
        // * All other assemblies
        //
        var callingAssembly = Assembly.GetCallingAssembly();
        var entryAssembly = Assembly.GetEntryAssembly();
        var targetAssembly = targetType.Assembly;

        // Search the calling assembly (first)
        var method = FindExtensionMethodInAssembly( callingAssembly, targetType, methodName );

        if ( method != null )
            return method;

        // Search the entry assembly (second)
        if ( entryAssembly != null && entryAssembly != callingAssembly )
        {
            method = FindExtensionMethodInAssembly( entryAssembly, targetType, methodName );

            if ( method != null )
                return method;
        }

        // Search the target assembly (third)
        if ( targetAssembly != callingAssembly && targetAssembly != entryAssembly )
        {
            method = FindExtensionMethodInAssembly( targetAssembly, targetType, methodName );

            if ( method != null )
                return method;
        }

        // Search all other assemblies
        foreach ( var assembly in AppDomain.CurrentDomain.GetAssemblies() )
        {
            if ( assembly == callingAssembly || assembly == entryAssembly || assembly == targetAssembly )
                continue;

            method = FindExtensionMethodInAssembly( assembly, targetType, methodName );
            
            if ( method != null ) 
                return method;
        }

        return null;
    }

    internal static MethodInfo FindExtensionMethodInAssembly( Assembly assembly, Type targetType, string methodName )
    {
        // Search for an extension method with the specified name that extends the specified target type.
        // This is a very expensive operation. To minimize the performance impact, we will filter out as
        // many types as possible.

        var methods = assembly.GetTypes()
            .Where( t => t.IsClass && t.IsSealed && t.IsAbstract && !t.IsGenericType && !t.IsNested )
            .SelectMany( t => 
                t.GetMethods( BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic )
                .Where( m => m.Name == methodName && m.IsDefined( typeof(ExtensionAttribute), false ) 
            ) );

        MethodInfo openMatch = null;

        foreach ( var method in methods )
        {
            var parameters = method.GetParameters();

            if ( parameters.Length == 0 )
                continue;

            var parameterType = parameters[0].ParameterType;

            if ( !parameterType.IsGenericType && parameterType == targetType )
                return method; 

            if ( openMatch == null && method.IsGenericMethodDefinition && parameterType.IsGenericType && targetType.IsGenericType )
            {
                var parameterTypeDefinition = parameterType.GetGenericTypeDefinition();
                var targetTypeDefinition = targetType.GetGenericTypeDefinition();

                if ( parameterTypeDefinition == targetTypeDefinition )
                {
                    var targetGenericArguments = targetType.GetGenericArguments();

                    try
                    {
                        openMatch = method.MakeGenericMethod( targetGenericArguments );
                        // keep searching for an exact match
                    }
                    catch
                    {
                        continue;
                    }
                }
            }

            if ( !parameterType.IsGenericType || !targetType.IsGenericType || parameterType != targetType )
                continue;

            return method; 
        }

        return openMatch;
    }
}
