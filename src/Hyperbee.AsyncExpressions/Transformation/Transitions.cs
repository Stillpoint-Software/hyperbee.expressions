using System.Diagnostics;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using static System.Linq.Expressions.Expression;

namespace Hyperbee.AsyncExpressions.Transformation;

[DebuggerDisplay( "Transition = {GetType().Name,nq}" )]

public abstract class Transition
{
    internal abstract Expression Reduce( IFieldResolverSource resolverSource );
    internal abstract NodeExpression LogicalNextNode { get; }
}

public class AwaitResultTransition : Transition
{
    public ParameterExpression AwaiterVariable { get; set; }
    public ParameterExpression ResultVariable { get; set; }
    public NodeExpression TargetNode { get; set; }

    internal override Expression Reduce( IFieldResolverSource resolverSource )
    {
        Expression getResult = (ResultVariable == null)
            ? Call( AwaiterVariable, "GetResult", Type.EmptyTypes )
            : Assign( ResultVariable, Call( AwaiterVariable, "GetResult", Type.EmptyTypes ) );

        return Block(
            getResult,
            Goto( TargetNode.NodeLabel )
        );
    }

    internal override NodeExpression LogicalNextNode => TargetNode;
}

public class AwaitTransition : Transition
{
    public int StateId { get; set; }

    public Expression Target { get; set; }
    public ParameterExpression AwaiterVariable { get; set; }
    public NodeExpression CompletionNode { get; set; }

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
            Goto( CompletionNode.NodeLabel )
        );
    }

    internal override NodeExpression LogicalNextNode => CompletionNode;

}

public class ConditionalTransition : Transition
{
    public Expression Test { get; set; }
    public NodeExpression IfTrue { get; set; }
    public NodeExpression IfFalse { get; set; }

    internal override Expression Reduce( IFieldResolverSource resolverSource )
    {
        return IfThenElse(
            Test,
            Goto( IfTrue.NodeLabel ),
            Goto( IfFalse.NodeLabel ) 
        );
    }

    internal override NodeExpression LogicalNextNode => IfTrue;
}

public class GotoTransition : Transition
{
    public NodeExpression TargetNode { get; set; }

    internal override Expression Reduce( IFieldResolverSource resolverSource )
    {
        return Goto( TargetNode.NodeLabel );
    }

    internal override NodeExpression LogicalNextNode => TargetNode;
}

public class LoopTransition : Transition
{
    public NodeExpression TargetNode { get; set; }
    public NodeExpression BodyNode { get; set; }
    public Expression ContinueGoto { get; set; }

    internal override Expression Reduce( IFieldResolverSource resolverSource )
    {
        return ContinueGoto;
    }

    internal override NodeExpression LogicalNextNode => BodyNode;
}

public class SwitchTransition : Transition
{
    private readonly List<SwitchCaseDefinition> _caseNodes = [];
    public NodeExpression DefaultNode { get; set; }
    public Expression SwitchValue { get; set; }

    internal override Expression Reduce( IFieldResolverSource resolverSource )
    {
        var defaultBody = (DefaultNode != null)
            ? Goto( DefaultNode.NodeLabel )
            : null;

        var cases = _caseNodes
            .Select( switchCase => switchCase.Reduce() );

        return Switch(
            SwitchValue,
            defaultBody,
            [.. cases]
        );
    }

    internal override NodeExpression LogicalNextNode => DefaultNode;

    public void AddSwitchCase( List<Expression> testValues, NodeExpression body )
    {
        _caseNodes.Add( new SwitchCaseDefinition( testValues, body ) );
    }

    private record SwitchCaseDefinition( List<Expression> TestValues, NodeExpression Body )
    {
        public SwitchCase Reduce() => SwitchCase( Goto( Body.NodeLabel ), TestValues );
    }
}



public class TryCatchTransition : Transition
{
    private readonly List<CatchBlockDefinition> _catchBlocks = [];
    public NodeExpression TryNode { get; set; }
    public NodeExpression FinallyNode { get; set; }

    internal override Expression Reduce( IFieldResolverSource resolverSource )
    {
        var catches = _catchBlocks
            .Select( catchBlock => catchBlock.Reduce() );

        var finallyBody = (FinallyNode != null)
            ? Goto( FinallyNode.NodeLabel )
            : null;

        return TryCatchFinally(
            Goto( TryNode.NodeLabel ),
            finallyBody,
            [.. catches]
        );
    }

    internal override NodeExpression LogicalNextNode => TryNode;
    
    public void AddCatchBlock( Type test, NodeExpression body )
    {
        _catchBlocks.Add( new CatchBlockDefinition( test, body ) );
    }

    private record CatchBlockDefinition( Type Test, NodeExpression Body )
    {
        public CatchBlock Reduce() => Catch( Test, Goto( Body.NodeLabel ) );
    }
}

