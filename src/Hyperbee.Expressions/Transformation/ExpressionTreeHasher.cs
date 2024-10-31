using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;

namespace Hyperbee.Expressions.Transformation;

public class ExpressionTreeHasher : ExpressionVisitor
{
    private int _hashCode;
    private readonly bool _ignoreParameterNames;
    private readonly Dictionary<ParameterExpression, int> _parameterMap;

    public ExpressionTreeHasher( bool ignoreParameterNames = true )
    {
        _ignoreParameterNames = ignoreParameterNames;
        _parameterMap = new Dictionary<ParameterExpression, int>();
    }

    public int ComputeHash( Expression expression )
    {
        _hashCode = 17; // Start with a prime number
        _parameterMap.Clear();

        Visit( expression );
        return _hashCode;
    }

    public override Expression Visit( Expression node )
    {
        if ( node == null )
        {
            _hashCode = _hashCode * 23 + 0;
            return null;
        }

        // Combine hash code with the node's type and node type
        _hashCode = _hashCode * 23 + node.NodeType.GetHashCode();
        _hashCode = _hashCode * 23 + GetTypeHashCode( node.Type );

        return base.Visit( node );
    }

    protected override Expression VisitExtension( Expression node )
    {
        _hashCode = _hashCode * 23 + GetTypeHashCode( node.GetType() );

        // Use VisitChildren, which is overridden in custom expressions
        return base.VisitExtension( node );
    }

    protected override Expression VisitBinary( BinaryExpression node )
    {
        // Combine properties specific to BinaryExpression
        _hashCode = _hashCode * 23 + GetMethodHashCode( node.Method );
        _hashCode = _hashCode * 23 + node.IsLifted.GetHashCode();
        _hashCode = _hashCode * 23 + node.IsLiftedToNull.GetHashCode();

        return base.VisitBinary( node );
    }

    protected override Expression VisitConstant( ConstantExpression node )
    {
        if ( node.Value != null )
        {
            Type valueType = node.Value.GetType();

            if ( valueType.IsPrimitive || valueType == typeof(string) || valueType.IsEnum )
            {
                // Handle primitive types, strings, and enums
                _hashCode = _hashCode * 23 + GetTypeHashCode( valueType );
                _hashCode = _hashCode * 23 + node.Value.GetHashCode();
            }
            else if ( typeof(IEnumerable<Expression>).IsAssignableFrom( valueType ) )
            {
                // Handle collections of expressions, if any
                foreach ( var expr in (IEnumerable<Expression>) node.Value )
                {
                    Visit( expr );
                }
            }
            else
            {
                // Handle reference types
                _hashCode = _hashCode * 23 + GetTypeHashCode( valueType );
                _hashCode = _hashCode * 23 + HashCodeGenerator.GenerateHashCode( node.Value );
            }
        }
        else
        {
            // Handle null constants
            _hashCode = _hashCode * 23 + 0;
        }

        return node;
    }

    protected override Expression VisitParameter( ParameterExpression node )
    {
        // Combine parameter type
        _hashCode = _hashCode * 23 + GetTypeHashCode( node.Type );

        if ( _ignoreParameterNames )
        {
            if ( !_parameterMap.TryGetValue( node, out int index ) )
            {
                index = _parameterMap.Count;
                _parameterMap[node] = index;
            }
            _hashCode = _hashCode * 23 + index;
        }
        else
        {
            // Use the parameter name, handling null appropriately
            _hashCode = _hashCode * 23 + (node.Name?.GetHashCode() ?? 0);
        }

        return node; // No need to call base.VisitParameter as there are no child nodes
    }

    protected override Expression VisitUnary( UnaryExpression node )
    {
        // Combine properties specific to UnaryExpression
        _hashCode = _hashCode * 23 + GetMethodHashCode( node.Method );
        _hashCode = _hashCode * 23 + node.IsLifted.GetHashCode();
        _hashCode = _hashCode * 23 + node.IsLiftedToNull.GetHashCode();

        return base.VisitUnary( node );
    }

    protected override Expression VisitMethodCall( MethodCallExpression node )
    {
        // Combine method info
        _hashCode = _hashCode * 23 + GetMethodHashCode( node.Method );

        return base.VisitMethodCall( node );
    }

    protected override Expression VisitMember( MemberExpression node )
    {
        // Combine member info
        _hashCode = _hashCode * 23 + GetMemberHashCode( node.Member );

        return base.VisitMember( node );
    }

    protected override Expression VisitNew( NewExpression node )
    {
        // Combine constructor info
        if ( node.Constructor != null )
        {
            _hashCode = _hashCode * 23 + GetMethodHashCode( node.Constructor );
        }

        // Combine members if any
        if ( node.Members != null )
        {
            foreach ( var member in node.Members )
            {
                _hashCode = _hashCode * 23 + GetMemberHashCode( member );
            }
        }

        return base.VisitNew( node );
    }

    protected override Expression VisitTypeBinary( TypeBinaryExpression node )
    {
        // Combine type operand
        _hashCode = _hashCode * 23 + GetTypeHashCode( node.TypeOperand );

        return base.VisitTypeBinary( node );
    }

