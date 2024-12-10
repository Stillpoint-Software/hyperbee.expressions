#define FAST_COMPILER

using System.Linq.Expressions;
using static System.Linq.Expressions.Expression;

namespace Hyperbee.Expressions.Transformation.Transitions;

internal class AwaitTransition : Transition
{
    public int StateId { get; set; }

    public Expression Target { get; set; }
    public Expression AwaiterVariable { get; set; }
    public StateNode CompletionNode { get; set; }
    public AwaitBinder AwaitBinder { get; set; }
    public bool ConfigureAwait { get; set; }

    internal override StateNode FallThroughNode => CompletionNode;

    public override void AddExpressions( List<Expression> expressions, StateMachineContext context )
    {
        base.AddExpressions( expressions, context );
        expressions.AddRange( Expressions() );
        return;

        List<Expression> Expressions()
        {
            var getAwaiterMethod = AwaitBinder.GetAwaiterMethod;
            var source = context.StateMachineInfo;

#if FAST_COMPILER
            // FEC: Use local variable for ref Target.
            //
            // Directly using ref Target as a param (e.g. Call(.., ref Target))
            // results in an Invalid Program Exception.
            //
            // Use a local variable as workaround.

            var target = Variable( Target.Type, "awaiter" );
#else
            var target = Target;
#endif

            var getAwaiterCall = getAwaiterMethod.IsStatic
                ? Call( getAwaiterMethod, target, Constant( ConfigureAwait ) )
                : Call( Constant( AwaitBinder ), getAwaiterMethod, target, Constant( ConfigureAwait ) );

            // Get AwaitUnsafeOnCompleted<TAwaiter, TStateMachine>( ref awaiter, ref state-machine )
            var awaitUnsafeOnCompleted = source.BuilderField.Type
                .GetMethods()
                .Single( methodInfo => methodInfo.Name == "AwaitUnsafeOnCompleted" && methodInfo.IsGenericMethodDefinition )
                .MakeGenericMethod( AwaiterVariable.Type, source.StateMachine.Type );

            var body = new List<Expression>
            {
#if FAST_COMPILER
                Assign( target, Target ),
#endif
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

#if FAST_COMPILER
            return [
                Block(
                    [target],
                    body
                )
            ];
#else
            return body;
#endif
        }
    }

    internal override void Optimize( HashSet<LabelTarget> references )
    {
        CompletionNode = OptimizeGotos( CompletionNode );
        references.Add( CompletionNode.NodeLabel );
    }
}
