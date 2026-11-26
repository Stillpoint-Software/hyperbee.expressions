#nullable enable

using System.Collections.ObjectModel;
using System.Linq.Expressions;

namespace Hyperbee.Expressions.CompilerServices;

/// <summary>
/// A closure boundary: an extension node compiled into its own frame, whose body becomes a
/// lambda during <c>Reduce()</c>. <c>AsyncBlockExpression</c> and <c>EnumerableBlockExpression</c>
/// in <c>Hyperbee.Expressions</c> are the two implementations.
/// </summary>
/// <remarks>
/// A compiler has to recognize these nodes for two reasons: their declared variables bound a
/// closure scope, and <see cref="CoroutineClosureRewriter"/> rebuilds them while hoisting
/// enclosing variables into cells.
///
/// The interface exists so neither job needs the concrete types. Without it a compiler would
/// have to reference <c>Hyperbee.Expressions</c> and everything that library depends on, purely
/// to name two classes in a <c>switch</c>. <see cref="Update"/> is part of the contract because
/// the node constructors are not all public, so rebuilding is the node's own job.
/// </remarks>
public interface ICoroutineBoundary
{
    /// <summary>The variables the boundary declares. These bound the closure scope.</summary>
    ReadOnlyCollection<ParameterExpression> Variables { get; }

    /// <summary>The body expressions of the boundary.</summary>
    ReadOnlyCollection<Expression> Expressions { get; }

    /// <summary>
    /// Returns a node of the same kind carrying the given variables and expressions, preserving
    /// everything else about this node. Returns this instance when both are unchanged, matching
    /// the convention of <see cref="Expression"/>'s own <c>Update</c> methods.
    /// </summary>
    Expression Update( ReadOnlyCollection<ParameterExpression> variables, ReadOnlyCollection<Expression> expressions );
}
