using System.Diagnostics;
using System.Linq.Expressions;
using Hyperbee.Expressions.Transformation.Transitions;

namespace Hyperbee.Expressions.Transformation;

[DebuggerDisplay( "State = {NodeLabel?.Name,nq}, ScopeId = {ScopeId}, GroupId = {GroupId}, StateOrder = {StateOrder}, Transition = {Transition?.GetType().Name,nq}" )]
public sealed class NodeExpression : Expression
{
    public int StateId { get; set; }
    public int GroupId { get; set; }
    public int ScopeId { get; set; }

    internal int StateOrder { get; set; }

    public NodeResult Result { get; set; } = new ();

    public LabelTarget NodeLabel { get; set; }
    public List<Expression> Expressions { get; set; } = new( 8 );

    internal Transition Transition { get; set; }

    internal NodeExpression() { }

    public NodeExpression( int stateId, int scopeId, int groupId )
    {
        StateId = stateId;
        ScopeId = scopeId;
        GroupId = groupId;
        NodeLabel = Label( $"ST_{StateId:0000}" );
    }

    public override ExpressionType NodeType => ExpressionType.Extension;
    public override Type Type => typeof( void );
    public override bool CanReduce => false; // This should NEVER be reduced

    public bool IsNoOp => Expressions.Count == 0 && Result.Variable == null;

    internal Expression GetExpression( StateMachineContext context )
    {
        ArgumentNullException.ThrowIfNull( context, nameof( context ) );

        var expressions = new List<Expression>( 8 ) { Label( NodeLabel ) };
        expressions.AddRange( Expressions );

        var prevNodeInfo = context.NodeInfo;
        context.NodeInfo = new NodeInfo( StateOrder, Result );

        Transition.AddExpressions( expressions, context );

        context.NodeInfo = prevNodeInfo;

        return expressions.Count == 1
            ? expressions[0]
            : Block( expressions );
    }

    internal static List<Expression> Merge( List<NodeExpression> nodes, StateMachineContext context )
    {
        var mergedExpressions = new List<Expression>( 32 );

        for ( var index = 0; index < nodes.Count; index++ )
        {
            var node = nodes[index];
            var expression = node.GetExpression( context );

            if ( expression is BlockExpression innerBlock )
                mergedExpressions.AddRange( innerBlock.Expressions.Where( expr => !IsDefaultVoid( expr ) ) );
            else
                mergedExpressions.Add( expression );
        }

        return mergedExpressions;

        static bool IsDefaultVoid( Expression expression )
        {
            return expression is DefaultExpression defaultExpression &&
                   defaultExpression.Type == typeof( void );
        }
    }
}
