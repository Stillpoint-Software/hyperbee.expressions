using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using static System.Linq.Expressions.Expression;

namespace Hyperbee.AsyncExpressions.Transformation.Transitions;

public class AwaitTransition : Transition
{
    public int StateId { get; set; }

    public Expression Target { get; set; }
    public ParameterExpression AwaiterVariable { get; set; }
    public NodeExpression CompletionNode { get; set; }
    public AwaitBinder AwaitBinder { get; set; }

    internal override Expression Reduce( int order, NodeExpression expression, IFieldResolverSource resolverSource )
    {
        var awaiterMethod = AwaitBinder.GetAwaiterMethod.IsGenericMethodDefinition
            ? AwaitBinder.GetAwaiterMethod.MakeGenericMethod( Target.Type.GenericTypeArguments[0] )
            : AwaitBinder.GetAwaiterMethod;

        var expressions = new List<Expression>
        {
            Assign(
                AwaiterVariable,
                Call( awaiterMethod, Target )
            ),
            IfThen(
                IsFalse( Property( AwaiterVariable, "IsCompleted" ) ),
                Block(
                    Assign( resolverSource.StateIdField, Constant( StateId ) ),
                    Call(
                        resolverSource.BuilderField,
                        "AwaitUnsafeOnCompleted",
                        [AwaiterVariable.Type, typeof( IAsyncStateMachine )],
                        AwaiterVariable,
                        resolverSource.StateMachine
                    ),
                    Return( resolverSource.ReturnLabel )
                )
            )
        };

        var fallThrough = GotoOrFallThrough( order, CompletionNode, true );
        
        if ( fallThrough != null )
            expressions.Add( fallThrough );

        return Block( expressions );
        /*return Block(
            Assign(
                AwaiterVariable,
                Call( Target, Target.Type.GetMethod( "GetAwaiter" )! )
            ),
            IfThen(
                IsFalse( Property( AwaiterVariable, "IsCompleted" ) ),
                Block(
                    Assign( resolverSource.StateIdField, Constant( StateId ) ),
                    Call(
                        resolverSource.BuilderField,
                        "AwaitUnsafeOnCompleted",
                        [AwaiterVariable.Type, typeof( IAsyncStateMachine )],
                        AwaiterVariable,
                        resolverSource.StateMachine
                    ),
                    Return( resolverSource.ReturnLabel )
                )
            ),
            GotoOrFallThrough( order, CompletionNode )
        );*/
    }

    internal override NodeExpression FallThroughNode => CompletionNode;
}
