using System.Linq.Expressions;

namespace Hyperbee.Expressions.Compiler.Lowering;

/// <summary>
/// Scans a lambda expression tree to find variables that are captured
/// by nested lambda expressions (closures). A captured variable is one
/// declared in an outer scope but referenced inside a nested lambda.
/// </summary>
public static class CaptureScanner
{
    /// <summary>
    /// Find all <see cref="ParameterExpression"/>s in the root lambda that are
    /// captured by nested lambda expressions or referenced by RuntimeVariables.
    /// </summary>
    public static HashSet<ParameterExpression> FindCapturedVariables( LambdaExpression rootLambda )
    {
        var captured = new HashSet<ParameterExpression>();
        var outerScope = new HashSet<ParameterExpression>( rootLambda.Parameters.Count + 4 );

        // The root lambda's own parameters are in scope for nested lambdas
        foreach ( var param in rootLambda.Parameters )
        {
            outerScope.Add( param );
        }

        // Collect all variables declared in blocks within the root lambda body
        CollectDeclaredVariables( rootLambda.Body, outerScope );

        // Walk nested lambdas and find which outer-scope variables they reference
        FindCapturesInNestedLambdas( rootLambda.Body, outerScope, captured );

        // RuntimeVariables requires live read/write access, so variables must be in StrongBox
        FindRuntimeVariablesCaptures( rootLambda.Body, captured );

        return captured;
    }

    /// <summary>
    /// Recursively collect all block-declared variables in the expression tree,
    /// stopping at nested lambda boundaries.
    /// </summary>
    private static void CollectDeclaredVariables( Expression? node, HashSet<ParameterExpression> outerScope )
    {
        if ( node == null )
            return;

        switch ( node )
        {
            case BlockExpression block:
                foreach ( var variable in block.Variables )
                {
                    outerScope.Add( variable );
                }
                foreach ( var expr in block.Expressions )
                {
                    CollectDeclaredVariables( expr, outerScope );
                }
                break;

            case LambdaExpression:
                // Stop at nested lambda boundaries
                break;

            case ConditionalExpression conditional:
                CollectDeclaredVariables( conditional.Test, outerScope );
                CollectDeclaredVariables( conditional.IfTrue, outerScope );
                CollectDeclaredVariables( conditional.IfFalse, outerScope );
                break;

            case BinaryExpression binary:
                CollectDeclaredVariables( binary.Left, outerScope );
                CollectDeclaredVariables( binary.Right, outerScope );
                break;

            case UnaryExpression unary:
                CollectDeclaredVariables( unary.Operand, outerScope );
                break;

            case MethodCallExpression methodCall:
                CollectDeclaredVariables( methodCall.Object, outerScope );
                foreach ( var arg in methodCall.Arguments )
                {
                    CollectDeclaredVariables( arg, outerScope );
                }
                break;

            case InvocationExpression invocation:
                CollectDeclaredVariables( invocation.Expression, outerScope );
                foreach ( var arg in invocation.Arguments )
                {
                    CollectDeclaredVariables( arg, outerScope );
                }
                break;

            case MemberExpression member:
                CollectDeclaredVariables( member.Expression, outerScope );
                break;

            case NewExpression newExpr:
                foreach ( var arg in newExpr.Arguments )
                {
                    CollectDeclaredVariables( arg, outerScope );
                }
                break;

            case TryExpression tryExpr:
                CollectDeclaredVariables( tryExpr.Body, outerScope );
                foreach ( var handler in tryExpr.Handlers )
                {
                    if ( handler.Variable != null )
                    {
                        outerScope.Add( handler.Variable );
                    }
                    CollectDeclaredVariables( handler.Filter, outerScope );
                    CollectDeclaredVariables( handler.Body, outerScope );
                }
                CollectDeclaredVariables( tryExpr.Finally, outerScope );
                CollectDeclaredVariables( tryExpr.Fault, outerScope );
                break;

            case GotoExpression gotoExpr:
                CollectDeclaredVariables( gotoExpr.Value, outerScope );
                break;

            case LabelExpression labelExpr:
                CollectDeclaredVariables( labelExpr.DefaultValue, outerScope );
                break;

            case TypeBinaryExpression typeBinary:
                CollectDeclaredVariables( typeBinary.Expression, outerScope );
                break;

            // ParameterExpression, ConstantExpression, DefaultExpression: no children
        }
    }

