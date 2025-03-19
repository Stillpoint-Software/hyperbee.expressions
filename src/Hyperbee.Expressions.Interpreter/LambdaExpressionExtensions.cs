using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;

namespace Hyperbee.Expressions.Interpreter;

public static class LambdaExpressionExtensions
{
    private static readonly MethodInfo OpenGenericInterpretMethod = typeof( XsInterpreter )
        .GetMethod( nameof( XsInterpreter.Interpret ), BindingFlags.Public | BindingFlags.Instance );

    private static readonly ConcurrentDictionary<Type, MethodInfo> InterpretMethods = new();
    private static readonly ConcurrentDictionary<Type, Type> DelegateTypes = new();

    public static object Interpret( this LambdaExpression lambda )
    {
        return Interpret( new XsInterpreter(), lambda );
    }

    internal static object Interpret( this XsInterpreter interpreter, LambdaExpression lambda )
    {
        if ( !typeof( Delegate ).IsAssignableFrom( lambda.Type ) )
            throw new InvalidOperationException( "LambdaExpression must be convertible to a delegate." );

        var delegateType = DelegateTypes.GetOrAdd( lambda.Type, type =>
        {
            var invokeMethod = lambda.Type.GetMethod( "Invoke" )
                ?? throw new InvalidOperationException( "Invalid delegate type." );

            var paramTypes = invokeMethod.GetParameters()
                .Select( p => p.ParameterType )
                .Append( invokeMethod.ReturnType )
                .ToArray();

            return Expression.GetDelegateType( paramTypes );
        } );

        return Interpret( interpreter, lambda, delegateType );
    }

    public static TDelegate Interpret<TDelegate>( this Expression<TDelegate> lambda )
        where TDelegate : Delegate
    {
        return (TDelegate) Interpret( (LambdaExpression) lambda );
    }

    public static object Interpret( this XsInterpreter interpreter, LambdaExpression lambda, Type delegateType )
    {
        var method = InterpretMethods.GetOrAdd( delegateType, type => OpenGenericInterpretMethod?.MakeGenericMethod( type ) );

        var interpretedDelegate = method?.Invoke( interpreter, [lambda] );

        if ( interpretedDelegate == null )
            throw new InvalidOperationException( "Failed to create interpreted delegate." );

        return interpretedDelegate;
    }
}
