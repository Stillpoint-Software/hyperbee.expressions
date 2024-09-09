using System.Linq.Expressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Hyperbee.AsyncExpressions;

public class RoslynStateMachineBuilder
{
    private BlockExpression _blockSource;

    public void SetSource( BlockExpression blockSource )
    {
        _blockSource = blockSource;
    }

    // CreateStateMachine method: Generates the state machine struct
    public CompilationUnitSyntax CreateStateMachine( string machineName, Type resultType )
    {
        // Create namespace for the state machine
        var namespaceDeclaration = SyntaxFactory.NamespaceDeclaration( SyntaxFactory.ParseName( "Hyperbee.AsyncExpressions" ) )
            .NormalizeWhitespace();

        // Create struct declaration: public struct StateMachineType : IAsyncStateMachine
        var structDeclaration = SyntaxFactory.StructDeclaration( machineName )
            .AddModifiers( SyntaxFactory.Token( SyntaxKind.PublicKeyword ) )
            .AddBaseListTypes(
                SyntaxFactory.SimpleBaseType( SyntaxFactory.ParseTypeName( "IAsyncStateMachine" ) )
            );

        // Add fields: public int _state; public TResult _finalResult;
        var stateField = CreateField( "_state", "int", SyntaxKind.PublicKeyword );
        var finalResultField = CreateField( "_finalResult", "TResult", SyntaxKind.PublicKeyword );

        structDeclaration = structDeclaration.AddMembers( stateField, finalResultField );

        // Add builder field with initialization
        var resultTypeSyntax = BuildTypeSyntax( resultType );
        var builderField = CreateBuilderField( resultTypeSyntax );
        structDeclaration = structDeclaration.AddMembers( builderField );

        // Add state fields
        var memberFields = CreateStateFields( _blockSource );
        structDeclaration = structDeclaration.AddMembers( memberFields );

        // Add constructor
        var constructor = CreateConstructor( machineName );
        structDeclaration = structDeclaration.AddMembers( constructor );

        // Add MoveNext method 
        var moveNextMethod = CreateMoveNextMethod( _blockSource );
        structDeclaration = structDeclaration.AddMembers( moveNextMethod );

        // Add SetStateMachine method
        var setStateMachineMethod = CreateSetStateMachineMethod();
        structDeclaration = structDeclaration.AddMembers( setStateMachineMethod );

        // Add CreateStateMachineRunnerMethod method
        var stateMachineRunnerMethod = CreateStateMachineRunnerMethod();
        structDeclaration = structDeclaration.AddMembers( stateMachineRunnerMethod );


        // Return the final compilation unit
        return SyntaxFactory.CompilationUnit()
            .AddUsings(
                SyntaxFactory.UsingDirective( SyntaxFactory.ParseName( "System" ) ),
                SyntaxFactory.UsingDirective( SyntaxFactory.ParseName( "System.Runtime.CompilerServices" ) ),
                SyntaxFactory.UsingDirective( SyntaxFactory.ParseName( "System.Threading.Tasks" ) )
            )
            .AddMembers( namespaceDeclaration.AddMembers( structDeclaration ) )
            .NormalizeWhitespace();
    }

    // Build method stub for later compilation
    public void Build()
    {
        // This method will be responsible for compiling the generated syntax tree
        // to produce the final assembly.
    }

    // Helper to create fields for the state machine struct
    private FieldDeclarationSyntax CreateField( string fieldName, string fieldType, SyntaxKind accessibility )
    {
        return SyntaxFactory.FieldDeclaration(
                SyntaxFactory.VariableDeclaration( SyntaxFactory.ParseTypeName( fieldType ) )
                    .AddVariables( SyntaxFactory.VariableDeclarator( fieldName ) )
            )
            .AddModifiers( SyntaxFactory.Token( accessibility ) );
    }

