using System.Collections.ObjectModel;
using System.Linq.Expressions;
using Hyperbee.Collections;
using Hyperbee.Expressions.CompilerServices;
using Hyperbee.Expressions.CompilerServices.Lowering;

namespace Hyperbee.Expressions;

public class YieldBlockExpression : Expression
{
    private Type _enumerableType;
    public ReadOnlyCollection<Expression> Expressions { get; }
    public ReadOnlyCollection<ParameterExpression> Variables { get; }

    internal LinkedDictionary<ParameterExpression, ParameterExpression> ScopedVariables { get; set; }

    public YieldBlockExpression(
        ReadOnlyCollection<ParameterExpression> variables,
        ReadOnlyCollection<Expression> expressions )
    {
        if ( expressions == null || expressions.Count == 0 )
            throw new ArgumentException( "YieldBlockExpression must contain at least one expression." );

        Variables = variables;
        Expressions = expressions;
    }

    public override ExpressionType NodeType => ExpressionType.Extension;
    public Type EnumerableType => _enumerableType ??= GetYieldType();
    public override Type Type => typeof( IEnumerable<> ).MakeGenericType( EnumerableType );
    public override bool CanReduce => true;

    public override Expression Reduce()
    {
        return YieldStateMachineBuilder.Create( EnumerableType, LoweringTransformer );
    }

    private LoweringInfo LoweringTransformer()
    {
        try
        {
            var visitor = new YieldLoweringVisitor();

            return visitor.Transform(
                EnumerableType,
                [.. Variables],
                [.. Expressions],
                ScopedVariables ?? []
            );
        }
        catch ( LoweringException ex )
        {
            throw new InvalidOperationException( $"Unable to lower {nameof( AsyncBlockExpression )}.", ex );
        }
    }

    private Type GetYieldType()
    {
        var stack = new Stack<Expression>( Expressions );
        while ( stack.Count > 0 )
        {
            var current = stack.Pop();
            switch ( current )
            {
                case YieldExpression { IsReturn: true } yieldExpression:
                    return yieldExpression.Type;

                case BlockExpression blockExpression:
                    foreach ( var expr in blockExpression.Expressions )
                    {
                        stack.Push( expr );
                    }
                    break;
                case ConditionalExpression conditionalExpression:
                    stack.Push( conditionalExpression.IfTrue );
                    stack.Push( conditionalExpression.IfFalse );
                    stack.Push( conditionalExpression.Test );
                    break;
                case LoopExpression loopExpression:
                    stack.Push( loopExpression.Body );
                    break;
                case SwitchExpression switchExpression:
                    stack.Push( switchExpression.DefaultBody );
                    foreach ( var switchCase in switchExpression.Cases )
                    {
                        stack.Push( switchCase.Body );
                    }
                    stack.Push( switchExpression.SwitchValue );
                    break;
                case TryExpression tryExpression:
                    stack.Push( tryExpression.Body );
                    if ( tryExpression.Fault != null ) stack.Push( tryExpression.Fault );
                    if ( tryExpression.Finally != null ) stack.Push( tryExpression.Finally );
                    foreach ( var handler in tryExpression.Handlers )
                    {
                        stack.Push( handler.Body );
                    }
                    break;
            }
        }
        return typeof( void );
    }
}

public static partial class ExpressionExtensions
{
    public static YieldBlockExpression BlockYield( params Expression[] expressions )
    {
        return new YieldBlockExpression( ReadOnlyCollection<ParameterExpression>.Empty, new ReadOnlyCollection<Expression>( expressions ) );
    }

    public static YieldBlockExpression BlockYield( ParameterExpression[] variables, params Expression[] expressions )
    {
        return new YieldBlockExpression( new ReadOnlyCollection<ParameterExpression>( variables ), new ReadOnlyCollection<Expression>( expressions ) );
    }

    public static YieldBlockExpression BlockYield( ReadOnlyCollection<Expression> expressions )
    {
        return new YieldBlockExpression( ReadOnlyCollection<ParameterExpression>.Empty, expressions );
    }

    public static YieldBlockExpression BlockYield( ReadOnlyCollection<ParameterExpression> variables, ReadOnlyCollection<Expression> expressions )
    {
        return new YieldBlockExpression( variables, expressions );
    }
}
