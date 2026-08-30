using System.Linq.Expressions;
using System.Reflection.Emit;

namespace Hyperbee.Expressions.CompilerServices;

/// <summary>
/// The parts of building a coroutine state machine that do not depend on what kind of
/// coroutine it is.
/// </summary>
/// <remarks>
/// <para>
/// The async and enumerable builders were written separately and grew in parallel, method for
/// method, with a body-building pass that was identical to the byte. Nothing tied them
/// together, so an improvement to one silently did not reach the other -- and every coroutine
/// defect found while working on them was that same shape: the async builder cached its
/// reduction, pre-compiled MoveNext through the ambient builder, overrode VisitChildren and
/// emitted MoveNext into the type, and the enumerable builder did none of those.
/// </para>
/// <para>
/// What is common lives here so the next improvement lands once: the shape of a build, the
/// choice between emitting MoveNext into the type and holding it in a field, and the pass
/// that hoists the lowered body onto the machine's fields. What differs -- the base type, the
/// fields, what MoveNext returns, and how the machine is started and handed out -- is left to
/// the derived builder.
/// </para>
/// </remarks>
internal abstract class CoroutineStateMachineBuilder<TResult>
{
    protected readonly ModuleBuilder ModuleBuilder;
    protected readonly string TypeName;

    protected CoroutineStateMachineBuilder( ModuleBuilder moduleBuilder, string typeName )
    {
        ModuleBuilder = moduleBuilder;
        TypeName = typeName;
    }

    /// <summary>
    /// An assignment the machine needs before it is handed out, deferred because it names a
    /// field of a type the build has not closed yet.
    /// </summary>
    protected delegate Expression FieldAssignment( ParameterExpression stateMachine, Type stateMachineType );

    /// <summary>
    /// Builds the state machine type and the expression that creates, primes and returns it.
    /// </summary>
    protected Expression BuildStateMachine(
        Func<LoweringInfo> loweringTransformer,
        int id,
        ExternVariables externVariables,
        bool canEmitIntoType )
    {
        ArgumentNullException.ThrowIfNull( loweringTransformer, nameof( loweringTransformer ) );

        var context = new StateMachineContext
        {
            LoweringInfo = loweringTransformer(),
            ExternVariables = externVariables,
            CanEmitIntoType = canEmitIntoType
        };

        // A builder that can emit into a MethodBuilder makes MoveNext the machine's own
        // method. Otherwise the body becomes a delegate the machine holds in a field, which
        // is the only option for a compiler that cannot emit into a type under construction.

        var stateMachineType = context.CanEmitIntoType && CoroutineBuilderContext.Current is ICoroutineMethodBuilder methodBuilder
            ? BuildWithEmittedMoveNext( id, context, methodBuilder, out var assignments )
            : BuildWithDelegateMoveNext( id, context, out assignments );

        return BuildStartExpression( id, context, stateMachineType, assignments );
    }

    /// <summary>
    /// Defines the state machine type with MoveNext emitted into it, and closes the type.
    /// </summary>
    protected abstract Type BuildWithEmittedMoveNext(
        int id,
        StateMachineContext context,
        ICoroutineMethodBuilder methodBuilder,
        out List<FieldAssignment> assignments );

    /// <summary>
    /// Defines the state machine type with MoveNext as a delegate it holds in a field, and
    /// closes the type.
    /// </summary>
    protected abstract Type BuildWithDelegateMoveNext(
        int id,
        StateMachineContext context,
        out List<FieldAssignment> assignments );

    /// <summary>
    /// Creates the machine, applies <paramref name="assignments"/> and whatever else it needs
    /// to run, and yields it to the enclosing expression.
    /// </summary>
    protected abstract Expression BuildStartExpression(
        int id,
        StateMachineContext context,
        Type stateMachineType,
        List<FieldAssignment> assignments );

    /// <summary>
    /// The lowered body, with its variables hoisted onto the machine's fields, followed by
    /// the antecedents that close it.
    /// </summary>
    protected static List<Expression> CreateBody( StateMachineContext context, params Expression[] antecedents )
    {
        var stateMachineInfo = context.StateMachineInfo;
        var loweringInfo = context.LoweringInfo;

        var scopes = loweringInfo.Scopes;

        // Create the body expressions

        var firstScope = scopes[0];

        var jumpTable = JumpTableBuilder.Build(
            firstScope,
            scopes,
            stateMachineInfo.StateField
        );

        // hoist variables, then the antecedents that close the body

        var expressions = firstScope.GetExpressions( context );
        var hoistingVisitor = new HoistingVisitor( stateMachineInfo.StateMachine, context.VariableFields, context.ExternVariables );

        var bodyExpressions = new List<Expression>( expressions.Count + antecedents.Length + 1 )
        {
            hoistingVisitor.Visit( jumpTable )
        };

        for ( var index = 0; index < expressions.Count; index++ )
        {
            bodyExpressions.Add( hoistingVisitor.Visit( expressions[index] ) );
        }

        bodyExpressions.AddRange( antecedents );

        return bodyExpressions;
    }
}
