using System.Diagnostics;
using System.Linq.Expressions;

namespace Hyperbee.AsyncExpressions;

[DebuggerDisplay( "AwaitBlock({Before}, {After})" )]
[DebuggerTypeProxy( typeof( AwaitableBlockExpressionProxy ) )]
public class AwaitableBlockExpression( Expression before, Expression after ) : Expression
{
    public Expression Before { get; } = before;
    public Expression After { get; } = after;

    public override ExpressionType NodeType => ExpressionType.Extension;
    public override bool CanReduce => true;

    public override Type Type => After.Type;

    public override Expression Reduce()
    {
        return Block( Before, After );
    }

    private class AwaitableBlockExpressionProxy( AwaitableBlockExpression node )
    {
        public Expression Before => node.Before;
        public Expression After => node.After;
    }
}
