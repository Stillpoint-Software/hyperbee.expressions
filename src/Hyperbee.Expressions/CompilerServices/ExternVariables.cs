using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Reflection.Emit;
using static System.Linq.Expressions.Expression;

namespace Hyperbee.Expressions.CompilerServices;

/// <summary>
/// Carries the variables a coroutine body reads from its enclosing scope through fields on
/// the state machine, so the body itself stays closed.
/// </summary>
/// <remarks>
/// A closed body can be compiled once and embedded as a constant delegate. An open one has
/// to be materialized per call by the enclosing compiler's closure machinery, which costs
/// an order of magnitude more per state machine.
///
/// Copying a variable into a field shares it only when the body never assigns the variable
/// itself. That holds for a variable already hoisted into a cell -- the body assigns
/// <c>cell.Value</c>, never the cell -- which is the form <see cref="CoroutineClosureRewriter"/>
/// produces. A body that assigns a variable directly is left open, and the enclosing
/// compiler shares it as before.
/// </remarks>
internal sealed class ExternVariables
{
    private const string FieldPrefix = "__extern<";

    private readonly ParameterExpression[] _variables;
    private readonly string[] _fieldNames;
    private FieldBuilder[] _fields;

    private ExternVariables( ParameterExpression[] variables, string[] fieldNames )
    {
        _variables = variables;
        _fieldNames = fieldNames;
    }

    public int Count => _variables.Length;

    /// <summary>
    /// Selects the free variables that can be carried by field. Returns null when there are
    /// none, or when any of them is assigned by the body and so has to stay shared.
    /// </summary>
    public static ExternVariables Create(
        IReadOnlyList<ParameterExpression> declared,
        IReadOnlyList<Expression> expressions )
    {
        var free = FreeVariableScanner.Find( declared, expressions, out var assigned );

        if ( free.Count == 0 || assigned.Count > 0 )
            return null;

        // Copying a variable into a field is a snapshot unless the variable is itself a
        // shared cell -- the enclosing scope can write it while the machine is suspended.
        // Only cells qualify, which is what CoroutineClosureRewriter produces.

        foreach ( var variable in free )
        {
            if ( !variable.Type.IsGenericType || variable.Type.GetGenericTypeDefinition() != typeof( StrongBox<> ) )
                return null;
        }

        var variables = new ParameterExpression[free.Count];
        var fieldNames = new string[free.Count];

        var index = 0;

        foreach ( var variable in free )
        {
            variables[index] = variable;
            fieldNames[index] = $"{FieldPrefix}{index}>";
            index++;
        }

        return new ExternVariables( variables, fieldNames );
    }

    public void DefineFields( TypeBuilder typeBuilder )
    {
        _fields = new FieldBuilder[_variables.Length];

        for ( var index = 0; index < _variables.Length; index++ )
        {
            _fields[index] = typeBuilder.DefineField( _fieldNames[index], _variables[index].Type, FieldAttributes.Public );
        }
    }

    /// <summary>
    /// Assignments that copy each variable into its state-machine field. Emitted in the
    /// enclosing expression, where the variables are in scope.
    /// </summary>
    public IEnumerable<Expression> AssignFields( Expression stateMachine, Type stateMachineType )
    {
        for ( var index = 0; index < _variables.Length; index++ )
        {
            yield return Assign(
                Field( stateMachine, stateMachineType.GetField( _fieldNames[index] )! ),
                _variables[index]
            );
        }
    }

    /// <summary>
    /// Rewrites references to the variables as reads of their state-machine fields, closing
    /// the body.
    /// </summary>
    public Expression Close( Expression body, ParameterExpression stateMachine, Type stateMachineType )
    {
        var fields = new Dictionary<ParameterExpression, MemberExpression>( _variables.Length );

        for ( var index = 0; index < _variables.Length; index++ )
        {
            // A body emitted into a type that is still open resolves through the builders;
            // one built after CreateType() resolves by name on the finished type.
            var field = stateMachineType is TypeBuilder
                ? _fields[index]
                : (FieldInfo) stateMachineType.GetField( _fieldNames[index] )!;

            fields[_variables[index]] = Field( stateMachine, field );
        }

        return new ExternRewriter( fields ).Visit( body );
    }

    private sealed class ExternRewriter( IReadOnlyDictionary<ParameterExpression, MemberExpression> fields ) : ExpressionVisitor
    {
        protected override Expression VisitParameter( ParameterExpression node )
        {
            return fields.TryGetValue( node, out var field ) ? field : node;
        }
    }
}
