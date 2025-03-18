using System.Linq.Expressions;
using System.Reflection;

namespace Hyperbee.Expressions.Interpreter;

public static class LambdaExpressionExtensions
{
    public static object Interpret( this LambdaExpression lambda )
    {
        return Interpret( new XsInterpreter(), lambda );
    }

    internal static object Interpret( this XsInterpreter interpreter, LambdaExpression lambda )
    {
        if ( !typeof( Delegate ).IsAssignableFrom( lambda.Type ) )
            throw new InvalidOperationException( "LambdaExpression must be convertible to a delegate." );

        var invokeMethod = lambda.Type.GetMethod( "Invoke" );

        if ( invokeMethod is null )
            throw new InvalidOperationException( "Invalid delegate type." );

        var paramTypes = invokeMethod.GetParameters()
            .Select( p => p.ParameterType )
            .Append( invokeMethod.ReturnType )
            .ToArray();

        var delegateType = Expression.GetDelegateType( paramTypes );

        return Interpret( interpreter, lambda, delegateType );
    }

    public static TDelegate Interpret<TDelegate>( this Expression<TDelegate> lambda )
        where TDelegate : Delegate
    {
        return (TDelegate) Interpret( (LambdaExpression) lambda );
    }

    public static object Interpret( this XsInterpreter interpreter, LambdaExpression lambda, Type delegateType )
    {
        var method = typeof( XsInterpreter )
            .GetMethod( nameof( XsInterpreter.Interpret ), BindingFlags.Public | BindingFlags.Instance )?
            .MakeGenericMethod( delegateType );

        var interpretedDelegate = method?.Invoke( interpreter, [lambda] );

        if ( interpretedDelegate == null )
            throw new InvalidOperationException( "Failed to create interpreted delegate." );

        return interpretedDelegate;
    }
}
