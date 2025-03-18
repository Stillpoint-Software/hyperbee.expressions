using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using Hyperbee.Expressions.Interpreter.Core;
using Hyperbee.Expressions.Interpreter.Evaluators;

namespace Hyperbee.Expressions.Interpreter;

public sealed class XsInterpreter : ExpressionVisitor
{
    internal InterpretContext Context;

    internal Transition Transition
    {
        get => Context.Transition;
        set
        {
            Context.TransitionChildIndex = 0;
            Context.Transition = value;
        }
    }

    public XsInterpreter() : this( new InterpretContext() )
    {
    }

    internal XsInterpreter( InterpretContext context )
    {
        Context = context;
    }

    public TDelegate Interpret<TDelegate>( LambdaExpression expression )
        where TDelegate : Delegate
    {
        if ( Context.Reduced == null )
        {
            var analyzer = new AnalyzerVisitor();
            analyzer.Analyze( expression );

            Context.Reduced = (LambdaExpression) analyzer.Reduced;
            Context.Transitions = analyzer.Transitions;

            expression = Context.Reduced;
        }

        return InterpretDelegateFactory.CreateDelegate<TDelegate>( this, expression );
    }

    internal T Interpret<T>( LambdaExpression lambda, bool hasReturn, params object[] values )
    {
        var (scope, results) = Context;

        scope.Push();

        try
        {
            for ( var i = 0; i < lambda.Parameters.Count; i++ )
                scope[lambda.Parameters[i]] = values[i];

            Visit( lambda.Body );

            ThrowIfTransitioning( Context.Transition );

            if ( hasReturn )
            {
                var result = results.Pop();

                return result is Closure closure
                    ? (T) closure.Lambda
                    : (T) result;
            }
            else
                results.Pop();
        }
        finally
        {
            scope.Pop();
        }

        return default;

        static void ThrowIfTransitioning( Transition transition )
        {
            if ( transition == null )
                return;

            var exception = GetException( transition );
            if ( exception != null )
                throw new InvalidOperationException( "Interpreter failed because of an unhandled exception.", exception );

            throw new InvalidOperationException( "Interpreter failed to transition to next expression." );
        }
    }

    // Goto

    protected override Expression VisitGoto( GotoExpression node )
    {
        if ( !Context.Transitions.TryGetValue( node, out var transition ) )
            throw new InterpreterException( $"Undefined label target: {node.Target.Name}", node );

        if ( node is { Kind: GotoExpressionKind.Return, Value: not null } )
        {
            // Note: returns should not pop the result since it will be popped
            // by the result of visiting the label in the common ancestor
            Visit( node.Value );
        }

        Transition = transition;
        Context.Results.Push( Default( node.Target.Type ) );

        return node;
    }

    protected override Expression VisitLabel( LabelExpression node )
    {
        if ( Context.IsTransitioning && Transition.TargetLabel == node.Target )
        {
            Transition = null;

            if ( node.Target.Type != typeof( void ) )
                return node;
        }

        Context.Results.Push( Default( node.Target.Type ) );

        return node;
    }

    // Block

    private enum BlockState
    {
        InitializeVariables,
        HandleStatements,
        Complete
    };

    protected override Expression VisitBlock( BlockExpression node )
    {
        var state = BlockState.InitializeVariables;
        var statementIndex = 0;
        object lastResult = null;

        var (scope, results) = Context;

        scope.Push();

        try
        {
EntryPoint:

            if ( Context.IsTransitioning )
            {
                var nextChild = Context.GetNextChild();
                statementIndex = node.Expressions.IndexOf( nextChild );
            }

            while ( true )
            {
                switch ( state )
                {
                    case BlockState.InitializeVariables:
                        foreach ( var variable in node.Variables )
                        {
                            scope[variable] = Default( variable.Type );
                        }

                        state = BlockState.HandleStatements;
                        break;

                    case BlockState.HandleStatements:
                        if ( statementIndex >= node.Expressions.Count )
                        {
                            state = BlockState.Complete;
                            break;
                        }

                        Visit( node.Expressions[statementIndex] );

                        lastResult = results.Pop();

                        if ( Context.IsTransitioning )
                        {
                            if ( Transition.CommonAncestor == node )
                                goto EntryPoint;

                            results.Push( lastResult );
                            return node!;
                        }

                        statementIndex++;
                        break;

                    case BlockState.Complete:
                        results.Push( lastResult );
                        return node;
                }
            }
        }
        finally
        {
            scope.Pop();
        }
    }