    /// <summary>
    /// Walk the tree looking for nested lambda expressions. For each nested lambda,
    /// find variables it references that are in the outer scope.
    /// </summary>
    private static void FindCapturesInNestedLambdas(
        Expression? node,
        HashSet<ParameterExpression> outerScope,
        HashSet<ParameterExpression> captured )
    {
        if ( node == null )
            return;

        switch ( node )
        {
            case LambdaExpression nestedLambda:
                // Found a nested lambda -- scan its body for references to outer scope variables
                var innerScope = new HashSet<ParameterExpression>( nestedLambda.Parameters );
                FindReferencedOuterVariables( nestedLambda.Body, outerScope, innerScope, captured );
                break;

            case BlockExpression block:
                foreach ( var expr in block.Expressions )
                {
                    FindCapturesInNestedLambdas( expr, outerScope, captured );
                }
                break;

            case ConditionalExpression conditional:
                FindCapturesInNestedLambdas( conditional.Test, outerScope, captured );
                FindCapturesInNestedLambdas( conditional.IfTrue, outerScope, captured );
                FindCapturesInNestedLambdas( conditional.IfFalse, outerScope, captured );
                break;

            case BinaryExpression binary:
                FindCapturesInNestedLambdas( binary.Left, outerScope, captured );
                FindCapturesInNestedLambdas( binary.Right, outerScope, captured );
                break;

            case UnaryExpression unary:
                FindCapturesInNestedLambdas( unary.Operand, outerScope, captured );
                break;

            case MethodCallExpression methodCall:
                FindCapturesInNestedLambdas( methodCall.Object, outerScope, captured );
                foreach ( var arg in methodCall.Arguments )
                {
                    FindCapturesInNestedLambdas( arg, outerScope, captured );
                }
                break;

            case InvocationExpression invocation:
                FindCapturesInNestedLambdas( invocation.Expression, outerScope, captured );
                foreach ( var arg in invocation.Arguments )
                {
                    FindCapturesInNestedLambdas( arg, outerScope, captured );
                }
                break;

            case MemberExpression member:
                FindCapturesInNestedLambdas( member.Expression, outerScope, captured );
                break;

            case NewExpression newExpr:
                foreach ( var arg in newExpr.Arguments )
                {
                    FindCapturesInNestedLambdas( arg, outerScope, captured );
                }
                break;

            case TryExpression tryExpr:
                FindCapturesInNestedLambdas( tryExpr.Body, outerScope, captured );
                foreach ( var handler in tryExpr.Handlers )
                {
                    FindCapturesInNestedLambdas( handler.Filter, outerScope, captured );
                    FindCapturesInNestedLambdas( handler.Body, outerScope, captured );
                }
                FindCapturesInNestedLambdas( tryExpr.Finally, outerScope, captured );
                FindCapturesInNestedLambdas( tryExpr.Fault, outerScope, captured );
                break;

            case GotoExpression gotoExpr:
                FindCapturesInNestedLambdas( gotoExpr.Value, outerScope, captured );
                break;

            case LabelExpression labelExpr:
                FindCapturesInNestedLambdas( labelExpr.DefaultValue, outerScope, captured );
                break;

            case TypeBinaryExpression typeBinary:
                FindCapturesInNestedLambdas( typeBinary.Expression, outerScope, captured );
                break;

            // ParameterExpression, ConstantExpression, DefaultExpression: no children to walk
        }
    }

    /// <summary>
    /// Recursively scan an expression (inside a nested lambda) for references to
    /// outer-scope variables. Variables declared in the inner scope are excluded.
    /// </summary>
    private static void FindReferencedOuterVariables(
        Expression? node,
        HashSet<ParameterExpression> outerScope,
        HashSet<ParameterExpression> innerScope,
        HashSet<ParameterExpression> captured )
    {
        if ( node == null )
            return;

        switch ( node )
        {
            case ParameterExpression param:
                if ( outerScope.Contains( param ) && !innerScope.Contains( param ) )
                {
                    captured.Add( param );
                }
                break;

            case BlockExpression block:
                // Block can declare its own variables in the inner scope
                foreach ( var variable in block.Variables )
                {
                    innerScope.Add( variable );
                }
                foreach ( var expr in block.Expressions )
                {
                    FindReferencedOuterVariables( expr, outerScope, innerScope, captured );
                }
                break;

            case LambdaExpression nestedLambda:
                // Even deeper nesting -- add its params to inner scope and scan body
                var deeperScope = new HashSet<ParameterExpression>( innerScope );
                foreach ( var param in nestedLambda.Parameters )
                {
                    deeperScope.Add( param );
                }
                FindReferencedOuterVariables( nestedLambda.Body, outerScope, deeperScope, captured );
                break;

            case ConditionalExpression conditional:
                FindReferencedOuterVariables( conditional.Test, outerScope, innerScope, captured );
                FindReferencedOuterVariables( conditional.IfTrue, outerScope, innerScope, captured );
                FindReferencedOuterVariables( conditional.IfFalse, outerScope, innerScope, captured );
                break;

            case BinaryExpression binary:
                FindReferencedOuterVariables( binary.Left, outerScope, innerScope, captured );
                FindReferencedOuterVariables( binary.Right, outerScope, innerScope, captured );
                break;

            case UnaryExpression unary:
                FindReferencedOuterVariables( unary.Operand, outerScope, innerScope, captured );
                break;

            case MethodCallExpression methodCall:
                FindReferencedOuterVariables( methodCall.Object, outerScope, innerScope, captured );
                foreach ( var arg in methodCall.Arguments )
                {
                    FindReferencedOuterVariables( arg, outerScope, innerScope, captured );
                }
                break;

            case InvocationExpression invocation:
                FindReferencedOuterVariables( invocation.Expression, outerScope, innerScope, captured );
                foreach ( var arg in invocation.Arguments )
                {
                    FindReferencedOuterVariables( arg, outerScope, innerScope, captured );
                }
                break;

            case MemberExpression member:
                FindReferencedOuterVariables( member.Expression, outerScope, innerScope, captured );
                break;

            case NewExpression newExpr:
                foreach ( var arg in newExpr.Arguments )
                {
                    FindReferencedOuterVariables( arg, outerScope, innerScope, captured );
                }
                break;

            case TryExpression tryExpr:
                FindReferencedOuterVariables( tryExpr.Body, outerScope, innerScope, captured );
                foreach ( var handler in tryExpr.Handlers )
                {
                    if ( handler.Variable != null )
                    {
                        innerScope.Add( handler.Variable );
                    }
                    FindReferencedOuterVariables( handler.Filter, outerScope, innerScope, captured );
                    FindReferencedOuterVariables( handler.Body, outerScope, innerScope, captured );
                }
                FindReferencedOuterVariables( tryExpr.Finally, outerScope, innerScope, captured );
                FindReferencedOuterVariables( tryExpr.Fault, outerScope, innerScope, captured );
                break;

            case GotoExpression gotoExpr:
                FindReferencedOuterVariables( gotoExpr.Value, outerScope, innerScope, captured );
                break;

            case LabelExpression labelExpr:
                FindReferencedOuterVariables( labelExpr.DefaultValue, outerScope, innerScope, captured );
                break;

            case TypeBinaryExpression typeBinary:
                FindReferencedOuterVariables( typeBinary.Expression, outerScope, innerScope, captured );
                break;

            case ConstantExpression:
            case DefaultExpression:
                // Leaf nodes -- nothing to scan
                break;
        }
    }

