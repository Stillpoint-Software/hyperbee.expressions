using System.Linq.Expressions;

namespace Hyperbee.Expressions.Visitors;

internal class ExpressionDependencyMatcher : ExpressionVisitor
{
    private readonly Dictionary<Expression, int> _countDictionary = [];
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
        if ( node == null )
            return null;

        if ( _countDictionary.TryGetValue( node, out var dependencyCount ) )
        {
            _counter += dependencyCount;
            return node;
        }

        var previousCounter = _counter;
        _counter = 0;

        base.Visit( node );

        dependencyCount = _matchPredicate( node ) ? _counter + 1 : _counter;

        _counter = previousCounter + dependencyCount;
        _countDictionary[node] = dependencyCount;

        return node;
    }
}