    // Constructor: public StateMachineType() { }
    private ConstructorDeclarationSyntax CreateConstructor( string typeName )
    {
        return SyntaxFactory.ConstructorDeclaration( typeName )
            .AddModifiers( SyntaxFactory.Token( SyntaxKind.PublicKeyword ) )
            .WithBody( SyntaxFactory.Block(
                // Initialize state
                SyntaxFactory.ExpressionStatement(
                    SyntaxFactory.AssignmentExpression(
                        SyntaxKind.SimpleAssignmentExpression,
                        SyntaxFactory.MemberAccessExpression(
                            SyntaxKind.SimpleMemberAccessExpression,
                            SyntaxFactory.ThisExpression(),
                            SyntaxFactory.IdentifierName( "_state" )
                        ),
                        SyntaxFactory.LiteralExpression( SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal( 0 ) )
                    )
                )
            ) );
    }

    // CreateFieldDeclaration method: Initializes _builder directly in the field declaration
    private FieldDeclarationSyntax CreateBuilderField( TypeSyntax resultTypeSyntax )
    {
        /*
            _builder initialized in the field declaration
            private AsyncTaskMethodBuilder<int> _builder = AsyncTaskMethodBuilder<int>.Create(); // Example where TResult is an int
        */

        return SyntaxFactory.FieldDeclaration(
            SyntaxFactory.VariableDeclaration(
                SyntaxFactory.GenericName( "AsyncTaskMethodBuilder" )
                    .WithTypeArgumentList(
                        SyntaxFactory.TypeArgumentList(
                            SyntaxFactory.SingletonSeparatedList( resultTypeSyntax )
                        )
                    )
            ).AddVariables(
                SyntaxFactory.VariableDeclarator( "_builder" )
                    .WithInitializer(
                        SyntaxFactory.EqualsValueClause(
                            SyntaxFactory.InvocationExpression(
                                SyntaxFactory.MemberAccessExpression(
                                    SyntaxKind.SimpleMemberAccessExpression,
                                    SyntaxFactory.GenericName( "AsyncTaskMethodBuilder" )
                                        .WithTypeArgumentList(
                                            SyntaxFactory.TypeArgumentList(
                                                SyntaxFactory.SingletonSeparatedList( resultTypeSyntax )
                                            )
                                        ),
                                    SyntaxFactory.IdentifierName( "Create" )
                                )
                            )
                        )
                    )
            )
        ).AddModifiers( SyntaxFactory.Token( SyntaxKind.PrivateKeyword ) );
    }

    private MemberDeclarationSyntax[] CreateStateFields( BlockExpression block )
    {
        /*
            This method generates fields equivalent to the following C# code:

            // Fields for variables (e.g., _result_0, _result_2)
            private int _result_0; // Stores result from state 0
            private int _result_2; // Stores result from state 2

            // Fields for awaiters (e.g., _awaiter_0, _awaiter_1, _awaiter_2)
            private TaskAwaiter<int> _awaiter_0; // Awaiter for state 0 (Task<int>)
            private TaskAwaiter _awaiter_1;      // Awaiter for state 1 (Task)
            private TaskAwaiter<int> _awaiter_2; // Awaiter for state 2 (Task<int>)
        */

        var fields = new List<FieldDeclarationSyntax>();

        // Create fields for variables
        foreach ( var variable in block.Variables )
        {
            var field = SyntaxFactory.FieldDeclaration(
                    SyntaxFactory.VariableDeclaration( SyntaxFactory.ParseTypeName( variable.Type.Name ) )
                        .AddVariables( SyntaxFactory.VariableDeclarator( $"_{variable.Name}" ) )
                )
                .AddModifiers( SyntaxFactory.Token( SyntaxKind.PublicKeyword ) );

            fields.Add( field );
        }

        // Create fields for awaiters, one for each child block
        for ( var i = 0; i < block.Expressions.Count; i++ )
        {
            var awaiterField = SyntaxFactory.FieldDeclaration(
                    SyntaxFactory.VariableDeclaration( SyntaxFactory.ParseTypeName( "System.Runtime.CompilerServices.TaskAwaiter" ) )
                        .AddVariables( SyntaxFactory.VariableDeclarator( $"_awaiter_{i}" ) )
                )
                .AddModifiers( SyntaxFactory.Token( SyntaxKind.PublicKeyword ) );

            fields.Add( awaiterField );
        }

        return fields.Cast<MemberDeclarationSyntax>().ToArray(); // Return the list of field declarations
    }