    // Conditional

    private enum ConditionalState
    {
        Test,
        HandleTest,
        Visit,
        Complete
    };

    protected override Expression VisitConditional( ConditionalExpression node )
    {
        var state = ConditionalState.Test;
        var continuation = ConditionalState.Complete;
        Expression expr = null;
        object lastResult = null;

EntryPoint:

        if ( Context.IsTransitioning )
        {
            expr = Context.GetNextChild();
            state = ConditionalState.Visit;
            continuation = expr == node.Test ? ConditionalState.HandleTest : ConditionalState.Complete;
        }

        var (_, results) = Context;

        while ( true )
        {
            switch ( state )
            {
                case ConditionalState.Test:
                    expr = node.Test;
                    state = ConditionalState.Visit;
                    continuation = ConditionalState.HandleTest;
                    break;

                case ConditionalState.HandleTest:
                    var conditionValue = (bool) lastResult!;
                    expr = conditionValue ? node.IfTrue : node.IfFalse;
                    state = ConditionalState.Visit;
                    continuation = ConditionalState.Complete;
                    break;

                case ConditionalState.Visit:
                    Visit( expr );

                    lastResult = results.Pop();

                    if ( Context.IsTransitioning )
                    {
                        if ( Transition.CommonAncestor == node )
                            goto EntryPoint;

                        results.Push( lastResult );
                        return node;
                    }

                    state = continuation;
                    break;

                case ConditionalState.Complete:
                    results.Push( lastResult );
                    return node;
            }
        }
    }

    // Switch

    private enum SwitchState
    {
        SwitchValue,
        HandleSwitchValue,
        MatchCase,
        HandleMatchCase,
        Visit,
        Complete
    }

    protected override Expression VisitSwitch( SwitchExpression node )
    {
        var state = SwitchState.SwitchValue;
        var continuation = SwitchState.Complete;
        var caseIndex = 0;
        var testIndex = 0;
        object switchValue = null;
        Expression expr = null;
        object lastResult = null;

EntryPoint:

        if ( Context.IsTransitioning )
        {
            expr = Context.GetNextChild();

            if ( expr == node.SwitchValue )
            {
                state = SwitchState.Visit;
                continuation = SwitchState.HandleSwitchValue;
            }
            else
            {
                var matchedCase = node.Cases.FirstOrDefault( c => c.Body == expr );
                expr = matchedCase?.Body ?? node.DefaultBody;
                state = expr != null ? SwitchState.Visit : SwitchState.Complete;
                continuation = SwitchState.Complete;
            }
        }

        var (_, results) = Context;

        while ( true )
        {
            switch ( state )
            {
                case SwitchState.SwitchValue:
                    expr = node.SwitchValue;
                    state = SwitchState.Visit;
                    continuation = SwitchState.HandleSwitchValue;
                    break;

                case SwitchState.HandleSwitchValue:
                    switchValue = lastResult;
                    caseIndex = 0;
                    testIndex = 0;
                    state = SwitchState.MatchCase;
                    break;

                case SwitchState.MatchCase:
                    if ( caseIndex >= node.Cases.Count )
                    {
                        expr = node.DefaultBody;
                        state = expr != null ? SwitchState.Visit : SwitchState.Complete;
                        continuation = SwitchState.Complete;
                        break;
                    }

                    var testValues = node.Cases[caseIndex].TestValues;
                    if ( testIndex >= testValues.Count )
                    {
                        caseIndex++;
                        testIndex = 0;
                        state = SwitchState.MatchCase;
                        break;
                    }

                    expr = testValues[testIndex];
                    state = SwitchState.Visit;
                    continuation = SwitchState.HandleMatchCase;
                    break;

                case SwitchState.HandleMatchCase:

                    if ( (switchValue != null && !switchValue.Equals( lastResult )) || (switchValue == null && lastResult != null) )
                    {
                        testIndex++;
                        state = SwitchState.MatchCase;
                        break;
                    }

                    expr = node.Cases[caseIndex].Body;
                    state = SwitchState.Visit;
                    continuation = SwitchState.Complete;
                    break;

                case SwitchState.Visit:
                    Visit( expr! );

                    lastResult = results.Pop();

                    if ( Context.IsTransitioning )
                    {
                        if ( Transition.CommonAncestor == node )
                            goto EntryPoint;

                        results.Push( lastResult );
                        return node;
                    }

                    state = continuation;
                    break;

                case SwitchState.Complete:
                    results.Push( lastResult );
                    return node;
            }
        }
    }

