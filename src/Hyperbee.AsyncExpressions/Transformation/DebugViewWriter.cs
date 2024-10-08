using System.Linq.Expressions;
using System.Text;
using Hyperbee.AsyncExpressions.Transformation.Transitions;

namespace Hyperbee.AsyncExpressions.Transformation;

internal static class DebugViewWriter
{
    private const string Indent1 = "\t";
    private const string Indent2 = "\t\t";
    private const string Indent3 = "\t\t\t";
    private const string Indent4 = "\t\t\t\t";

    public static void WriteTo( StringWriter writer, List<NodeExpression> nodes, IEnumerable<ParameterExpression> variables )
    {
        // variables
        var parameterExpressions = variables ?? [];

        var variableCount = 0;

        foreach ( var expr in parameterExpressions )
        {
            if ( variableCount++ == 0 )
                writer.WriteLine( "Variables" );

            writer.WriteLine( $"{Indent1}{VariableToString( expr )}" );
        }

        if ( variableCount > 0 )
            writer.WriteLine();

        // nodes
        foreach ( var node in nodes.Where( node => node != null ) )
        {
            // label
            writer.WriteLine( $"{node.NodeLabel.Name}:" );

            // expressions

            if ( node.Expressions.Count > 0 )
            {
                writer.WriteLine( $"{Indent1}Expressions" );

                foreach ( var expr in node.Expressions )
                {
                    writer.WriteLine( $"{Indent2}{ExpressionToString(expr)}" );
                }
            }

            // transitions

            writer.WriteLine( $"{Indent1}Transition" );
            writer.WriteLine( TransitionToString(node.Transition) );

            // results

            if ( node.ResultValue != null || node.ResultVariable != null )
            {
                writer.WriteLine( $"{Indent1}Result ({node.Type.Name})" );
                if ( node.ResultValue != null  )
                    writer.WriteLine( $"{Indent2}Value: {node.ResultValue}" );
                if ( node.ResultVariable != null )
                    writer.WriteLine( $"{Indent2}Variable: {node.ResultVariable}" );
            }

            writer.WriteLine();
        }
    }

    private static string TransitionToString( Transition transition )
    {
        if ( transition == null )
            return $"{Indent2}Exit";

        var builder = new StringBuilder();
        builder.AppendLine( $"{Indent2}{transition.GetType().Name}" );

        switch ( transition )
        {
            case ConditionalTransition conditional:
                builder.AppendLine( $"{Indent3}Test: {ExpressionToString( conditional.Test )}" );
                builder.AppendLine( $"{Indent3}IfTrue: {ExpressionToString( conditional.IfTrue )}" );
                builder.AppendLine( $"{Indent3}IfFalse: {ExpressionToString( conditional.IfFalse )}" );
                break;

            case GotoTransition gotoTransition:
                builder.AppendLine( $"{Indent3}Target: {gotoTransition.TargetNode.NodeLabel.Name}" );
                break;

            case AwaitTransition awaitTransition:
                builder.AppendLine( $"{Indent3}Completion: {awaitTransition.CompletionNode.NodeLabel.Name}" );
                break;

            case AwaitResultTransition awaitResultTransition:
                builder.AppendLine( $"{Indent3}Target: {awaitResultTransition.TargetNode.NodeLabel.Name}" );
                break;

            case LoopTransition loopTransition:
                builder.AppendLine( $"{Indent3}Body: {ExpressionToString( loopTransition.BodyNode )}" );
                break;

            case SwitchTransition switchTransition:
                builder.AppendLine( $"{Indent3}SwitchValue: {ExpressionToString( switchTransition.SwitchValue )}" );
                foreach ( var caseDefinition in switchTransition.CaseNodes )
                {
                    builder.AppendLine( $"{Indent4}Test: {string.Join( ",", caseDefinition.TestValues )}" );
                    builder.AppendLine( $"{Indent4}Body: {ExpressionToString( caseDefinition.Body )}" );
                }

                break;

            case TryCatchTransition tryCatchTransition:
                builder.AppendLine( $"{Indent3}Try: {ExpressionToString( tryCatchTransition.TryNode )}" );
                builder.AppendLine( $"{Indent3}Try: {ExpressionToString( tryCatchTransition.FinallyNode )}" );

                foreach ( var catchBlockDefinition in tryCatchTransition.CatchBlocks )
                {
                    builder.AppendLine( $"{Indent4}Catch: {catchBlockDefinition.Test.Name}" );
                    builder.AppendLine( $"{Indent4}Body: {ExpressionToString( catchBlockDefinition.Body )}" );
                }

                break;
        }

        return builder.ToString();
    }

