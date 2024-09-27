using System.Linq.Expressions;

namespace Hyperbee.AsyncExpressions.Transformation;

internal static class DiagnosticHelper
{
    public static void WriteNodes( List<StateNode> nodes )
    {
        foreach ( var node in nodes )
        {
            if ( node == null )
                continue;

            // label

            Console.WriteLine( node.Label.Name + ":" );

            // variables

            if ( node.Variables.Count > 0 )
            {
                Console.WriteLine( "\tVariables" );
                Console.WriteLine( $"\t\t[{VariablesToString( node.Variables )}]" );
            }

            // expressions

            if ( node.Expressions.Count > 0 )
            {
                Console.WriteLine( "\tExpressions" );

                foreach ( var expr in node.Expressions )
                {
                    Console.WriteLine( $"\t\t{expr}" );
                }
            }

            // transitions

            var transition = node.Transition;

            Console.WriteLine( $"\t{transition?.GetType().Name ?? "Terminal"}" );

            if ( transition != null )
            {
                switch ( transition )
                {
                    case ConditionalTransition condNode:
                        Console.WriteLine( $"\t\tIfTrue -> {condNode.IfTrue?.Label}" );
                        Console.WriteLine( $"\t\tIfFalse -> {condNode.IfFalse?.Label}" );
                        break;
                    case SwitchTransition switchNode:
                        foreach ( var caseNode in switchNode.CaseNodes )
                        {
                            Console.WriteLine( $"\t\tCase -> {caseNode?.Label}" );
                        }
                        Console.WriteLine( $"\t\tDefault -> {switchNode.DefaultNode?.Label}" );
                        break;
                    case TryCatchTransition tryNode:
                        Console.WriteLine( $"\t\tTry -> {tryNode.TryNode?.Label}" );
                        foreach ( var catchNode in tryNode.CatchNodes )
                        {
                            Console.WriteLine( $"\t\tCatch -> {catchNode?.Label}" );
                        }
                        Console.WriteLine( $"\t\tFinally -> {tryNode.FinallyNode?.Label}" );
                        break;
                    case AwaitTransition awaitNode:
                        Console.WriteLine( $"\t\tCompletion -> {awaitNode.CompletionNode?.Label}" );
                        break;
                    case AwaitResultTransition awaitResultNode:
                        Console.WriteLine( $"\t\tGoto -> {awaitResultNode.TargetNode?.Label}" );
                        break;
                    case GotoTransition gotoNode:
                        Console.WriteLine( $"\t\tGoto -> {gotoNode.TargetNode?.Label}" );
                        break;
                }
            }

            if ( node.Transition == null )
            {
                Console.WriteLine( "\t\tExit" );
            }

            Console.WriteLine();
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