    // Try/Catch

    private enum TryCatchState
    {
        Try,
        Catch,
        HandleCatch,
        Finally,
        HandleFinally,
        Visit,
        Complete
    }

    protected override Expression VisitTry( TryExpression node )
    {
        var state = TryCatchState.Try;
        var continuation = TryCatchState.Complete;
        var catchIndex = 0;

        Expression expr = null;
        ParameterExpression exceptionVariable = null;
        object lastResult = null;

EntryPoint:

        if ( Context.IsTransitioning )
        {
            expr = Context.GetNextChild();

            if ( expr == node.Body )
            {
                state = TryCatchState.Visit;
                continuation = TryCatchState.Finally;
            }
            else if ( expr == node.Finally )
            {
                state = TryCatchState.Visit;
                continuation = TryCatchState.Complete;
            }
            else
            {
                var exceptionHandler = node.Handlers.FirstOrDefault( c => c.Body == expr );

                if ( exceptionHandler != null )
                {
                    expr = exceptionHandler.Body;
                    state = TryCatchState.Visit;
                    continuation = TryCatchState.Finally;
                }
            }
        }

        var (scope, results) = Context;

        while ( true )
        {
            Exception exception;
            switch ( state )
            {
                case TryCatchState.Try:
                    expr = node.Body;
                    state = TryCatchState.Visit;
                    continuation = TryCatchState.Finally;
                    break;

                case TryCatchState.Catch:
                    if ( catchIndex >= node.Handlers.Count )
                    {
                        state = TryCatchState.Finally;
                        break;
                    }

                    var handler = node.Handlers[catchIndex];
                    var exceptionType = GetException( Transition )?.GetType();

                    if ( handler.Test.IsAssignableFrom( exceptionType ) )
                    {
                        exceptionVariable = handler.Variable;
                        expr = handler.Body;
                        state = TryCatchState.HandleCatch;
                        continuation = TryCatchState.Finally;
                    }
                    else
                    {
                        catchIndex++;
                        state = TryCatchState.Catch;
                    }
                    break;

                case TryCatchState.HandleCatch:

                    exception = GetException( Transition );
                    Transition = null;

                    try
                    {
                        scope.Push();
                        if ( exceptionVariable != null )
                            scope[exceptionVariable] = exception;

                        Visit( expr! );
                    }
                    finally
                    {
                        scope.Pop();
                    }

                    lastResult = results.Pop();

                    if ( Context.IsTransitioning )
                    {
                        if ( lastResult == null && exception != null )
                        {
                            // handle empty rethrow
                            Context.Transition = new TransitionException( exception );
                            lastResult = exception;
                        }

                        if ( Transition.CommonAncestor == node )
                            goto EntryPoint;
                    }

                    state = continuation;
                    break;

                case TryCatchState.Finally:
                    if ( node.Finally != null )
                    {
                        expr = node.Finally;
                        state = TryCatchState.HandleFinally;
                        continuation = TryCatchState.Complete;
                    }
                    else
                    {
                        state = TryCatchState.Complete;
                    }
                    break;

                case TryCatchState.HandleFinally:

                    exception = GetException( Transition );

                    if ( exception != null )
                        Transition = null;

                    Visit( expr! );

                    results.Pop(); // don't capture finally block result

                    if ( exception != null )
                        throw exception;

                    if ( Context.IsTransitioning )
                    {
                        if ( Transition.CommonAncestor == node )
                            goto EntryPoint;

                        results.Push( lastResult );
                        return node;
                    }

                    state = continuation;
                    break;

                case TryCatchState.Visit:
                    Visit( expr! );

                    lastResult = results.Pop();

                    if ( Context.IsTransitioning )
                    {
                        if ( Transition.CommonAncestor == node )
                            goto EntryPoint;

                        if ( GetException( Transition ) != null )
                        {
                            state = TryCatchState.Catch;
                            break;
                        }

                        results.Push( lastResult );
                        return node;
                    }

                    state = continuation;
                    break;

                case TryCatchState.Complete:
                    results.Push( lastResult );
                    return node;
            }
        }
    }

    // Loop

