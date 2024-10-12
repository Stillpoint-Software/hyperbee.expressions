using System.Reflection;
using System.Runtime.CompilerServices;

namespace Hyperbee.AsyncExpressions.Factory;

internal static class Reflection
{
    internal static bool IsOrInheritsFromGeneric( Type baseType, Type checkType )
    {
        if ( !baseType.IsGenericTypeDefinition || !checkType.IsGenericTypeDefinition )
            throw new ArgumentException( "Both baseType and checkType should be generic type definitions." );

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

    internal static MethodInfo GetGenericMethod( Type target, string name, Type[] types, BindingFlags bindingAttr )
    {
        // Find matching methods
        var methods = target
            .GetMethods( bindingAttr )
            .Where( m => m.Name == name && m.IsGenericMethodDefinition );

        // Find the method that matches the given types
        foreach ( var method in methods )
        {
            var parameters = method.GetParameters();

            if ( parameters.Length != types.Length )
                continue;

            var match = true;

            // Compare each parameter type with the given types

            for ( var i = 0; i < parameters.Length; i++ )
            {
                var paramType = parameters[i].ParameterType;

                if ( paramType.IsGenericType )
                {
                    if ( paramType.GetGenericTypeDefinition() == types[i].GetGenericTypeDefinition() )
                        continue;

                    match = false;
                    break;
                }

                if ( paramType == types[i] )
                    continue;

                match = false;
                break;
            }

            if ( match )
                return method;
        }

        return null;
    }

    internal static MethodInfo FindExtensionMethod( Type targetType, string methodName )
    {
        var callingAssembly = Assembly.GetCallingAssembly();
        var entryAssembly = Assembly.GetEntryAssembly();
        var targetAssembly = targetType.Assembly;

        // Search the calling assembly
        var method = FindMethodInAssembly( targetType, methodName, callingAssembly );

        if ( method != null )
            return method;

        // Search the entry assembly
        if ( entryAssembly != null && entryAssembly != callingAssembly )
        {
            method = FindMethodInAssembly( targetType, methodName, entryAssembly );

            if ( method != null )
                return method;
        }

        // Search the target assembly
        if ( targetAssembly != callingAssembly && targetAssembly != entryAssembly )
        {
            method = FindMethodInAssembly( targetType, methodName, targetAssembly );

            if ( method != null )
                return method;
        }

        // Search all other assemblies
        foreach ( var assembly in AppDomain.CurrentDomain.GetAssemblies() )
        {
            if ( assembly == callingAssembly || assembly == entryAssembly || assembly == targetAssembly )
                continue;

            method = FindMethodInAssembly( targetType, methodName, assembly );
            
            if ( method != null ) 
                return method;
        }

        return null;
    }

    internal static MethodInfo FindMethodInAssembly( Type targetType, string methodName, Assembly assembly )
    {
        var extensionMethods = assembly.GetTypes()
            .Where( t => t.IsSealed && !t.IsGenericType && !t.IsNested )
            .SelectMany( t => t.GetMethods( BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic ) )
            .Where( m => m.Name == methodName && m.IsDefined( typeof(ExtensionAttribute), false ) );

        MethodInfo closedGenericMatch = null;
        MethodInfo openGenericMatch = null;

        foreach ( var method in extensionMethods )
        {
            var parameters = method.GetParameters();

            if ( parameters.Length == 0 )
                continue;

            var parameterType = parameters[0].ParameterType;

            if ( !parameterType.IsGenericType && parameterType == targetType )
                return method;

            if ( method.IsGenericMethodDefinition && parameterType.IsGenericType && targetType.IsGenericType )
            {
                var parameterGenericTypeDefinition = parameterType.GetGenericTypeDefinition();
                var targetGenericTypeDefinition = targetType.GetGenericTypeDefinition();

                if ( parameterGenericTypeDefinition == targetGenericTypeDefinition )
                {
                    var targetGenericArguments = targetType.GetGenericArguments();

                    try
                    {
                        openGenericMatch = method.MakeGenericMethod( targetGenericArguments );
                    }
                    catch
                    {
                        continue;
                    }
                }
            }

            if ( !parameterType.IsGenericType || !targetType.IsGenericType || parameterType != targetType )
                continue;

            closedGenericMatch = method;
            break;
        }

        return closedGenericMatch ?? openGenericMatch;
    }
}
