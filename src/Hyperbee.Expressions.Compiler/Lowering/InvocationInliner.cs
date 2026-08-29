using System.Collections.ObjectModel;
using System.Linq.Expressions;
using static System.Linq.Expressions.Expression;

namespace Hyperbee.Expressions.Compiler.Lowering;

/// <summary>
/// Inlines <c>Expression.Invoke( lambda, args )</c> at the call site.
/// </summary>
/// <remarks>
/// A lambda invoked in place does not need to become a delegate. Its body can run in the calling
/// frame with its parameters bound as block variables, which removes a separate compilation and
/// removes the capture entirely: an enclosing variable the body reads becomes an ordinary local
/// read, so nothing has to be boxed and nothing is allocated per call. The System compiler does
/// the same. Note that <see cref="InvocationExpression.CanReduce"/> is false for this shape, so
/// the rewrite cannot be delegated to the BCL.
/// </remarks>
internal static class InvocationInliner
{
    /// <summary>
    /// True when the invocation targets a lambda whose body can be inlined at the call site.
    /// </summary>
    public static bool CanInline( InvocationExpression node, out LambdaExpression? lambda )
    {
        lambda = node.Expression as LambdaExpression;

        if ( lambda == null )
            return false;

        // A by-ref parameter has to alias the caller's storage, which a block variable cannot do.

        var parameters = lambda.Parameters;

        for ( var index = 0; index < parameters.Count; index++ )
        {
            if ( parameters[index].IsByRef )
                return false;
        }

        return true;
    }

    /// <summary>
    /// Rewrite the invocation as a block that binds the parameters and evaluates the body.
    /// </summary>
    public static Expression Inline( LambdaExpression lambda, ReadOnlyCollection<Expression> arguments )
    {
        var parameters = lambda.Parameters;

        if ( parameters.Count == 0 )
        {
            return lambda.Body.Type == lambda.ReturnType
                ? lambda.Body
                : Block( lambda.ReturnType, lambda.Body );
        }

        // An argument that reads one of the lambda's own parameters has to be evaluated before
        // those parameters come into scope, or the block declaration shadows the outer variable
        // the argument meant to read.

        if ( !ArgumentsReadParameters( arguments, parameters ) )
            return Bind( lambda, arguments );

        var temporaries = new ParameterExpression[parameters.Count];
        var expressions = new Expression[parameters.Count + 1];
        var values = new Expression[parameters.Count];

        for ( var index = 0; index < parameters.Count; index++ )
        {
            temporaries[index] = Variable( parameters[index].Type, $"$arg{index}" );
            expressions[index] = Assign( temporaries[index], arguments[index] );
            values[index] = temporaries[index];
        }

        expressions[^1] = Bind( lambda, values );

        return Block( lambda.ReturnType, temporaries, expressions );
    }

    private static Expression Bind( LambdaExpression lambda, IReadOnlyList<Expression> values )
    {
        var parameters = lambda.Parameters;
        var expressions = new Expression[parameters.Count + 1];

        for ( var index = 0; index < parameters.Count; index++ )
        {
            expressions[index] = Assign( parameters[index], values[index] );
        }

        expressions[^1] = lambda.Body;

        return Block( lambda.ReturnType, parameters, expressions );
    }

    private static bool ArgumentsReadParameters(
        ReadOnlyCollection<Expression> arguments,
        ReadOnlyCollection<ParameterExpression> parameters )
    {
        var scanner = new ParameterReader( parameters );

        for ( var index = 0; index < arguments.Count; index++ )
        {
            scanner.Visit( arguments[index] );

            if ( scanner.Found )
                return true;
        }

        return false;
    }

    private sealed class ParameterReader( ReadOnlyCollection<ParameterExpression> parameters ) : ExpressionVisitor
    {
        public bool Found { get; private set; }

        protected override Expression VisitParameter( ParameterExpression node )
        {
            if ( parameters.Contains( node ) )
                Found = true;

            return node;
        }
    }
}
