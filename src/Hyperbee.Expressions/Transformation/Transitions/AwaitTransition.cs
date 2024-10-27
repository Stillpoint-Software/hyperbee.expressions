using System.Linq.Expressions;
using static System.Linq.Expressions.Expression;

namespace Hyperbee.Expressions.Transformation.Transitions;

public class AwaitTransition : Transition
{
    public int StateId { get; set; }

    public Expression Target { get; set; }
    public ParameterExpression AwaiterVariable { get; set; }
    public NodeExpression CompletionNode { get; set; }
    public AwaitBinder AwaitBinder { get; set; }
    public bool ConfigureAwait { get; set; }

    internal override Expression Reduce( int order, NodeExpression expression, IHoistingSource resolverSource )
    {
        var awaitable = Variable( Target.Type, "awaitable" );

        var getAwaiterMethod = AwaitBinder.GetAwaiterMethod;

        var getAwaiterCall = getAwaiterMethod.IsStatic
            ? Call( getAwaiterMethod, awaitable, Constant( ConfigureAwait ) )
            : Call( Constant( AwaitBinder ), getAwaiterMethod, awaitable, Constant( ConfigureAwait ) );

        // Get AwaitUnsafeOnCompleted<TAwaiter, TStateMachine>( ref awaiter, ref state-machine )
        var awaitUnsafeOnCompleted = resolverSource.BuilderField.Type
            .GetMethods()
            .Single( m => m.Name == "AwaitUnsafeOnCompleted" && m.IsGenericMethodDefinition )
            .MakeGenericMethod( AwaiterVariable.Type, resolverSource.StateMachine.Type );

        var expressions = new List<Expression>
        {
            Assign(
                awaitable,
                Target
            ),
            Assign(
                AwaiterVariable,
                getAwaiterCall
            ),
            IfThen(
                IsFalse( Property( AwaiterVariable, "IsCompleted" ) ),
                Block(
                    Assign( resolverSource.StateIdField, Constant( StateId ) ),
                    Call(
                        resolverSource.BuilderField,
                        awaitUnsafeOnCompleted,
                        AwaiterVariable,
                        resolverSource.StateMachine
                    ),
                    Return( resolverSource.ExitLabel )
                )
            )
        };

        var fallThrough = GotoOrFallThrough( order, CompletionNode, true );

        if ( fallThrough != null )
            expressions.Add( fallThrough );

        return Block( [awaitable], expressions );
    }

    internal override NodeExpression FallThroughNode => CompletionNode;
    
    internal override void OptimizeTransition( HashSet<LabelTarget> references )
    {
        CompletionNode = OptimizeTransition( CompletionNode );
        references.Add( CompletionNode.NodeLabel );
    }
}