    // MoveNext method: Generates logic for state transitions based on the expression block
    private MethodDeclarationSyntax CreateMoveNextMethod( BlockExpression block )
    {
        /*
        This method generates the equivalent of the following C# code for handling state transitions:

        public void MoveNext()
        {
            try
            {
                if (_state == 0)
                {
                    var task0 = ChildBlock0(); // Call child block 0 which returns Task<int>
                    _awaiter_0 = task0.ConfigureAwait(false).GetAwaiter();

                    if (!_awaiter_0.IsCompleted)
                    {
                        _builder.AwaitUnsafeOnCompleted(ref _awaiter_0, ref this);
                        return;
                    }

                    // Store the result of state 0
                    _result_0 = _awaiter_0.GetResult();
                    _state = 1;
                }

                if (_state == 1)
                {
                    var task1 = ChildBlock1(); // Call child block 1 which returns Task
                    _awaiter_1 = task1.ConfigureAwait(false).GetAwaiter();

                    if (!_awaiter_1.IsCompleted)
                    {
                        _builder.AwaitUnsafeOnCompleted(ref _awaiter_1, ref this);
                        return;
                    }

                    _awaiter_1.GetResult();
                    _state = 2;
                }

                if (_state == 2)
                {
                    var task2 = ChildBlock2(_result_0); // Pass result from state 0 to child block 2
                    _awaiter_2 = task2.ConfigureAwait(false).GetAwaiter();

                    if (!_awaiter_2.IsCompleted)
                    {
                        _builder.AwaitUnsafeOnCompleted(ref _awaiter_2, ref this);
                        return;
                    }

                    _result_2 = _awaiter_2.GetResult(); // Store result from state 2
                    _state = 3;
                }

                if (_state == 3)
                {
                    _finalResult = _result_0 + _result_2; // Sum results of state 0 and state 2
                    _builder.SetResult(_finalResult);
                }
            }
            catch (Exception ex)
            {
                _builder.SetException(ex);
            }
        }
        */
        var statements = new List<StatementSyntax>();

        // Loop over child blocks and create states
        for ( var i = 0; i < block.Expressions.Count; i++ )
        {
            // Compile the child block into a delegate that can be invoked at runtime
            var compiledBlock = CompileSubBlock( block.Expressions[i] );

            // Check current state
            var stateCheck = SyntaxFactory.IfStatement(
                SyntaxFactory.BinaryExpression( SyntaxKind.EqualsExpression,
                    SyntaxFactory.IdentifierName( "_state" ),
                    SyntaxFactory.LiteralExpression( SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal( i ) )
                ),
                SyntaxFactory.Block(
                    // Invoke the compiled child block to get the task for this state
                    GenerateTaskInvocation( i, compiledBlock ),

                    // Generate state block logic
                    GenerateStateBlock( i )
                )
            );

            statements.Add( stateCheck );
        }

        // Wrap all states in a try-catch block and return the method
        return SyntaxFactory.MethodDeclaration(
                SyntaxFactory.PredefinedType( SyntaxFactory.Token( SyntaxKind.VoidKeyword ) ),
                "MoveNext"
            )
            .AddModifiers( SyntaxFactory.Token( SyntaxKind.PublicKeyword ) )
            .WithBody( SyntaxFactory.Block( GenerateTryCatchBlock( statements ) ) );

        // ---- Local Functions ----

        // Generates the Try-Catch block that wraps the state transitions
        static TryStatementSyntax GenerateTryCatchBlock( List<StatementSyntax> stateStatements )
        {
            return SyntaxFactory.TryStatement(
                SyntaxFactory.Block( stateStatements ), // Add all state transitions
                SyntaxFactory.SingletonList(
                    SyntaxFactory.CatchClause()
                        .WithCatchKeyword( SyntaxFactory.Token( SyntaxKind.CatchKeyword ) )
                        .WithDeclaration(
                            SyntaxFactory.CatchDeclaration(
                                SyntaxFactory.ParseTypeName( "Exception" )
                            ).WithIdentifier( SyntaxFactory.Identifier( "ex" ) )
                        ).WithBlock(
                            SyntaxFactory.Block(
                                SyntaxFactory.ExpressionStatement(
                                    SyntaxFactory.InvocationExpression(
                                        SyntaxFactory.MemberAccessExpression( SyntaxKind.SimpleMemberAccessExpression,
                                            SyntaxFactory.IdentifierName( "_builder" ),
                                            SyntaxFactory.IdentifierName( "SetException" )
                                        )
                                    ).WithArgumentList(
                                        SyntaxFactory.ArgumentList(
                                            SyntaxFactory.SingletonSeparatedList(
                                                SyntaxFactory.Argument( SyntaxFactory.IdentifierName( "ex" ) )
                                            )
                                        )
                                    )
                                )
                            )
                        )
                ),
                null // no Finally clause
            );
        }

        // Local function to compile a sub-block into a delegate
        static Delegate CompileSubBlock( Expression expression )
        {
            return Expression.Lambda( expression ).Compile();
        }

        // Local function to generate the logic for invoking a compiled sub-block
        static StatementSyntax GenerateTaskInvocation( int i, Delegate compiledBlock )
        {
            return SyntaxFactory.LocalDeclarationStatement(
                SyntaxFactory.VariableDeclaration( SyntaxFactory.IdentifierName( "var" ) )
                    .AddVariables(
                        SyntaxFactory.VariableDeclarator( $"task{i}" )
                            .WithInitializer(
                                SyntaxFactory.EqualsValueClause(
                                    SyntaxFactory.InvocationExpression( SyntaxFactory.IdentifierName( compiledBlock.Method.Name ) )
                                )
                            )
                    )
            );
        }

        // Local function to handle the state check and task awaiting
        static BlockSyntax GenerateStateBlock( int i )
        {
            return SyntaxFactory.Block(
                // _awaiter# = task.ConfigureAwait(false).GetAwaiter();
                SyntaxFactory.ExpressionStatement(
                    SyntaxFactory.AssignmentExpression(
                        SyntaxKind.SimpleAssignmentExpression,
                        SyntaxFactory.IdentifierName( $"_awaiter_{i}" ),
                        SyntaxFactory.InvocationExpression(
                            SyntaxFactory.MemberAccessExpression(
                                SyntaxKind.SimpleMemberAccessExpression,
                                SyntaxFactory.InvocationExpression(
                                    SyntaxFactory.MemberAccessExpression(
                                        SyntaxKind.SimpleMemberAccessExpression,
                                        SyntaxFactory.IdentifierName( $"task{i}" ),
                                        SyntaxFactory.IdentifierName( "ConfigureAwait" )
                                    )
                                ).WithArgumentList(
                                    SyntaxFactory.ArgumentList(
                                        SyntaxFactory.SingletonSeparatedList( SyntaxFactory.Argument(
                                            SyntaxFactory.LiteralExpression( SyntaxKind.FalseLiteralExpression )
                                        ) )
                                    )
                                ),
                                SyntaxFactory.IdentifierName( "GetAwaiter" )
                            )
                        )
                    )
                ),

                // if (!_awaiter#.IsCompleted)
                SyntaxFactory.IfStatement(
                    SyntaxFactory.PrefixUnaryExpression( SyntaxKind.LogicalNotExpression,
                        SyntaxFactory.MemberAccessExpression(
                            SyntaxKind.SimpleMemberAccessExpression,
                            SyntaxFactory.IdentifierName( $"_awaiter_{i}" ),
                            SyntaxFactory.IdentifierName( "IsCompleted" )
                        )
                    ),
                    SyntaxFactory.Block(
                        // _builder.AwaitUnsafeOnCompleted(ref _awaiter#, ref this)
                        SyntaxFactory.ExpressionStatement(
                            SyntaxFactory.InvocationExpression(
                                SyntaxFactory.MemberAccessExpression(
                                    SyntaxKind.SimpleMemberAccessExpression,
                                    SyntaxFactory.IdentifierName( "_builder" ),
                                    SyntaxFactory.IdentifierName( "AwaitUnsafeOnCompleted" )
                                )
                            ).WithArgumentList(
                                SyntaxFactory.ArgumentList( SyntaxFactory.SeparatedList( new[]
                                {
                                    SyntaxFactory.Argument( SyntaxFactory.IdentifierName( $"_awaiter_{i}" ) )
                                        .WithRefOrOutKeyword( SyntaxFactory.Token( SyntaxKind.RefKeyword ) ),
                                    SyntaxFactory.Argument( SyntaxFactory.IdentifierName( "this" ) )
                                        .WithRefOrOutKeyword( SyntaxFactory.Token( SyntaxKind.RefKeyword ) )
                                } ) )
                            )
                        ),
                        SyntaxFactory.ReturnStatement() // Return to await completion
                    )
                ),

                // _awaiter#.GetResult();
                SyntaxFactory.ExpressionStatement(
                    SyntaxFactory.InvocationExpression(
                        SyntaxFactory.MemberAccessExpression(
                            SyntaxKind.SimpleMemberAccessExpression,
                            SyntaxFactory.IdentifierName( $"_awaiter_{i}" ),
                            SyntaxFactory.IdentifierName( "GetResult" )
                        )
                    )
                ),

                // _state = i + 1;
                SyntaxFactory.ExpressionStatement(
                    SyntaxFactory.AssignmentExpression(
                        SyntaxKind.SimpleAssignmentExpression,
                        SyntaxFactory.IdentifierName( "_state" ),
                        SyntaxFactory.LiteralExpression( SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal( i + 1 ) )
                    )
                )
            );
        }
    }