    protected override Expression VisitLoop( LoopExpression node )
    {
        var (scope, results) = Context;

        scope.Push();

        try
        {
            object lastResult;

            while ( true )
            {
                Visit( node.Body );
                lastResult = results.Pop();

                if ( !Context.IsTransitioning )
                {
                    continue;
                }

                if ( Transition.TargetLabel == node.BreakLabel || Transition.TargetLabel == node.ContinueLabel )
                {
                    Transition = null;
                    break;
                }

                results.Push( lastResult );
                return node;
            }

            results.Push( lastResult );
        }
        finally
        {
            scope.Pop();
        }

        return node;
    }

    // Lambda

    protected override Expression VisitLambda<T>( Expression<T> node )
    {
        var (scope, results) = Context;

        if ( scope.Count == 0 )
        {
            results.Push( this.Interpret( node, node.Type ) );
            return node;
        }

        var freeVariables = FreeVariableVisitor.GetFreeVariables( node );

        if ( freeVariables.Count == 0 )
        {
            results.Push( this.Interpret( node, node.Type ) );
            return node;
        }

        var capturedScope = new Dictionary<ParameterExpression, object>();

        foreach ( var variable in freeVariables )
        {
            if ( !scope.TryGetValue( variable, out var value ) )
                throw new InterpreterException( $"Captured variable '{variable.Name}' is not defined.", node );

            capturedScope[variable] = value;
        }

        var lambda = this.Interpret( node, node.Type );
        results.Push( new Closure( lambda, capturedScope ) );

        return node;
    }

    protected override Expression VisitInvocation( InvocationExpression node )
    {
        Visit( node.Expression );

        var (scope, results) = Context;

        var targetValue = results.Pop();

        Delegate lambdaDelegate;
        Dictionary<ParameterExpression, object> capturedScope = null;

        switch ( targetValue )
        {
            case Closure closure:
                lambdaDelegate = closure.Lambda as Delegate;
                capturedScope = closure.CapturedScope;
                break;

            case Delegate @delegate:
                lambdaDelegate = @delegate;
                break;

            case LambdaExpression lambda:
                lambdaDelegate = (Delegate) this.Interpret( lambda, lambda.Type );
                break;

            default:
                throw new InterpreterException( "Invocation target is not a valid lambda or closure.", node );
        }

        var arguments = new object[node.Arguments.Count];

        for ( var i = 0; i < node.Arguments.Count; i++ )
        {
            Visit( node.Arguments[i] );
            arguments[i] = results.Pop();
        }

        try
        {
            scope.Push();

            if ( capturedScope is not null )
            {
                foreach ( var (param, value) in capturedScope )
                    scope[param] = value;
            }

            var result = lambdaDelegate?.DynamicInvoke( arguments );

            results.Push( result );

            return node;
        }
        finally
        {
            scope.Pop();
        }
    }

    protected override Expression VisitMethodCall( MethodCallExpression node )
    {
        var isStatic = node.Method.IsStatic;
        object instance = null;

        var (scope, results) = Context;

        if ( !isStatic )
        {
            Visit( node.Object );
            instance = results.Pop();
        }

        var arguments = new object[node.Arguments.Count];
        var capturedValues = new Dictionary<int, Dictionary<ParameterExpression, object>>();
        var hasClosure = false;

        for ( var i = 0; i < node.Arguments.Count; i++ )
        {
            Visit( node.Arguments[i] );
            var argValue = results.Pop();

            switch ( argValue )
            {
                case Closure closure:
                    hasClosure = true;
                    arguments[i] = closure.Lambda;
                    capturedValues[i] = closure.CapturedScope;
                    break;

                case LambdaExpression lambda:
                    arguments[i] = this.Interpret( lambda, lambda.Type );
                    break;

                default:
                    arguments[i] = argValue;
                    break;
            }
        }

        try
        {
            if ( hasClosure )
            {
                scope.Push();

                foreach ( var capturedScope in capturedValues.Values )
                {
                    foreach ( var (param, value) in capturedScope )
                        scope[param] = value;
                }
            }

            var result = node.Method.Invoke( instance, arguments );
            results.Push( result );
            return node;
        }
        catch ( TargetInvocationException invocationException )
        {
            throw invocationException.InnerException ?? invocationException;
        }
        finally
        {
            if ( hasClosure )
            {
                scope.Pop();
            }
        }
    }