    protected override Expression VisitIndex( IndexExpression node )
    {
        // Combine indexer info if any
        if ( node.Indexer != null )
        {
            _hashCode = _hashCode * 23 + GetMemberHashCode( node.Indexer );
        }

        return base.VisitIndex( node );
    }

    // The following overrides are necessary because the base ExpressionVisitor does not traverse
    // the child nodes of MemberBinding and ElementInit expressions by default.

    protected override MemberBinding VisitMemberBinding( MemberBinding node )
    {
        _hashCode = _hashCode * 23 + node.BindingType.GetHashCode();
        _hashCode = _hashCode * 23 + GetMemberHashCode( node.Member );

        // Explicitly visit the member binding
        switch ( node )
        {
            case MemberAssignment assignment:
                VisitMemberAssignment( assignment );
                break;
            case MemberMemberBinding memberBinding:
                VisitMemberMemberBinding( memberBinding );
                break;
            case MemberListBinding listBinding:
                VisitMemberListBinding( listBinding );
                break;
        }

        return node;
    }

    protected override MemberAssignment VisitMemberAssignment( MemberAssignment node )
    {
        Visit( node.Expression );
        return node; // No need to call base.VisitMemberAssignment as it's an abstract method
    }

    protected override MemberMemberBinding VisitMemberMemberBinding( MemberMemberBinding node )
    {
        foreach ( var binding in node.Bindings )
        {
            VisitMemberBinding( binding );
        }
        return node; // No need to call base.VisitMemberMemberBinding as it's an abstract method
    }

    protected override MemberListBinding VisitMemberListBinding( MemberListBinding node )
    {
        foreach ( var initializer in node.Initializers )
        {
            VisitElementInit( initializer );
        }
        return node; // No need to call base.VisitMemberListBinding as it's an abstract method
    }

    protected override ElementInit VisitElementInit( ElementInit node )
    {
        _hashCode = _hashCode * 23 + GetMethodHashCode( node.AddMethod );

        foreach ( var argument in node.Arguments )
        {
            Visit( argument );
        }
        return node; // No need to call base.VisitElementInit as it's an abstract method
    }

    // Override VisitLabelTarget to handle LabelTarget nodes

    protected override LabelTarget VisitLabelTarget( LabelTarget node )
    {
        if ( node != null )
        {
            _hashCode = _hashCode * 23 + (node.Name?.GetHashCode() ?? 0);
            _hashCode = _hashCode * 23 + GetTypeHashCode( node.Type );
        }
        return base.VisitLabelTarget( node );
    }

    // Override methods for LabelExpression and GotoExpression to visit LabelTarget

    protected override Expression VisitGoto( GotoExpression node )
    {
        _hashCode = _hashCode * 23 + node.Kind.GetHashCode();
        VisitLabelTarget( node.Target );
        Visit( node.Value );
        return base.VisitGoto( node );
    }

    protected override Expression VisitLabel( LabelExpression node )
    {
        VisitLabelTarget( node.Target );
        Visit( node.DefaultValue );
        return base.VisitLabel( node );
    }

    // Optional: Handle LoopExpression

    protected override Expression VisitLoop( LoopExpression node )
    {
        Visit( node.Body );
        VisitLabelTarget( node.BreakLabel );
        VisitLabelTarget( node.ContinueLabel );
        return base.VisitLoop( node );
    }

    // Optional: Handle SwitchExpression

    protected override Expression VisitSwitch( SwitchExpression node )
    {
        Visit( node.SwitchValue );
        foreach ( var caseExpression in node.Cases )
        {
            foreach ( var testValue in caseExpression.TestValues )
            {
                Visit( testValue );
            }
            Visit( caseExpression.Body );
        }
        Visit( node.DefaultBody );
        _hashCode = _hashCode * 23 + GetMethodHashCode( node.Comparison );
        return base.VisitSwitch( node );
    }

    // Optional: Handle TryExpression

    protected override Expression VisitTry( TryExpression node )
    {
        Visit( node.Body );
        foreach ( var handler in node.Handlers )
        {
            Visit( handler.Filter );
            Visit( handler.Variable );
            Visit( handler.Body );
        }
        Visit( node.Fault );
        Visit( node.Finally );
        return base.VisitTry( node );
    }

    // Optional: Handle DynamicExpression

    protected override Expression VisitDynamic( DynamicExpression node )
    {
        _hashCode = _hashCode * 23 + GetTypeHashCode( node.DelegateType );
        _hashCode = _hashCode * 23 + GetTypeHashCode( node.Binder.GetType() );

        foreach ( var argument in node.Arguments )
        {
            Visit( argument );
        }

        return base.VisitDynamic( node );
    }

    // Optional: Handle RuntimeVariablesExpression

    protected override Expression VisitRuntimeVariables( RuntimeVariablesExpression node )
    {
        foreach ( var variable in node.Variables )
        {
            Visit( variable );
        }
        return base.VisitRuntimeVariables( node );
    }

    // Optional: Handle DebugInfoExpression