    // SetStateMachine method: public void SetStateMachine(IAsyncStateMachine stateMachine)
    private MethodDeclarationSyntax CreateSetStateMachineMethod()
    {
        /*
        public void SetStateMachine(IAsyncStateMachine stateMachine)
        {
            _builder.SetStateMachine(stateMachine);
        }
        */
        return SyntaxFactory.MethodDeclaration( SyntaxFactory.PredefinedType( SyntaxFactory.Token( SyntaxKind.VoidKeyword ) ), "SetStateMachine" )
            .AddModifiers( SyntaxFactory.Token( SyntaxKind.PublicKeyword ) )
            .AddParameterListParameters(
                SyntaxFactory.Parameter( SyntaxFactory.Identifier( "stateMachine" ) )
                    .WithType( SyntaxFactory.ParseTypeName( "IAsyncStateMachine" ) )
            )
            .WithBody( SyntaxFactory.Block(
                SyntaxFactory.ExpressionStatement(
                    SyntaxFactory.InvocationExpression(
                            SyntaxFactory.MemberAccessExpression(
                                SyntaxKind.SimpleMemberAccessExpression,
                                SyntaxFactory.IdentifierName( "_builder" ),
                                SyntaxFactory.IdentifierName( "SetStateMachine" )
                            )
                        )
                        .WithArgumentList(
                            SyntaxFactory.ArgumentList(
                                SyntaxFactory.SingletonSeparatedList(
                                    SyntaxFactory.Argument( SyntaxFactory.IdentifierName( "stateMachine" ) )
                                )
                            )
                        )
                )
            ) );
    }