    protected override Expression VisitBinary( BinaryExpression node )
    {
        if ( node.NodeType is ExpressionType.Assign
            or ExpressionType.AddAssign
            or ExpressionType.SubtractAssign
            or ExpressionType.MultiplyAssign
            or ExpressionType.DivideAssign
            or ExpressionType.ModuloAssign
            or ExpressionType.LeftShiftAssign
            or ExpressionType.RightShiftAssign )
        {
            switch ( node.Left )
            {
                case MemberExpression memberExpr:
                    Visit( memberExpr.Expression ); // Visit and push instance

                    if ( Context.IsTransitioning )
                        return node;

                    break;

                case IndexExpression indexExpr:
                    Visit( indexExpr.Object ); // Visit and push instance
                    if ( Context.IsTransitioning )
                        return node;

                    foreach ( var arg in indexExpr.Arguments )
                    {
                        Visit( arg ); // Visit and push index arguments

                        if ( Context.IsTransitioning )
                            return node;
                    }

                    break;
            }
        }
        else
        {
            Visit( node.Left ); // Visit and push leftValue

            if ( Context.IsTransitioning )
                return node;
        }

        Visit( node.Right ); // Visit and push rightValue

        if ( Context.IsTransitioning )
            return node;

        var result = BinaryEvaluator.Binary( Context, node );

        Context.Results.Push( result );

        return node;
    }

    protected override Expression VisitTypeBinary( TypeBinaryExpression node )
    {
        Visit( node.Expression );

        var (_, results) = Context;

        var operand = results.Pop();

        var result = operand is not null && node.TypeOperand.IsAssignableFrom( operand.GetType() );

        results.Push( result );
        return node;
    }

    protected override Expression VisitUnary( UnaryExpression node )
    {
        Visit( node.Operand ); // Visit and push operand

        var result = UnaryEvaluator.Unary( Context, node );
        Context.Results.Push( result );

        return node;
    }

    protected override Expression VisitConstant( ConstantExpression node )
    {
        Context.Results.Push( node.Value );
        return node;
    }

    protected override Expression VisitDefault( DefaultExpression node )
    {
        var defaultValue = node.Type.IsValueType && node.Type != typeof( void )
            ? RuntimeHelpers.GetUninitializedObject( node.Type )
            : null;

        Context.Results.Push( defaultValue );
        return node;
    }

    protected override Expression VisitIndex( IndexExpression node )
    {
        var (_, results) = Context;

        var arguments = new object[node.Arguments.Count];
        for ( var i = 0; i < node.Arguments.Count; i++ )
        {
            Visit( node.Arguments[i] );
            arguments[i] = results.Pop();
        }

        Visit( node.Object );
        var instance = results.Pop();

        var result = node.Indexer!.GetValue( instance, arguments );
        results.Push( result );

        return node;
    }

    protected override Expression VisitListInit( ListInitExpression node )
    {
        Visit( node.NewExpression );

        var (_, results) = Context;

        var instance = results.Pop();

        foreach ( var initializer in node.Initializers )
        {
            var arguments = new object[initializer.Arguments.Count];

            for ( var index = 0; index < initializer.Arguments.Count; index++ )
            {
                Visit( initializer.Arguments[index] );
                arguments[index] = results.Pop();
            }

            initializer.AddMethod.Invoke( instance, arguments );
        }

        results.Push( instance );
        return node;
    }

    protected override Expression VisitMember( MemberExpression node )
    {
        Visit( node.Expression );

        var (_, results) = Context;

        var instance = results.Pop();

        object result;
        switch ( node.Member )
        {
            case PropertyInfo prop:
                result = prop.GetValue( instance );
                break;
            case FieldInfo field:
                if ( field.FieldType.IsValueType )
                {
                    var typeReference = __makeref(instance);
                    result = field.GetValueDirect( typeReference );
                    break;
                }
                result = field.GetValue( instance );
                break;
            default:
                throw new InterpreterException( $"Unsupported member access: {node.Member.Name}", node );
        }

        results.Push( result );
        return node;
    }