    private static string ExpressionToString( Expression expr )
    {
        return expr switch
        {
            BinaryExpression binary => FormatBinaryExpression( binary ),
            MethodCallExpression methodCall => FormatMethodCallExpression( methodCall ),
            ConstantExpression constant => FormatConstantExpression( constant ),
            ParameterExpression param => FormatParameterExpression( param ),
            LabelExpression label => FormatLabelExpression( label ),
            ConditionalExpression conditional => FormatConditionalExpression( conditional ),
            LambdaExpression lambda => FormatLambdaExpression( lambda ),
            UnaryExpression unary => FormatUnaryExpression( unary ),
            MemberExpression member => FormatMemberExpression( member ),
            SwitchExpression cases => FormatSwitchExpression( cases ),
            LoopExpression loop => FormatLoopExpression( loop ),
            BlockExpression block => FormatBlockExpression( block ),
            NodeExpression node => FormatNodeExpression( node ),
            null => "null",
            _ => expr.ToString()
        };
    }

    private static string FormatNodeExpression( NodeExpression node )
    {
        return $"node {node.NodeLabel.Name}";
    }

    private static string FormatBlockExpression( BlockExpression block )
    {
        var builder = new StringBuilder();
        builder.Append( "block" );
        builder.AppendLine();

        foreach ( var blockExpression in block.Expressions )
        {
            builder.Append( Indent3 );
            builder.Append( ExpressionToString( blockExpression ) );
            builder.AppendLine();
        }

        return builder.ToString();
    }

    private static string FormatBinaryExpression( BinaryExpression binary )
    {
        return $"{binary.Left} {GetBinaryOperator( binary.NodeType )} {binary.Right}";
    }

    private static string FormatConditionalExpression( ConditionalExpression conditional )
    {
        var test = ExpressionToString( conditional.Test );
        var ifTrue = ExpressionToString( conditional.IfTrue );
        var ifFalse = ExpressionToString( conditional.IfFalse );

        return $"if ({test}) {{ {ifTrue} }} else {{ {ifFalse} }}";
    }

    private static string FormatConstantExpression( ConstantExpression constant )
    {
        switch ( constant.Value )
        {
            case string stringValue:
                return $"Constant String \"{stringValue}\"";

            default:
            {
                var typeName = constant.Type.Name;
                return $"Constant {typeName} {constant.Value}";
            }
        }
    }

    private static string FormatLabelExpression( LabelExpression label )
    {
        return $"Label {label.Target.Name}";
    }

    private static string FormatLambdaExpression( LambdaExpression lambda )
    {
        var parameters = string.Join( ", ", lambda.Parameters.Select( p => p.Name ) );
        var body = ExpressionToString( lambda.Body );

        return $"Lambda ({parameters}) => {body}";
    }

    private static string FormatLoopExpression( LoopExpression loop )
    {
        var builder = new StringBuilder();

        builder.AppendLine( $"loop => {ExpressionToString( loop.Body )}" );
        builder.Append( Indent2 );
        builder.AppendLine( $"break: {loop.BreakLabel?.Name}" );
        builder.Append( Indent2 );
        builder.AppendLine( $"continue: {loop.ContinueLabel?.Name}" );

        return builder.ToString();
    }