    protected override Expression VisitDebugInfo( DebugInfoExpression node )
    {
        _hashCode = _hashCode * 23 + node.Document.GetHashCode();
        _hashCode = _hashCode * 23 + node.StartLine.GetHashCode();
        _hashCode = _hashCode * 23 + node.EndLine.GetHashCode();
        _hashCode = _hashCode * 23 + node.StartColumn.GetHashCode();
        _hashCode = _hashCode * 23 + node.EndColumn.GetHashCode();
        _hashCode = _hashCode * 23 + node.IsClear.GetHashCode();
        return base.VisitDebugInfo( node );
    }

    // Custom hash code methods for Type, MethodInfo, and MemberInfo

    private int GetTypeHashCode( Type type )
    {
        if ( type == null )
            return 0;

        unchecked
        {
            int hash = 17;

            // Include the assembly-qualified name for uniqueness
            hash = hash * 23 + type.AssemblyQualifiedName!.GetHashCode();

            if ( type.IsGenericType )
            {
                foreach ( var arg in type.GetGenericArguments() )
                {
                    hash = hash * 23 + GetTypeHashCode( arg );
                }
            }

            return hash;
        }
    }

    private int GetMethodHashCode( MethodBase method )
    {
        if ( method == null )
            return 0;

        unchecked
        {
            int hash = 17;

            hash = hash * 23 + method.Name.GetHashCode();
            hash = hash * 23 + GetTypeHashCode( method.DeclaringType );

            if ( method.IsGenericMethod )
            {
                foreach ( var arg in method.GetGenericArguments() )
                {
                    hash = hash * 23 + GetTypeHashCode( arg );
                }
            }

            foreach ( var parameter in method.GetParameters() )
            {
                hash = hash * 23 + GetTypeHashCode( parameter.ParameterType );
            }

            return hash;
        }
    }

    private int GetMemberHashCode( MemberInfo member )
    {
        if ( member == null )
            return 0;

        unchecked
        {
            int hash = 17;

            hash = hash * 23 + member.MemberType.GetHashCode();
            hash = hash * 23 + member.Name.GetHashCode();
            hash = hash * 23 + GetTypeHashCode( member.DeclaringType );

            return hash;
        }
    }
}

public static class HashCodeGenerator
{
    private static readonly ConcurrentDictionary<Type, Func<object, int>> CachedHashGenerators = new();

    public static int GenerateHashCode(object obj)
    {
        if (obj == null)
            return 0;

        var type = obj.GetType();

        // Retrieve or create a hash generator for the type.
        var hashGenerator = CachedHashGenerators.GetOrAdd(type, CreateHashGenerator);
        return hashGenerator(obj);
    }

    private static Func<object, int> CreateHashGenerator(Type type)
    {
        var getHashCodeMethod = GetGetHashCodeMethod(type);

        if (getHashCodeMethod != null && getHashCodeMethod.DeclaringType != typeof(object))
        {
            // If GetHashCode is implemented, use it directly.
            return obj => obj.GetHashCode();
        }

        return obj => GenerateReflectionBasedHashCode(obj, type, [] );
    }

    private static MethodInfo GetGetHashCodeMethod(Type type)
    {
        return type.GetMethods(BindingFlags.Instance | BindingFlags.Public)
                   .FirstOrDefault(m => m.Name == "GetHashCode" && m.GetParameters().Length == 0 && m.DeclaringType == type);
    }

    private static int GenerateReflectionBasedHashCode(object obj, Type type, HashSet<object> visitedObjects)
    {
        if (obj == null)
            return 0;

        // Detect cycles by checking if we've already visited this object.
        if (!visitedObjects.Add(obj))
            return 0; // Return 0 for already-visited objects to avoid cycles.

        int hash = 17;

        try
        {
            // Iterate over fields
            foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                var fieldValue = field.GetValue(obj);
                int fieldHash = GenerateFieldHashCode(fieldValue, visitedObjects);
                hash = CombineHashCodes(hash, fieldHash);
            }
        }
        finally
        {
            // Remove the object from the visited set to allow other paths to process it.
            visitedObjects.Remove(obj);
        }

        return hash;
    }

    private static int GenerateFieldHashCode(object value, HashSet<object> visitedObjects)
    {
        if (value == null)
            return 0;

        var valueType = value.GetType();

        // Check if the type of the value has an overridden GetHashCode method.
        var getHashCodeMethod = GetGetHashCodeMethod(valueType);

        if (getHashCodeMethod != null && getHashCodeMethod.DeclaringType != typeof(object))
        {
            // If GetHashCode is implemented, use it directly.
            return value.GetHashCode();
        }

        // If the value is a struct or complex type, calculate its hash recursively.
        if (!valueType.IsPrimitive && !valueType.IsEnum && valueType != typeof(string))
        {
            return GenerateReflectionBasedHashCode(value, valueType, visitedObjects);
        }

        // For primitive types, use the default hash code.
        return value.GetHashCode();
    }

    private static int CombineHashCodes(int hash, int fieldHash)
    {
        unchecked
        {
            return hash * 31 + fieldHash;
        }
    }
}
