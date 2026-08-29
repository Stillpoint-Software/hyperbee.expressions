using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using static System.Linq.Expressions.Expression;

namespace Hyperbee.Expressions.CompilerServices;

/// <summary>
/// Hoists variables shared between a coroutine block and its enclosing scope into
/// <see cref="StrongBox{T}"/> cells, so the coroutine body can carry the cell by field
/// instead of being compiled as a closure.
/// </summary>
/// <remarks>
/// A coroutine body that reads an enclosing variable has to be materialized per call by the
/// enclosing compiler's closure machinery. Rewriting the variable to a cell moves the sharing
/// into the tree: the body reads <c>cell.Value</c> and never assigns the cell, so the state
/// machine can hold the cell in a field and stay closed. The cell is an ordinary variable to
/// the enclosing compiler, so this works the same under any of them.
///
/// Only variables with a single declaration site are rewritten. Anything else is left alone
/// and takes the original path, which is slower but correct.
/// </remarks>
public static class CoroutineClosureRewriter
{
    /// <summary>
    /// Rewrites the lambda so coroutine blocks share enclosing variables through cells.
    /// Returns the lambda unchanged when there is nothing to share.
    /// </summary>
    public static LambdaExpression Rewrite( LambdaExpression lambda )
    {
        ArgumentNullException.ThrowIfNull( lambda );

        var shared = SharedVariableScanner.Find( lambda );

        if ( shared.Count == 0 )
            return lambda;

        var boxes = new Dictionary<ParameterExpression, ParameterExpression>( shared.Count );

        foreach ( var variable in shared )
        {
            boxes[variable] = Variable(
                typeof( StrongBox<> ).MakeGenericType( variable.Type ),
                $"$cell_{variable.Name}" );
        }

        var rewriter = new Rewriter( boxes );

        var body = rewriter.Visit( lambda.Body );
        var parameters = lambda.Parameters.Where( boxes.ContainsKey ).ToArray();

        if ( parameters.Length > 0 )
            body = Rewriter.DeclareCells( parameters, boxes, body, seedFromVariable: true );

        return Lambda( lambda.Type, body, lambda.Name, lambda.TailCall, lambda.Parameters );
    }

    /// <summary>
    /// Rewrites a typed lambda. See <see cref="Rewrite(LambdaExpression)"/>.
    /// </summary>
    public static Expression<TDelegate> Rewrite<TDelegate>( Expression<TDelegate> lambda )
    {
        return (Expression<TDelegate>) Rewrite( (LambdaExpression) lambda );
    }

    private sealed class Rewriter( Dictionary<ParameterExpression, ParameterExpression> boxes ) : ExpressionVisitor
    {
        public static Expression DeclareCells(
            IReadOnlyList<ParameterExpression> variables,
            Dictionary<ParameterExpression, ParameterExpression> boxes,
            Expression body,
            bool seedFromVariable )
        {
            var cells = new ParameterExpression[variables.Count];
            var expressions = new Expression[variables.Count + 1];

            for ( var index = 0; index < variables.Count; index++ )
            {
                var variable = variables[index];
                var cell = boxes[variable];
                var constructor = cell.Type.GetConstructor( seedFromVariable ? [variable.Type] : Type.EmptyTypes )!;

                cells[index] = cell;

                expressions[index] = Assign(
                    cell,
                    seedFromVariable ? New( constructor, variable ) : New( constructor ) );
            }

            expressions[^1] = body;

            return Block( body.Type, cells, expressions );
        }

        protected override Expression VisitParameter( ParameterExpression node )
        {
            return boxes.TryGetValue( node, out var cell )
                ? Field( cell, "Value" )
                : node;
        }

        protected override Expression VisitBlock( BlockExpression node )
        {
            var hoisted = node.Variables.Where( boxes.ContainsKey ).ToArray();

            if ( hoisted.Length == 0 )
                return base.VisitBlock( node );

            var remaining = node.Variables.Where( variable => !boxes.ContainsKey( variable ) );
            var expressions = node.Expressions.Select( Visit ).ToArray();

            var inner = expressions.Length == 1 && !remaining.Any()
                ? expressions[0]
                : Block( node.Type, remaining, expressions );

            return DeclareCells( hoisted, boxes, inner, seedFromVariable: false );
        }

        protected override CatchBlock VisitCatchBlock( CatchBlock node )
        {
            if ( node.Variable == null || !boxes.ContainsKey( node.Variable ) )
                return base.VisitCatchBlock( node );

            // The catch variable has to remain a ParameterExpression of the handler's type,
            // so the cell is seeded from it at the top of the handler instead.

            var body = DeclareCells( [node.Variable], boxes, Visit( node.Body ), seedFromVariable: true );

            return MakeCatchBlock( node.Test, node.Variable, body, node.Filter == null ? null : Visit( node.Filter ) );
        }

