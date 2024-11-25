using System.Linq.Expressions;

namespace Hyperbee.Expressions.Visitors;

internal class ExpressionDependencyMatcher : ExpressionVisitor
{
    private readonly Dictionary<Expression, int> _countDictionary = new();
    private readonly Func<Expression, bool> _matchPredicate;
    private int _counter;

    public ExpressionDependencyMatcher( Func<Expression, bool> matchPredicate )
    {
        _matchPredicate = matchPredicate ?? throw new ArgumentNullException( nameof( matchPredicate ) );
    }

    public int MatchCount( Expression expression )
    {
        if ( expression == null )
            throw new ArgumentNullException( nameof( expression ) );

        if ( _countDictionary.TryGetValue( expression, out var count ) )
            return count;

        _counter = 0;
        Visit( expression );

        return _counter;
    }

    public bool HasMatch( Expression expression )
    {
        return MatchCount( expression ) > 0;
    }

    public void Clear()
    {
        _countDictionary.Clear();
        _counter = 0;
    }

    protected override Expression VisitLambda<T>( Expression<T> node )
    {
        return node;
    }

    public override Expression Visit( Expression node )
    {
        if ( node == null || _countDictionary.ContainsKey( node ) )
            return node;

        var parentCount = _counter;
        _counter = 0;

        var result = base.Visit( node );

        var childCount = _counter;
        _counter += parentCount;

        _countDictionary[node] = childCount;

        return result;
    }

    protected override Expression VisitExtension( Expression node )
    {
        if ( _matchPredicate( node ) )
        {
            _counter++;
        }

        return base.VisitExtension( node );
    }
}
