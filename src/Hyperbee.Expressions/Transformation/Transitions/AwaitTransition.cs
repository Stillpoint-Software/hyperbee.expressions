using System.Linq.Expressions;

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

    protected override Expression VisitChildren( ExpressionVisitor visitor )
    {
        return Update(
            visitor.Visit( Target ),
            visitor.Visit( AwaiterVariable )
        );
    }

    internal AwaitTransition Update( Expression target, Expression awaiterVariable )
    {
        if ( target == Target && awaiterVariable == AwaiterVariable )
            return this;

        return new AwaitTransition
        {
            StateId = StateId,
            Target = target,
            AwaiterVariable = awaiterVariable,
            CompletionNode = CompletionNode,
            AwaitBinder = AwaitBinder,
            ConfigureAwait = ConfigureAwait
        };
    }

    protected override List<Expression> GetBody()
    {
        return GetExpressions();

        List<Expression> GetExpressions()
        {
            var resolverSource = Parent.StateMachineSource;
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

            var fallThrough = GotoOrFallThrough( Parent.StateOrder, CompletionNode, true );

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
