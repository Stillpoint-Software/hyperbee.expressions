using System.Diagnostics;
using System.Linq.Expressions;

namespace Hyperbee.AsyncExpressions.Transformation.Transitions;

[DebuggerDisplay( "Transition = {GetType().Name,nq}" )]

public abstract class Transition
{
    internal abstract Expression Reduce( int order, IFieldResolverSource resolverSource );
    internal abstract NodeExpression LogicalNextNode { get; }
}