    private MethodDeclarationSyntax CreateStateMachineRunnerMethod()
    {
        // Create the variable declaration: var stateMachine = new StateMachine();
        var stateMachineDeclaration = SyntaxFactory.LocalDeclarationStatement(
            SyntaxFactory.VariableDeclaration( SyntaxFactory.IdentifierName( "var" ) )
                .AddVariables(
                    SyntaxFactory.VariableDeclarator( SyntaxFactory.Identifier( "stateMachine" ) )
                        .WithInitializer(
                            SyntaxFactory.EqualsValueClause(
                                SyntaxFactory.ObjectCreationExpression( SyntaxFactory.IdentifierName( "StateMachine" ) )
                                    .WithArgumentList( SyntaxFactory.ArgumentList() )
                            )
                        )
                )
        );

        // Create the builder.Start(ref stateMachine) statement
        var startMethodCall = SyntaxFactory.ExpressionStatement(
            SyntaxFactory.InvocationExpression(
                    SyntaxFactory.MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        SyntaxFactory.MemberAccessExpression(
                            SyntaxKind.SimpleMemberAccessExpression,
                            SyntaxFactory.IdentifierName( "stateMachine" ),
                            SyntaxFactory.IdentifierName( "_builder" )
                        ),
                        SyntaxFactory.IdentifierName( "Start" )
                    )
                )
                .WithArgumentList(
                    SyntaxFactory.ArgumentList(
                        SyntaxFactory.SingletonSeparatedList(
                            SyntaxFactory.Argument( SyntaxFactory.IdentifierName( "stateMachine" ) )
                                .WithRefOrOutKeyword( SyntaxFactory.Token( SyntaxKind.RefKeyword ) )
                        )
                    )
                )
        );

