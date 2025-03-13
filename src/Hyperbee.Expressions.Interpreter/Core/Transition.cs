using System.Linq.Expressions;

namespace Hyperbee.Expressions.Interpreter.Core;

internal record Transition( Expression CommonAncestor, List<Expression> Children, LabelTarget TargetLabel );

internal record TransitionException( Exception Exception ) : Transition( null, null, null );
