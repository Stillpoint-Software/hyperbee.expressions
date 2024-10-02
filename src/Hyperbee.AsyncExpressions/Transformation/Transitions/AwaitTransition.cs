using System.Linq.Expressions;
using System.Runtime.CompilerServices;

namespace Hyperbee.AsyncExpressions.Transformation.Transitions;

public class AwaitTransition : Transition
{
    public int StateId { get; set; }

    public Expression Target { get; set; }
    public ParameterExpression AwaiterVariable { get; set; }
    public NodeExpression CompletionNode { get; set; }

    internal override Expression Reduce( int order, IFieldResolverSource resolverSource )
    {
        return Expression.Block(
            Expression.Assign(
                AwaiterVariable,
                Expression.Call( Target, Target.Type.GetMethod( "GetAwaiter" )! )
            ),
            Expression.IfThen(
                Expression.IsFalse( Expression.Property( AwaiterVariable, "IsCompleted" ) ),
                Expression.Block(
                    Expression.Assign( resolverSource.StateIdField, Expression.Constant( StateId ) ),
                    Expression.Call(
                        resolverSource.BuilderField,
                        "AwaitUnsafeOnCompleted",
                        [AwaiterVariable.Type, typeof( IAsyncStateMachine )],
                        AwaiterVariable,
                        resolverSource.StateMachine
                    ),
                    Expression.Return( resolverSource.ReturnLabel )
                )
            ),
            //Goto( CompletionNode.NodeLabel )
            order + 1 == CompletionNode.Order //BF ugly but works - we can clean up :)
                ? Expression.Empty()
                : Expression.Goto( CompletionNode.NodeLabel )
        );
    }

    internal override NodeExpression LogicalNextNode => CompletionNode;

}