    private static string FormatMemberExpression( MemberExpression member )
    {
        return $"{member.Expression}.{member.Member.Name}";
    }

    private static string FormatMethodCallExpression( MethodCallExpression methodCall )
    {
        var declaringTypeName = methodCall.Method.DeclaringType?.Name;
        var methodName = methodCall.Method.Name;
        var arguments = string.Join( ", ", methodCall.Arguments.Select( ExpressionToString ) );

        return $"{declaringTypeName}.{methodName}({arguments})";
    }

    private static string FormatParameterExpression( ParameterExpression param )
    {
        return $"{param.Type.Name} {param.Name}";
    }

    private static string FormatSwitchExpression( SwitchExpression switchExpr )
    {
        var builder = new StringBuilder();

        builder.Append( $"switch ({ExpressionToString( switchExpr.SwitchValue )})" );

        foreach ( var caseExpr in switchExpr.Cases )
        {
            var caseValues = string.Join( ", ", caseExpr.TestValues.Select( ExpressionToString ) );

            builder.AppendLine();
            builder.Append( Indent3 );
            builder.Append( $"case {caseValues}:" );
            builder.AppendLine();
            builder.Append( Indent4 );
            builder.Append( ExpressionToString( caseExpr.Body ) );
        }

        if ( switchExpr.DefaultBody != null )
        {
            builder.AppendLine();
            builder.Append( Indent3 );
            builder.Append( "default:" );
            builder.AppendLine();
            builder.Append( Indent4 );
            builder.Append( ExpressionToString( switchExpr.DefaultBody ) );
        }

        return builder.ToString();

    }

    private static string FormatUnaryExpression( UnaryExpression unary )
    {
        return $"{GetUnaryOperator( unary.NodeType )}({ExpressionToString( unary.Operand )})";
    }

    private static string GetBinaryOperator( ExpressionType nodeType )
    {
        return nodeType switch
        {
            ExpressionType.Add => "+",
            ExpressionType.Subtract => "-",
            ExpressionType.Multiply => "*",
            ExpressionType.Divide => "/",
            ExpressionType.Modulo => "%",
            ExpressionType.AndAlso => "&&",
            ExpressionType.OrElse => "||",
            ExpressionType.Equal => "==",
            ExpressionType.NotEqual => "!=",
            ExpressionType.GreaterThan => ">",
            ExpressionType.GreaterThanOrEqual => ">=",
            ExpressionType.LessThan => "<",
            ExpressionType.LessThanOrEqual => "<=",
            ExpressionType.Assign => "=",
            ExpressionType.AddAssign => "+=",
            ExpressionType.SubtractAssign => "-=",
            ExpressionType.MultiplyAssign => "*=",
            ExpressionType.DivideAssign => "/=",
            ExpressionType.ModuloAssign => "%=",
            _ => nodeType.ToString()
        };
    }

    private static string GetUnaryOperator( ExpressionType nodeType )
    {
        return nodeType switch
        {
            ExpressionType.Negate => "-",
            ExpressionType.Not => "!",
            ExpressionType.Increment => "++",
            ExpressionType.Decrement => "--",
            ExpressionType.UnaryPlus => "+",
            _ => nodeType.ToString()
        };
    }
    private static string VariableToString( ParameterExpression expr )
    {
        return $"{TypeToString( expr.Type )} {expr.Name}";

        static string TypeToString( Type type )
        {
            return type switch
            {
                null => "null",
                { IsGenericType: true } => $"{type.Name.Split( '`' )[0]}<{string.Join( ", ", type.GetGenericArguments().Select( TypeToString ) )}>",
                { IsArray: true } => $"{TypeToString( type.GetElementType() )}[]",
                { IsByRef: true } => $"{TypeToString( type.GetElementType() )}&",
                { IsPointer: true } => $"{TypeToString( type.GetElementType() )}*",
                { IsGenericType: false } => type.Name
            };
        }
    }

}
