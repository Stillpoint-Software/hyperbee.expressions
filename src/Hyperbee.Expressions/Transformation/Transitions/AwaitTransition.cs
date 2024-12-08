#define USE_LOCAL_AWAITER

using System.Linq.Expressions;
using static System.Linq.Expressions.Expression;

namespace Hyperbee.Expressions.Transformation.Transitions;

internal class AwaitTransition : Transition
{
    public int StateId { get; set; }

    public Expression Target { get; set; }
    public Expression AwaiterVariable { get; set; }
    public IStateNode CompletionNode { get; set; }
    public AwaitBinder AwaitBinder { get; set; }
    public bool ConfigureAwait { get; set; }

    internal override IStateNode FallThroughNode => CompletionNode;

    public override void AddExpressions( List<Expression> expressions, StateMachineContext context )
    {
        base.AddExpressions( expressions, context );
        expressions.AddRange( Expressions() );
        return;
#if USE_LOCAL_AWAITER
        List<Expression> Expressions()
        {
            var getAwaiterMethod = AwaitBinder.GetAwaiterMethod;
            var source = context.StateMachineInfo;
            
            var localAwaiter = Variable( Target.Type, "localAwaiter" );

            var getBinderCall = Call( typeof(AwaitBinderFactory).GetMethod( nameof(AwaitBinderFactory.GetOrCreate), [typeof( Type )] )!, Constant( AwaitBinder.TargetType ) ); //BF ME - Test to bypass Constant(AwaitBinder)

            var getAwaiterCall = getAwaiterMethod.IsStatic
                ? Call( getAwaiterMethod, localAwaiter, Constant( ConfigureAwait ) )
                : Call( /*Constant( AwaitBinder )*/ getBinderCall, getAwaiterMethod, localAwaiter, Constant( ConfigureAwait ) ); //BF ME - Bypass Constant(AwaitBinder)

            // Get AwaitUnsafeOnCompleted<TAwaiter, TStateMachine>( ref awaiter, ref state-machine )
            var awaitUnsafeOnCompleted = source.BuilderField.Type
                .GetMethods()
                .Single( methodInfo => methodInfo.Name == "AwaitUnsafeOnCompleted" && methodInfo.IsGenericMethodDefinition )
                .MakeGenericMethod( AwaiterVariable.Type, source.StateMachine.Type );

            var body = new List<Expression>
            {
                Assign( localAwaiter, Target ),
                Assign(
                    AwaiterVariable,
                    getAwaiterCall
                ),
                IfThen(
                    IsFalse( Property( AwaiterVariable, "IsCompleted" ) ),
                    Block(
                        Assign( source.StateField, Constant( StateId ) ),
                        Call(
                            source.BuilderField,
                            awaitUnsafeOnCompleted,
                            AwaiterVariable,
                            source.StateMachine
                        ),
                        Return( source.ExitLabel )
                    )
                )
            };

            var fallThrough = GotoOrFallThrough( context.StateNode.StateOrder, CompletionNode, true );

            if ( fallThrough != null )
                body.Add( fallThrough );

            return [
                Block(
                    [localAwaiter],
                    body
                )
            ];
        }
#else
        List<Expression> Expressions()
        {
            var getAwaiterMethod = AwaitBinder.GetAwaiterMethod;
            var source = context.StateMachineInfo;

            var getAwaiterCall = getAwaiterMethod.IsStatic
                ? Call( getAwaiterMethod, Target, Constant( ConfigureAwait ) )
                : Call( Constant( AwaitBinder ), getAwaiterMethod, Target, Constant( ConfigureAwait ) );

            // Get AwaitUnsafeOnCompleted<TAwaiter, TStateMachine>( ref awaiter, ref state-machine )
            var awaitUnsafeOnCompleted = source.BuilderField.Type
                .GetMethods()
                .Single( methodInfo => methodInfo.Name == "AwaitUnsafeOnCompleted" && methodInfo.IsGenericMethodDefinition )
                .MakeGenericMethod( AwaiterVariable.Type, source.StateMachine.Type );

            var body = new List<Expression>
            {
                Assign(
                    AwaiterVariable,
                    getAwaiterCall
                ),
                IfThen(
                    IsFalse( Property( AwaiterVariable, "IsCompleted" ) ),
                    Block(
                        Assign( source.StateField, Constant( StateId ) ),
                        Call(
                            source.BuilderField,
                            awaitUnsafeOnCompleted,
                            AwaiterVariable,
                            source.StateMachine
                        ),
                        Return( source.ExitLabel )
                    )
                )
            };

            var fallThrough = GotoOrFallThrough( context.StateNode.StateOrder, CompletionNode, true );

            if ( fallThrough != null )
                body.Add( fallThrough );

            return body;
        }
#endif
    }

    internal override void Optimize( HashSet<LabelTarget> references )
    {
        CompletionNode = OptimizeGotos( CompletionNode );
        references.Add( CompletionNode.NodeLabel );
    }
}
