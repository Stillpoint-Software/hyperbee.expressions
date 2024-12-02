using System.Linq.Expressions;
using static System.Linq.Expressions.Expression;

namespace Hyperbee.Expressions.Transformation.Transitions;

internal class AwaitTransition : Transition
{
    public int StateId { get; set; }

    public Expression Target { get; set; }
    public Expression AwaiterVariable { get; set; }
    public NodeExpression CompletionNode { get; set; }
    public AwaitBinder AwaitBinder { get; set; }
    public bool ConfigureAwait { get; set; }

    internal override NodeExpression FallThroughNode => CompletionNode;

    protected override List<Expression> GetBody(NodeExpression parent )
    {
        return GetExpressions();

        List<Expression> GetExpressions()
        {
            var resolverSource = parent.StateMachineSource;
            var getAwaiterMethod = AwaitBinder.GetAwaiterMethod;

            var getAwaiterCall = getAwaiterMethod.IsStatic
                ? Call( getAwaiterMethod, Target, Constant( ConfigureAwait ) )
                : Call( Constant( AwaitBinder ), getAwaiterMethod, Target, Constant( ConfigureAwait ) );

            // Get AwaitUnsafeOnCompleted<TAwaiter, TStateMachine>( ref awaiter, ref state-machine )
            var awaitUnsafeOnCompleted = resolverSource.BuilderField.Type
                .GetMethods()
                .Single( methodInfo => methodInfo.Name == "AwaitUnsafeOnCompleted" && methodInfo.IsGenericMethodDefinition )
                .MakeGenericMethod( AwaiterVariable.Type, resolverSource.StateMachine.Type );

            var body = new List<Expression>
            {
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

            var fallThrough = GotoOrFallThrough( parent.StateOrder, CompletionNode, true );

            if ( fallThrough != null )
                body.Add( fallThrough );

            return body;
        }
    }

    internal override void Optimize( HashSet<LabelTarget> references )
    {
        CompletionNode = OptimizeGotos( CompletionNode );
        references.Add( CompletionNode.NodeLabel );
    }
}
