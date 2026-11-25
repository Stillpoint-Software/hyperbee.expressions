using System.Linq.Expressions;

namespace Hyperbee.Expressions.Compiler.Lowering;

/// <summary>
/// Removes the branch of a conditional or switch that a constant test proves unreachable.
/// </summary>
/// <remarks>
/// This exists for its effect on coroutines, not for tidier IL. An await inside a branch that
/// can never execute is still lowered in full: it gets its own state and its own result field
/// on the generated state machine. Measured on a <c>BlockAsync</c> with one live await, adding
/// a second await under <c>Constant( false )</c> cost 17.6 KB of a 45.7 KB compile and an
/// extra field, which is within 5% of what two genuinely live awaits cost. Coroutine
/// compilation is dominated by <c>TypeBuilder.CreateType()</c>, so a state removed here is
/// worth far more than any amount of shorter IL.
///
/// For a plain lambda the same work buys almost nothing -- the JIT folds a constant test and
/// drops the dead block on its own -- which is why the caller gates this on the pre-scan that
/// proves whether extension nodes are present at all.
///
/// Note what this deliberately is **not**: it does not fold arithmetic. A constant test is
/// already a literal <c>bool</c>, so none of the hazards that make a constant folder dangerous
/// -- checked overflow, NaN ordering, nullable lifted operators, nullable bool three-valued
/// logic, decimal -- arise here. Keeping the two apart is what makes this safe.
/// </remarks>
internal sealed class ConstantBranchEliminator : ExpressionVisitor
{
    private static readonly ConstantBranchEliminator Instance = new();

    // Stateless, so one shared instance is safe across threads. The label scan below
    // allocates per call, and only for a branch that is about to be discarded.
    private ConstantBranchEliminator()
    {
    }

    public static LambdaExpression Eliminate( LambdaExpression lambda )
        => (LambdaExpression) Instance.Visit( lambda );

    protected override Expression VisitConditional( ConditionalExpression node )
    {
        var result = base.VisitConditional( node );

        if ( result is not ConditionalExpression visited )
            return result;

        if ( visited.Test is not ConstantExpression { Value: bool test } )
            return visited;

        var live = test ? visited.IfTrue : visited.IfFalse;
        var dead = test ? visited.IfFalse : visited.IfTrue;

        // Deleting a branch that defines a label would orphan any jump to it. Expression
        // tree scoping already forbids jumping into a block from outside, and this runs on
        // the caller's tree before any lowering, so the tree still obeys that rule -- but
        // the check is cheap and the failure would be an invalid-program defect.
        return DefinesLabel( dead ) ? visited : Retype( live, visited.Type );
    }

    protected override Expression VisitSwitch( SwitchExpression node )
    {
        var result = base.VisitSwitch( node );

        if ( result is not SwitchExpression visited )
            return result;

        // A custom comparison method decides what matching means, so resolving the switch
        // here would be guessing at its semantics. Only default equality is decidable.
        if ( visited.Comparison != null || visited.SwitchValue is not ConstantExpression value )
            return visited;

        // Every test value has to be constant. One that is not leaves it unknown whether an
        // earlier case would have matched, so the whole switch stays.
        foreach ( var switchCase in visited.Cases )
        {
            foreach ( var testValue in switchCase.TestValues )
            {
                if ( testValue is not ConstantExpression )
                    return visited;
            }
        }

        SwitchCase? match = null;

        foreach ( var switchCase in visited.Cases )
        {
            foreach ( var testValue in switchCase.TestValues )
            {
                if ( !Equals( ( (ConstantExpression) testValue ).Value, value.Value ) )
                    continue;

                match = switchCase;
                break;
            }

            if ( match != null )
                break;
        }

        var live = match?.Body ?? visited.DefaultBody;

        if ( live == null )
            return visited;

        foreach ( var switchCase in visited.Cases )
        {
            if ( switchCase != match && DefinesLabel( switchCase.Body ) )
                return visited;
        }

        if ( match != null && visited.DefaultBody != null && DefinesLabel( visited.DefaultBody ) )
            return visited;

        return Retype( live, visited.Type );
    }

    private static Expression Retype( Expression chosen, Type target )
    {
        if ( chosen.Type == target )
            return chosen;

        // A conditional or switch may carry a declared type wider than either branch, so the
        // surviving branch is widened back to it rather than narrowing the node's type under
        // whatever is reading it.
        return target == typeof( void )
            ? Expression.Block( typeof( void ), chosen )
            : Expression.Convert( chosen, target );
    }

    private static bool DefinesLabel( Expression? node ) => node != null && LabelFinder.Any( node );

    /// <summary>Finds whether a subtree defines a label, stopping at the first one.</summary>
    private sealed class LabelFinder : ExpressionVisitor
    {
        private bool _found;

        public static bool Any( Expression node )
        {
            var finder = new LabelFinder();
            finder.Visit( node );
            return finder._found;
        }

        public override Expression? Visit( Expression? node ) => _found ? node : base.Visit( node );

        protected override Expression VisitLabel( LabelExpression node )
        {
            _found = true;
            return node;
        }
    }
}
