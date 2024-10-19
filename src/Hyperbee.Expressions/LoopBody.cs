using System.Linq.Expressions;

namespace Hyperbee.Expressions;

public delegate Expression LoopBody( LabelTarget breakLabel, LabelTarget continueLabel );
