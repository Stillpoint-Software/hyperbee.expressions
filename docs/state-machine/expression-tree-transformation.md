---
layout: default
title: Expression Tree Transformation
parent: State Machines
nav_order: 2
---
# Expression Tree Transformation

State machine generation involves converting user expression trees into state machine representations and executing them.
This process involves several steps, including tree traversal, state creation, and managing state transitions. The transformation
process is essential for handling complex branching scenarios like conditional expressions and asynchronous operations.

**The first step** in this process uses a Lowering Technique to transform flow control constructs (such as if, switch, loops, and 
awaits) into a state tree that can be used to generate a flattened goto state machine. This step systematically traverses the 
expression tree and replaces branching constructs with simplified state nodes that manage control flow using transitions and goto 
operations. This step also identifies variables that persist across state transitions and which need to be hoisted ny the builder.

- Key Concepts:
    - **Lowering:** Traverse the Expression tree and create a Lowered representation that can be used to manage asynchronous execution.
    - **Hoist Variables:** Identify variables that persist across state transitions.
    - **State Tree Result:** Returns a Lowered state tree that is used to generate the final state-machine.


## Implementation Overview

The `LoweringVisitor` is responsible for traversing the expression tree and transforming its flow control constructs into a lowered 
representation of states that use goto operations. This is where complex flow control constructs like conditionals, switches, and loops 
are converted into a simplified state tree (using gotos) that will be used to generate the final state machine. The conversion to a 
state tree allows the state machine to correctly represent the original control flow while supporting asynchronous execution that must
suspend and resume operations from arbitrary points in the original expression tree.

### Traversing the Expression Tree
The Expression visitor pattern is employed to traverse the expression tree and create the state tree representation. Each expression
in the expression tree is visited and potentially transformed into one or more state nodes.

### The StateContext
`LoweringVisitor` uses a `StateContext` to manage the collection of state nodes that are created durring the expression visit, and to 
track the transitions between them. The context keeps track of branching nodes, loops, await continuations, and variable scope, and 
links these states with goto based transitions that will be used to generate the final state machine.

### Handling Await Expressions
Await expressions introduce complexity because they suspend execution until the awaited task completes. Each `await` may complete 
immediately, or it may complete eventually. Eventual completions require the state machine to suspend until the awaited result is 
available. The transformation process must handle these execution paths correctly by generating state nodes and flow, that represent 
the awaiting and resumption paths of execution.

### Handling Branching
Branching in the expression tree is one of the most important transformations. The state machine must correctly handle various types of
branching, to ensure that all possible execution paths are visited and correctly mapped to states. The visitor lowers the implementation 
by unnesting higher level flow control constructs into a flattened representation.

- **Conditional, Switch, Try, and Await:** Branches in constructs such as conditionals (if), switches, and try/catch blocks must be visited, 
  and the states created for these constructs must be correctly linked.
- **GotoExpression, LabelExpression, and BlockExpression:** These expressions represent direct flow control within the expression tree and 
  must be transformed into labeled states and goto transitions.
- **Nested Branches:** It is common to encounter nested branching constructs, which must be recursively handled to ensure the correct flow of 
  control.
- **Hoisting Variables:** Any variables declared within branches that must persist outside of the branch are hoisted to the appropriate scope 
  in the state machine.
- **Join States:** Eventually, diverging branches must be re-joined. Joining, especially in the context of nesting, must be carefully handled.

### Join States and the Branch Tail Node

Every branching construct must eventually rejoin the main flow of execution. The join state represents the point where diverging branches
reunite, ensuring that the state machine continues to execute correctly.

If you think about each unique branch segment (e.g. the 'if' or 'else' path in a conditional expression) as a single linked list of states, 
the branch tail node (represented internally by `_tailIndex` in the `GotoTransformerVisitor.StateContext`), is the last node in the conditional 
path. This tail node must be re-joined to the main execution path; the place where the 'if' and 'else' branches again begin to execute the
same code again. This re-convergance is non-trivial, as branching structures are often nested, and all of the potential paths in a nesting
structure must be correctly re-joined.

#### Key Roles of the Tail Node

1. **Tracking the final state in a Branch**:
   - The tail node represents the last state node in the current branch path. It is updated as the traversal
     progresses through different branches of the expression tree.
   - It is important to note that `_tailIndex` tracks the current branch segment from the last branching
     state node, not from the root of the state tree. This ensures that state transitions are managed correctly
     within nested branching constructs.

2. **Managing Transitions**:
   - When a branch is visited, a new state node is created, and `_tailIndex` is updated to point to this new state.
   - After visiting a branch, the tail node's transition is set to point to the join state or the next state in the sequence.

3. **Handling Nested Branches**:
   - In nested branching constructs, the tail node helps maintain correct state transitions by ensuring that each branch's 
     end state correctly points its associated join state.

4. **Ensuring Correct Execution Flow**:
   - By keeping track of the tail node, the traversal ensures that the execution flow of the transformed expression tree
     correctly mirrors the original structure, with appropriate transitions, and re-joins, between states.

#### Example: `VisitConditional` Method

Let's rereview the `VisitConditional` method to see how branching is managed:

```csharp
protected override Expression VisitConditional(ConditionalExpression node) 
{ 
    var updatedTest = VisitInternal(node.Test);
    var joinIndex = _states.EnterBranchState(out var sourceIndex, out var nodes);

    var conditionalTransition = new ConditionalTransition
    {
        IfTrue = VisitBranch(node.IfTrue, joinIndex),
        IfFalse = (node.IfFalse is not DefaultExpression)
            ? VisitBranch(node.IfFalse, joinIndex)
            : nodes[joinIndex]
    };

    var gotoConditional = Expression.IfThenElse(
        updatedTest,
        Expression.Goto(conditionalTransition.IfTrue.Label),
        Expression.Goto(conditionalTransition.IfFalse.Label));

    nodes[sourceIndex].Expressions.Add(gotoConditional);

    _states.ExitBranchState(sourceIndex, conditionalTransition);

    return node;
}
```

#### Step-by-Step Breakdown

1. **Before Entering the Conditional Expression**:
   - `_tailIndex` points to the current traversal state node. This state node is the source state for the conditional branch.

2. **Entering the Conditional Expression**:
   - `EnterBranchState` is called, which creates a new join state.
   - The current `_tailIndex` (source state) is saved as `sourceIndex`.
   - `_tailIndex` is then updated to point to the new join state.

3. **Visiting Branches**:
   - For the `IfTrue` and `IfFalse` branches, `VisitBranch` is called.
   - `VisitBranch` creates new branch states and updates `_tailIndex` to point to these new states.
   - The branch expressions are individually visited, and any nested branches will further update `_tailIndex`.

4. **Exiting the Conditional Expression**:
   - After visiting all branches, `ExitBranchState` is called.
   - This method pops the last join index from the `_joinIndexes` stack and sets `_tailIndex` to this value.
   - The transition for the source state is set, and the traversal continues.

#### Key Points

- The tail node (`_tailIndex`)  represents the index of the last state node within the current branch being visited.
- It ensures that state transitions are correctly managed, in branching constructs.
- It is updated when new branching states are added, and when entering or exiting branching constructs.
- The `_tailIndex` tracks the current branch segment from the last branching state node, not from the root of the state tree,
  ensuring correct state transitions within nested branches. 

### Summary

The result of the transformation is a lowered state tree, and a set of variables that require hoisting, that will be used by
the `StateMachineBuilder` to generate the final state machine expression.