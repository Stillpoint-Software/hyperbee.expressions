using System.Globalization;
using System.Linq.Expressions;

namespace Hyperbee.Expressions.Transformation;

internal record LoweringResult
{
    public List<StateContext.Scope> Scopes { get; init; }
    public ParameterExpression ReturnValue { get; init; }
    public int AwaitCount { get; init; }
    public IReadOnlyCollection<Expression> Variables { get; init; }
    public ParameterExpression[] ScopedVariables { get; internal set; }

    public IEnumerable<NodeExpression> Nodes => Scopes.SelectMany( scope => scope.Nodes );

    internal string DebugView
    {
        get
        {
            using StringWriter writer = new StringWriter( CultureInfo.CurrentCulture );
            DebugViewWriter.WriteTo( writer, Scopes, Variables );
            return writer.ToString();
        }
    }
}