    protected override Expression VisitNew( NewExpression node )
    {
        var arguments = new object[node.Arguments.Count];
        var capturedValues = new Dictionary<int, Dictionary<ParameterExpression, object>>();
        var hasClosure = false;

        var (scope, results) = Context;

        for ( var index = 0; index < node.Arguments.Count; index++ )
        {
            Visit( node.Arguments[index] );

            var argValue = results.Pop();

            switch ( argValue )
            {
                case Closure closure:
                    hasClosure = true;
                    arguments[index] = closure.Lambda;
                    capturedValues[index] = closure.CapturedScope;
                    break;

                case LambdaExpression lambda:
                    arguments[index] = this.Interpret( lambda, lambda.Type );
                    break;

                default:
                    arguments[index] = argValue;
                    break;
            }
        }

        var constructor = node.Constructor;

        if ( constructor is null )
        {
            throw new InterpreterException( $"No valid constructor found for type {node.Type}.", node );
        }

        if ( !hasClosure )
        {
            var instance = constructor.Invoke( arguments );
            results.Push( instance );
            return node;
        }

        scope.Push();

        try
        {
            foreach ( var (_, capturedScope) in capturedValues )
            {
                foreach ( var (param, value) in capturedScope )
                    scope[param] = value;
            }

            var instance = constructor.Invoke( arguments );
            results.Push( instance );
            return node;
        }
        finally
        {
            scope.Pop();
        }
    }

    protected override Expression VisitNewArray( NewArrayExpression node )
    {
        var elementType = node.Type.GetElementType();
        var (_, results) = Context;

        switch ( node.NodeType )
        {
            case ExpressionType.NewArrayInit:
                {
                    // Handle NewArrayInit: Array initialized with values
                    var values = new object[node.Expressions.Count];

                    for ( var i = 0; i < node.Expressions.Count; i++ )
                    {
                        Visit( node.Expressions[i] );
                        values[i] = results.Pop();
                    }

                    var array = Array.CreateInstance( elementType!, values.Length );

                    for ( var i = 0; i < values.Length; i++ )
                        array.SetValue( values[i], i );

                    results.Push( array );
                    break;
                }
            case ExpressionType.NewArrayBounds:
                {
                    // Handle NewArrayBounds: Array created with specified dimensions
                    var lengths = new int[node.Expressions.Count];

                    for ( var i = 0; i < node.Expressions.Count; i++ )
                    {
                        Visit( node.Expressions[i] );
                        lengths[i] = (int) results.Pop();
                    }

                    var array = Array.CreateInstance( elementType!, lengths );
                    results.Push( array );
                    break;
                }
            default:
                throw new InterpreterException( $"Unsupported array creation type: {node.NodeType}", node );
        }

        return node;
    }

    protected override Expression VisitParameter( ParameterExpression node )
    {
        var (scope, results) = Context;

        if ( !scope.TryGetValue( node, out var value ) )
            throw new InterpreterException( $"Parameter '{node.Name}' not found.", node );

        results.Push( value );
        return node;
    }

    private static Exception GetException( Transition transition )
    {
        if ( transition is TransitionException { Exception: not null } transitionException )
            return transitionException.Exception;

        return null;
    }

    private static object Default( Type type )
    {
        if ( type == typeof( void ) )
            return null;

        if ( type == typeof( string ) )
            return string.Empty;

        return type.IsValueType
            ? RuntimeHelpers.GetUninitializedObject( type )
            : null;
    }

    public record Closure( object Lambda, Dictionary<ParameterExpression, object> CapturedScope );

    private sealed class FreeVariableVisitor : ExpressionVisitor
    {
        private readonly HashSet<ParameterExpression> _declaredVariables = [];
        private readonly HashSet<ParameterExpression> _freeVariables = [];

        public static HashSet<ParameterExpression> GetFreeVariables( Expression expression )
        {
            var visitor = new FreeVariableVisitor();
            visitor.Visit( expression );
            return visitor._freeVariables;
        }

        protected override Expression VisitBlock( BlockExpression node )
        {
            _declaredVariables.UnionWith( node.Variables );

            return base.VisitBlock( node );
        }

        protected override CatchBlock VisitCatchBlock( CatchBlock node )
        {
            _declaredVariables.Add( node.Variable );

            return base.VisitCatchBlock( node );
        }

        protected override Expression VisitLambda<T>( Expression<T> node )
        {
            _declaredVariables.UnionWith( node.Parameters );

            return base.VisitLambda( node );
        }

        protected override Expression VisitParameter( ParameterExpression node )
        {
            if ( !_declaredVariables.Contains( node ) )
            {
                _freeVariables.Add( node );
            }

            return base.VisitParameter( node );
        }
    }
}
