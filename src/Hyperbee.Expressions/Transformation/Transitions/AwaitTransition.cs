using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using static System.Linq.Expressions.Expression;

namespace Hyperbee.Expressions.Transformation.Transitions;

public class AwaitTransition : Transition
{
    public int StateId { get; set; }

    public Expression Target { get; set; }
    public ParameterExpression AwaiterVariable { get; set; }
    public NodeExpression CompletionNode { get; set; }
    public MethodInfo GetAwaiterMethod { get; set; }
    public bool ConfigureAwait { get; set; }

    internal override Expression Reduce( int order, NodeExpression expression, IFieldResolverSource resolverSource )
    {
        var getAwaiterCall = GetAwaiterMethod.IsStatic
            ? Call( GetAwaiterMethod, Target, Constant( ConfigureAwait ) )
            : Call( Target, GetAwaiterMethod, Constant( ConfigureAwait ) );

        var expressions = new List<Expression>
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
    }

    internal override NodeExpression FallThroughNode => CompletionNode;
}