    /// <summary>
    /// Recursively scan for RuntimeVariablesExpression nodes and force their
    /// referenced variables into the captured set. RuntimeVariables requires
    /// live read/write access, which is only possible through StrongBox.
    /// </summary>
    private static void FindRuntimeVariablesCaptures(
        Expression? node,
        HashSet<ParameterExpression> captured )
    {
        if ( node == null )
            return;

        switch ( node )
        {
            case RuntimeVariablesExpression runtimeVars:
                foreach ( var variable in runtimeVars.Variables )
                {
                    captured.Add( variable );
                }
                break;

            case BlockExpression block:
                foreach ( var expr in block.Expressions )
                {
                    FindRuntimeVariablesCaptures( expr, captured );
                }
                break;

            case ConditionalExpression conditional:
                FindRuntimeVariablesCaptures( conditional.Test, captured );
                FindRuntimeVariablesCaptures( conditional.IfTrue, captured );
                FindRuntimeVariablesCaptures( conditional.IfFalse, captured );
                break;

            case BinaryExpression binary:
                FindRuntimeVariablesCaptures( binary.Left, captured );
                FindRuntimeVariablesCaptures( binary.Right, captured );
                break;

            case UnaryExpression unary:
                FindRuntimeVariablesCaptures( unary.Operand, captured );
                break;

            case MethodCallExpression methodCall:
                FindRuntimeVariablesCaptures( methodCall.Object, captured );
                foreach ( var arg in methodCall.Arguments )
                {
                    FindRuntimeVariablesCaptures( arg, captured );
                }
                break;

            case InvocationExpression invocation:
                FindRuntimeVariablesCaptures( invocation.Expression, captured );
                foreach ( var arg in invocation.Arguments )
                {
                    FindRuntimeVariablesCaptures( arg, captured );
                }
                break;

            case TryExpression tryExpr:
                FindRuntimeVariablesCaptures( tryExpr.Body, captured );
                foreach ( var handler in tryExpr.Handlers )
                {
                    FindRuntimeVariablesCaptures( handler.Filter, captured );
                    FindRuntimeVariablesCaptures( handler.Body, captured );
                }
                FindRuntimeVariablesCaptures( tryExpr.Finally, captured );
                FindRuntimeVariablesCaptures( tryExpr.Fault, captured );
                break;

            case LambdaExpression lambda:
                FindRuntimeVariablesCaptures( lambda.Body, captured );
                break;

            case LoopExpression loop:
                FindRuntimeVariablesCaptures( loop.Body, captured );
                break;

            case SwitchExpression switchExpr:
                FindRuntimeVariablesCaptures( switchExpr.SwitchValue, captured );
                foreach ( var c in switchExpr.Cases )
                {
                    FindRuntimeVariablesCaptures( c.Body, captured );
                }
                FindRuntimeVariablesCaptures( switchExpr.DefaultBody, captured );
                break;

            case GotoExpression gotoExpr:
                FindRuntimeVariablesCaptures( gotoExpr.Value, captured );
                break;

            case LabelExpression labelExpr:
                FindRuntimeVariablesCaptures( labelExpr.DefaultValue, captured );
                break;
        }
    }
}
