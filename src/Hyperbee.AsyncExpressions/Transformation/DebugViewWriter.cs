using System.Linq.Expressions;
using System.Text;

namespace Hyperbee.AsyncExpressions.Transformation;

internal static class DebugViewWriter
{
    public static void WriteTo( StringWriter writer, List<StateNode> nodes, IEnumerable<ParameterExpression> variables )
    {

        // variables
        var parameterExpressions = variables as ParameterExpression[] ?? variables.ToArray();
        if ( parameterExpressions.Length != 0 )
        {
            writer.WriteLine( "Variables" );

            foreach ( var expr in parameterExpressions )
            {
                writer.WriteLine( $"\t{VariableToString( expr )}" );
            }
        }

        writer.WriteLine();

        foreach ( var node in nodes.Where( node => node != null ) )
        {
            // label
            writer.WriteLine( $"{node.Label.Name}:" );

            // expressions

            if ( node.Expressions.Count > 0 )
            {
                writer.WriteLine( "\tExpressions" );

                foreach ( var expr in node.Expressions )
                {
                    writer.WriteLine( $"\t\t{ExpressionToString(expr)}" );
                }
            }

            // transitions

            writer.WriteLine( "\tTransition" );
            writer.WriteLine( $"\t\t{node.Transition?.GetType().Name ?? "Exit"}" );

            writer.WriteLine();
        }
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
            _ => expr.ToString()
        };
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
            builder.Append( Repeat( '\t', 3 ) );
            builder.Append( $"case {caseValues}:" );
            builder.AppendLine();
            builder.Append( Repeat( '\t', 4 ) );
            builder.Append( ExpressionToString( caseExpr.Body ) );
        }

        if ( switchExpr.DefaultBody != null )
        {
            builder.AppendLine();
            builder.Append( Repeat( '\t', 3 ) );
            builder.Append( "default:" );
            builder.AppendLine();
            builder.Append( Repeat( '\t', 4 ) );
            builder.Append( ExpressionToString( switchExpr.DefaultBody ) );
        }

        return builder.ToString();

        static string Repeat( char c, int count ) => new ( c, count );
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
