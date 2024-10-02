using System.Diagnostics;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using static System.Linq.Expressions.Expression;

namespace Hyperbee.AsyncExpressions.Transformation;

[DebuggerDisplay( "Transition = {GetType().Name,nq}" )]

public abstract class Transition
{
    internal abstract Expression Reduce( IFieldResolverSource resolverSource );
}

public class AwaitResultTransition : Transition
{
    public ParameterExpression AwaiterVariable { get; set; }
    public ParameterExpression ResultVariable { get; set; }
    public StateNode TargetNode { get; set; }

    internal override Expression Reduce( IFieldResolverSource resolverSource )
    {
        Expression getResult = (ResultVariable == null)
            ? Call( AwaiterVariable, "GetResult", Type.EmptyTypes )
            : Assign( ResultVariable, Call( AwaiterVariable, "GetResult", Type.EmptyTypes ) );

        return Block(
            getResult,
            Goto( TargetNode.Label )
        );
    }
}

public class AwaitTransition : Transition
{
    public Expression Target { get; set; }
    public ParameterExpression AwaiterVariable { get; set; }
    public int StateId { get; set; }
    public StateNode CompletionNode { get; set; }

    internal override Expression Reduce( IFieldResolverSource resolverSource )
    {
        return Block(
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
            Goto( CompletionNode.Label )
        );

    }
}

public class ConditionalTransition : Transition
{
    public Expression Test { get; set; }
    public StateNode IfTrue { get; set; }
    public StateNode IfFalse { get; set; }

    internal override Expression Reduce( IFieldResolverSource resolverSource )
    {
        return IfThenElse(
            Test,
            Goto( IfTrue.Label ),
            Goto( IfFalse.Label ) 
        );
    }
}

public class GotoTransition : Transition
{
    public StateNode TargetNode { get; set; }

    internal override Expression Reduce( IFieldResolverSource resolverSource )
    {
        return Goto( TargetNode.Label );
    }
}

public class LoopTransition : Transition
{
    public StateNode TargetNode { get; set; }
    public StateNode Body { get; set; }
    public Expression ContinueGoto { get; set; }

    internal override Expression Reduce( IFieldResolverSource resolverSource )
    {
        return ContinueGoto;
    }
}

public class SwitchTransition : Transition
{
    public List<SwitchCaseTransition> CaseNodes { get; set; } = [];
    public StateNode DefaultNode { get; set; }
    public Expression SwitchValue { get; set; }

    internal override Expression Reduce( IFieldResolverSource resolverSource )
    {
        var defaultBody = (DefaultNode != null)
            ? Goto( DefaultNode.Label )
            : null;

        var cases = CaseNodes
            .Select( switchCase => switchCase.ReduceSwitchCase() );

        return Switch(
            SwitchValue,
            defaultBody,
            [.. cases]
        );
    }
}

public class SwitchCaseTransition
{
    public List<Expression> TestValues { get; set; } = [];
    public StateNode Body { get; set; }

    public SwitchCase ReduceSwitchCase()
    {
        return SwitchCase( Goto( Body.Label ), TestValues );
    }
}

public class TryCatchTransition : Transition
{
    public StateNode TryNode { get; set; }
    public List<CatchBlockTransition> CatchBlocks { get; set; } = [];
    public StateNode FinallyNode { get; set; }

    internal override Expression Reduce( IFieldResolverSource resolverSource )
    {
        var catches = CatchBlocks
            .Select( catchBlock => catchBlock.ReduceCatchBlock() );

        var finallyBody = (FinallyNode != null)
            ? Goto( FinallyNode.Label )
            : null;

        return TryCatchFinally(
            Goto( TryNode.Label ),
            finallyBody,
            [.. catches]
        );
    }
}

public class CatchBlockTransition
{
    public Type Test { get; set; }
    public StateNode Body { get; set; }

    public CatchBlock ReduceCatchBlock()
    {
        return Catch( Test, Goto( Body.Label ) );
    }
}