        protected override Expression VisitLambda<T>( Expression<T> node )
        {
            var hoisted = node.Parameters.Where( boxes.ContainsKey ).ToArray();

            if ( hoisted.Length == 0 )
                return base.VisitLambda( node );

            var body = DeclareCells( hoisted, boxes, Visit( node.Body ), seedFromVariable: true );

            return Lambda( node.Type, body, node.Name, node.TailCall, node.Parameters );
        }

        protected override Expression VisitExtension( Expression node )
        {
            switch ( node )
            {
                case AsyncBlockExpression asyncBlock:
                    return RewriteCoroutine( asyncBlock.Variables, asyncBlock.Expressions,
                        ( variables, expressions ) => new AsyncBlockExpression( variables, expressions, asyncBlock.RuntimeOptions ) );

                case EnumerableBlockExpression enumerableBlock:
                    return RewriteCoroutine( enumerableBlock.Variables, enumerableBlock.Expressions,
                        ( variables, expressions ) => new EnumerableBlockExpression( variables, expressions, enumerableBlock.RuntimeOptions ) );

                default:
                    return base.VisitExtension( node );
            }
        }

        private Expression RewriteCoroutine(
            System.Collections.ObjectModel.ReadOnlyCollection<ParameterExpression> variables,
            System.Collections.ObjectModel.ReadOnlyCollection<Expression> expressions,
            Func<System.Collections.ObjectModel.ReadOnlyCollection<ParameterExpression>,
                 System.Collections.ObjectModel.ReadOnlyCollection<Expression>, Expression> create )
        {
            var hoisted = variables.Where( boxes.ContainsKey ).ToArray();
            var remaining = variables.Where( variable => !boxes.ContainsKey( variable ) ).ToList();

            var rewritten = expressions.Select( Visit ).ToList();

            if ( hoisted.Length > 0 )
            {
                // A cell declared by the block itself is initialized inside it, so the
                // declaration order the block relies on is preserved.

                remaining.AddRange( hoisted.Select( variable => boxes[variable] ) );

                for ( var index = hoisted.Length - 1; index >= 0; index-- )
                {
                    var cell = boxes[hoisted[index]];
                    rewritten.Insert( 0, Assign( cell, New( cell.Type.GetConstructor( Type.EmptyTypes )! ) ) );
                }
            }

            return create(
                new System.Collections.ObjectModel.ReadOnlyCollection<ParameterExpression>( remaining ),
                new System.Collections.ObjectModel.ReadOnlyCollection<Expression>( rewritten ) );
        }
    }

    private sealed class SharedVariableScanner : ExpressionVisitor
    {
        private readonly HashSet<ParameterExpression> _candidates = [];
        private readonly Dictionary<ParameterExpression, int> _declarations = [];

        public static HashSet<ParameterExpression> Find( LambdaExpression lambda )
        {
            var scanner = new SharedVariableScanner();

            foreach ( var parameter in lambda.Parameters )
            {
                scanner.Declare( parameter );
            }

            scanner.Visit( lambda.Body );

            // Only a variable with a single declaration site can be replaced by a cell
            // without changing which scope the declaration belongs to.

            scanner._candidates.RemoveWhere(
                variable => !scanner._declarations.TryGetValue( variable, out var count ) || count != 1 );

            return scanner._candidates;
        }

        private void Declare( ParameterExpression variable )
        {
            _declarations[variable] = _declarations.TryGetValue( variable, out var count ) ? count + 1 : 1;
        }

        private void Declare( IReadOnlyList<ParameterExpression> variables )
        {
            for ( var index = 0; index < variables.Count; index++ )
            {
                Declare( variables[index] );
            }
        }

        protected override Expression VisitBlock( BlockExpression node )
        {
            Declare( node.Variables );
            return base.VisitBlock( node );
        }

        protected override CatchBlock VisitCatchBlock( CatchBlock node )
        {
            if ( node.Variable != null )
                Declare( node.Variable );

            return base.VisitCatchBlock( node );
        }

        protected override Expression VisitLambda<T>( Expression<T> node )
        {
            Declare( node.Parameters );
            return base.VisitLambda( node );
        }

        protected override Expression VisitExtension( Expression node )
        {
            switch ( node )
            {
                case AsyncBlockExpression asyncBlock:
                    Declare( asyncBlock.Variables );
                    Collect( asyncBlock.Variables, asyncBlock.Expressions );
                    break;

                case EnumerableBlockExpression enumerableBlock:
                    Declare( enumerableBlock.Variables );
                    Collect( enumerableBlock.Variables, enumerableBlock.Expressions );
                    break;
            }

            return base.VisitExtension( node );
        }

        private void Collect(
            IReadOnlyList<ParameterExpression> declared,
            IReadOnlyList<Expression> expressions )
        {
            foreach ( var variable in FreeVariableScanner.Find( declared, expressions, out _ ) )
            {
                _candidates.Add( variable );
            }
        }
    }
}
