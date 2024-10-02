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
}

public class AwaitTransition : Transition
{
    public Expression Target { get; set; }
    public ParameterExpression AwaiterVariable { get; set; }
    public int StateId { get; set; }
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
}

public class GotoTransition : Transition
{
    public NodeExpression TargetNode { get; set; }

    internal override Expression Reduce( IFieldResolverSource resolverSource )
    {
        return Goto( TargetNode.NodeLabel );
    }
}

public class LoopTransition : Transition
{
    public NodeExpression TargetNode { get; set; }
    public NodeExpression Body { get; set; }
    public Expression ContinueGoto { get; set; }

    internal override Expression Reduce( IFieldResolverSource resolverSource )
    {
        return ContinueGoto;
    }
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

    public void AddSwitchCase( List<Expression> testValues, NodeExpression body )
    {
        _caseNodes.Add( new SwitchCaseDefinition( testValues, body ) );
    }

    internal record SwitchCaseDefinition( List<Expression> TestValues, NodeExpression Body )
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

    public void AddCatchBlock( Type test, NodeExpression body )
    {
        _catchBlocks.Add( new CatchBlockDefinition( test, body ) );
    }

    internal record CatchBlockDefinition( Type Test, NodeExpression Body )
    {
        public CatchBlock Reduce() => Catch( Test, Goto( Body.NodeLabel ) );
    }
}

