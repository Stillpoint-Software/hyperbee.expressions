using System.Linq.Expressions;

namespace Hyperbee.AsyncExpressions.Transformation;

internal static class DebugViewWriter
{
    public static void WriteTo( StringWriter writer, List<StateNode> nodes )
    {
        foreach ( var node in nodes )
        {
            if ( node == null )
                continue;

            // label

            writer.WriteLine( node.Label.Name + ":" );

            // variables

            if ( node.Variables.Count > 0 )
            {
                writer.WriteLine( "\tVariables" );
                writer.WriteLine( $"\t\t[{VariablesToString( node.Variables )}]" );
            }

            // expressions

            if ( node.Expressions.Count > 0 )
            {
                writer.WriteLine( "\tExpressions" );

                foreach ( var expr in node.Expressions )
                {
                    writer.WriteLine( $"\t\t{expr}" );
                }
            }

            // transitions

            var transition = node.Transition;

            writer.WriteLine( $"\t{transition?.GetType().Name ?? "Terminal"}" );

            if ( transition != null )
            {
                switch ( transition )
                {
                    case ConditionalTransition condNode:
                        writer.WriteLine( $"\t\tIfTrue -> {condNode.IfTrue?.Label}" );
                        writer.WriteLine( $"\t\tIfFalse -> {condNode.IfFalse?.Label}" );
                        break;
                    case SwitchTransition switchNode:
                        foreach ( var caseNode in switchNode.CaseNodes )
                        {
                            writer.WriteLine( $"\t\tCase -> {caseNode?.Label}" );
                        }

                        writer.WriteLine( $"\t\tDefault -> {switchNode.DefaultNode?.Label}" );
                        break;
                    case TryCatchTransition tryNode:
                        writer.WriteLine( $"\t\tTry -> {tryNode.TryNode?.Label}" );
                        foreach ( var catchNode in tryNode.CatchNodes )
                        {
                            writer.WriteLine( $"\t\tCatch -> {catchNode?.Label}" );
                        }

                        writer.WriteLine( $"\t\tFinally -> {tryNode.FinallyNode?.Label}" );
                        break;
                    case AwaitTransition awaitNode:
                        writer.WriteLine( $"\t\tCompletion -> {awaitNode.CompletionNode?.Label}" );
                        break;
                    case AwaitResultTransition awaitResultNode:
                        writer.WriteLine( $"\t\tGoto -> {awaitResultNode.TargetNode?.Label}" );
                        break;
                    case GotoTransition gotoNode:
                        writer.WriteLine( $"\t\tGoto -> {gotoNode.TargetNode?.Label}" );
                        break;
                }
            }

            if ( node.Transition == null )
            {
                writer.WriteLine( "\t\tExit" );
            }

            writer.WriteLine();
        }
    }

    private static string VariablesToString( IEnumerable<ParameterExpression> parameterExpressions )
    {
        return string.Join( ", ", parameterExpressions.Select( x => $"{TypeToString( x.Type )} {x.Name}" ) );

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