        // Create the return statement: return _builder.Task;
        var returnStatement = SyntaxFactory.ReturnStatement(
            SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                SyntaxFactory.IdentifierName( "_builder" ),
                SyntaxFactory.IdentifierName( "Task" )
            )
        );

        // Combine all statements into a method body
        return SyntaxFactory.MethodDeclaration(
                SyntaxFactory.PredefinedType( SyntaxFactory.Token( SyntaxKind.VoidKeyword ) ),
                SyntaxFactory.Identifier( "CreateStateMachineRunner" )
            )
            .AddModifiers( SyntaxFactory.Token( SyntaxKind.PrivateKeyword ) )
            .WithBody(
                SyntaxFactory.Block(
                    stateMachineDeclaration, // Declare stateMachine variable
                    startMethodCall, // Call _builder.Start(ref stateMachine)
                    returnStatement // Return _builder.Task
                )
            );
    }


    // Convert a Type to TypeSyntax and typeName
    private static TypeSyntax BuildTypeSyntax( Type type )
    {
        TypeSyntax typeSyntax;

        if ( type.IsGenericType )
        {
            // Handle generic types like Task<int> or List<string>
            var genericTypeName = type.GetGenericTypeDefinition().Name.Split( '`' )[0]; // Get the base generic name
            var genericArgs = type.GetGenericArguments();
            var genericArgsSyntax = SyntaxFactory.SeparatedList<TypeSyntax>();

            // Recursively build TypeSyntax for each generic argument
            foreach ( var arg in genericArgs )
            {
                var argSyntax = BuildTypeSyntax( arg );
                genericArgsSyntax = genericArgsSyntax.Add( argSyntax );
            }

            typeSyntax = SyntaxFactory.GenericName( genericTypeName )
                .WithTypeArgumentList(
                    SyntaxFactory.TypeArgumentList( genericArgsSyntax )
                );
        }
        else if ( type.IsArray )
        {
            // Handle array types (e.g., int[], string[])
            var elementType = BuildTypeSyntax( type.GetElementType() );
            typeSyntax = SyntaxFactory.ArrayType( elementType )
                .WithRankSpecifiers( SyntaxFactory.SingletonList(
                    SyntaxFactory.ArrayRankSpecifier(
                        SyntaxFactory.SingletonSeparatedList<ExpressionSyntax>(
                            SyntaxFactory.OmittedArraySizeExpression() )
                    )
                ) );
        }
        else
        {
            // Handle non-generic, non-array types (e.g., int, string)
            typeSyntax = SyntaxFactory.IdentifierName( type.Name );
        }

        return typeSyntax;
    }
}



