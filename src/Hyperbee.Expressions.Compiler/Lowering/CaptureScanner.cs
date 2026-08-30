using System.Linq.Expressions;
using Hyperbee.Expressions;

namespace Hyperbee.Expressions.Compiler.Lowering;

/// <summary>
/// Scans a lambda expression tree to find variables that are captured
/// by nested lambda expressions (closures). A captured variable is one
/// declared in an outer scope but referenced inside a nested lambda.
/// </summary>
/// <remarks>
/// Both passes are <see cref="ExpressionVisitor"/>-based so that every node type is
/// traversed. A hand-rolled walk that misses a node type does not fail loudly — it
/// under-reports captures, and the variable is then compiled as an unshared local.
/// </remarks>
public static class CaptureScanner
{
    /// <summary>
    /// Find all <see cref="ParameterExpression"/>s in the root lambda that are
    /// captured by nested lambda expressions or referenced by RuntimeVariables.
    /// </summary>
    public static HashSet<ParameterExpression> FindCapturedVariables( LambdaExpression rootLambda )
    {
        // Pass 1: what the root frame declares. Closure boundaries are not entered —
        // what they declare belongs to their own frame.
        var rootScope = DeclarationScanner.Scan( rootLambda );

        // Pass 2: root-frame variables referenced from inside a closure boundary, plus
        // any variable a RuntimeVariables node needs live access to.
        return ReferenceScanner.Scan( rootLambda, rootScope );
    }

    /// <summary>
    /// A closure boundary is a construct compiled into its own frame: a nested lambda,
    /// or a coroutine block, whose state-machine body becomes a lambda during Reduce().
    /// Returns the variables the boundary declares, or null when the node is not one.
    /// </summary>
    private static IReadOnlyList<ParameterExpression>? CoroutineVariables( Expression node )
    {
        return node switch
        {
            AsyncBlockExpression asyncBlock => asyncBlock.Variables,
            EnumerableBlockExpression enumerableBlock => enumerableBlock.Variables,
            _ => null
        };
    }

    private sealed class DeclarationScanner : ExpressionVisitor
    {
        private readonly HashSet<ParameterExpression> _declared = [];

        public static HashSet<ParameterExpression> Scan( LambdaExpression rootLambda )
        {
            var scanner = new DeclarationScanner();

            foreach ( var parameter in rootLambda.Parameters )
            {
                scanner._declared.Add( parameter );
            }

            scanner.Visit( rootLambda.Body );

            return scanner._declared;
        }

        protected override Expression VisitBlock( BlockExpression node )
        {
            foreach ( var variable in node.Variables )
            {
                _declared.Add( variable );
            }

            return base.VisitBlock( node );
        }

        protected override CatchBlock VisitCatchBlock( CatchBlock node )
        {
            if ( node.Variable != null )
                _declared.Add( node.Variable );

            return base.VisitCatchBlock( node );
        }

        protected override Expression VisitLambda<T>( Expression<T> node )
        {
            return node; // closure boundary
        }

        protected override Expression VisitInvocation( InvocationExpression node )
        {
            // An invoked lambda is inlined, so it is not a boundary: its parameters become
            // block variables of this frame and its body runs here.

            if ( !InvocationInliner.CanInline( node, out var lambda ) )
                return base.VisitInvocation( node );

            foreach ( var parameter in lambda!.Parameters )
            {
                _declared.Add( parameter );
            }

            foreach ( var argument in node.Arguments )
            {
                Visit( argument );
            }

            Visit( lambda.Body );

            return node;
        }

        protected override Expression VisitExtension( Expression node )
        {
            return CoroutineVariables( node ) != null
                ? node // closure boundary
                : base.VisitExtension( node );
        }
    }

    private sealed class ReferenceScanner : ExpressionVisitor
    {
        private readonly HashSet<ParameterExpression> _rootScope;
        private readonly HashSet<ParameterExpression> _captured = [];
        private readonly List<ParameterExpression> _shadowed = [];

        private int _boundaryDepth;

        private ReferenceScanner( HashSet<ParameterExpression> rootScope )
        {
            _rootScope = rootScope;
        }

        public static HashSet<ParameterExpression> Scan( LambdaExpression rootLambda, HashSet<ParameterExpression> rootScope )
        {
            var scanner = new ReferenceScanner( rootScope );

            scanner.Visit( rootLambda.Body );

            return scanner._captured;
        }

        protected override Expression VisitParameter( ParameterExpression node )
        {
            if ( _boundaryDepth > 0 && _rootScope.Contains( node ) && !_shadowed.Contains( node ) )
                _captured.Add( node );

            return node;
        }

        protected override Expression VisitRuntimeVariables( RuntimeVariablesExpression node )
        {
            // RuntimeVariables requires live read/write access, which only a StrongBox provides.

            foreach ( var variable in node.Variables )
            {
                _captured.Add( variable );
            }

            return node;
        }

        protected override Expression VisitLambda<T>( Expression<T> node )
        {
            var count = Shadow( node.Parameters );
            _boundaryDepth++;

            try
            {
                return base.VisitLambda( node );
            }
            finally
            {
                _boundaryDepth--;
                Unshadow( count );
            }
        }

        protected override Expression VisitInvocation( InvocationExpression node )
        {
            // An invoked lambda is inlined, so it is not a boundary. Its parameters shadow
            // an enclosing variable of the same instance for the length of the body.

            if ( !InvocationInliner.CanInline( node, out var lambda ) )
                return base.VisitInvocation( node );

            foreach ( var argument in node.Arguments )
            {
                Visit( argument );
            }

            var count = Shadow( lambda!.Parameters );

            try
            {
                Visit( lambda.Body );
                return node;
            }
            finally
            {
                Unshadow( count );
            }
        }

        protected override Expression VisitExtension( Expression node )
        {
            var variables = CoroutineVariables( node );

            if ( variables == null )
                return base.VisitExtension( node );

            var count = Shadow( variables );
            _boundaryDepth++;

            try
            {
                return base.VisitExtension( node );
            }
            finally
            {
                _boundaryDepth--;
                Unshadow( count );
            }
        }

        protected override Expression VisitBlock( BlockExpression node )
        {
            // Outside a boundary a block declaration is a root-frame declaration, already
            // in scope. Inside one it shadows the root-frame variable of the same instance.

            var count = _boundaryDepth > 0 ? Shadow( node.Variables ) : 0;

            try
            {
                return base.VisitBlock( node );
            }
            finally
            {
                Unshadow( count );
            }
        }

        protected override CatchBlock VisitCatchBlock( CatchBlock node )
        {
            var count = _boundaryDepth > 0 && node.Variable != null ? Shadow( [node.Variable] ) : 0;

            try
            {
                return base.VisitCatchBlock( node );
            }
            finally
            {
                Unshadow( count );
            }
        }

        private int Shadow( IReadOnlyList<ParameterExpression> variables )
        {
            var count = 0;

            for ( var index = 0; index < variables.Count; index++ )
            {
                var variable = variables[index];

                if ( !_rootScope.Contains( variable ) )
                    continue;

                _shadowed.Add( variable );
                count++;
            }

            return count;
        }

        private void Unshadow( int count )
        {
            if ( count > 0 )
                _shadowed.RemoveRange( _shadowed.Count - count, count );
        }
    }
}
