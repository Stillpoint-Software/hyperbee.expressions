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

**The first step** in this process transforms flow control constructs (such as if, switch, loops, and awaits) in the expression 
tree into a state tree that can be used to generate a flattened goto state machine. This step systematically traverses the expression 
tree and replaces branching constructs with state nodes that manage control flow using transitions and goto operations. This step 
also identifies variables that persist across state transitions.

## Implementation Overview

The `GotoTransformerVisitor` is responsible for traversing the expression tree and transforming its flow control constructs into  
state machine nodes that use goto operations. This is where flow control constructs like conditionals, switches, and loops are
turned into labeled states. The conversion to a state node structure enables the state machine to correctly represent the original 
control flow while allowing for asynchronous execution that must suspend and resume operations.

### Traversing the Expression Tree
The Expression visitor pattern is employed to traverse the expression tree and create the state node representation. Each expression
in the expression tree is visited and potentially transformed into one or more state nodes.

### The StateContext
`GotoTransformerVisitor` uses a `StateContext` to manage the collection of state nodes that are created durring the expression 
visit, and to track the transitions between them. The context keeps track of branching nodes, await continuations, and variables, 
and links states into a tree based goto flow that will be used to generate the final state machine.

### Handling Await Expressions
Await expressions introduce additional complexity because they suspend execution until the awaited task completes. Each `await` 
may complete immediately, or complete eventually. Eventual completions require the state machine to suspend until the awaited result
has been completed. The transformation process must handle these paths correctly by generating state nodes that represent the 
awaiting and resumption of execution.

### Handling Branching
Branching in the expression tree is one of the most important transformations. The state machine must correctly handle various types of
branching, ensuring that all possible branches are visited and correctly mapped to states.

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

The purpose of the branching tail node (represented by `_tailIndex` in the `GotoTransformerVisitor.StateContext` class)
is to keep track of the current state node at the end of the current branch path during the traversal of the expression
tree so we can correctly re-join branches to the main flow. This is crucial for managing state transitions in branching constructs 
(e.g. `Conditional`, `Switch`, `Try`, and `Await`).

#### Key Roles of the Tail Node

1. **Tracking the Current State**:
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
     end state correctly points to the join state or the next relevant state.

4. **Ensuring Correct Execution Flow**:
   - By keeping track of the tail node, the traversal ensures that the execution flow of the transformed expression tree
     correctly mirrors the original structure, with appropriate transitions between states.

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
   - `_tailIndex` is updated to point to the new join state.

3. **Visiting Branches**:
   - For the `IfTrue` and `IfFalse` branches, `VisitBranch` is called.
   - `VisitBranch` creates new branch states and updates `_tailIndex` to point to these new states.
   - The branch expressions are visited, and any nested branches will further update `_tailIndex`.

4. **Exiting the Conditional Expression**:
   - After visiting all branches, `ExitBranchState` is called.
   - This method pops the last join index from the `_joinIndexes` stack and sets `_tailIndex` to this value.
   - The transition for the source state is set, and the traversal continues.

### Summary

- The tail node (`_tailIndex`)  represents the index of the last state node within the current branch being visited.
- It ensures that state transitions are correctly managed, in branching constructs.
- It is updated when new branching states are added, and when entering or exiting branching constructs.
- The `_tailIndex` tracks the current branch segment from the last branching state node, not from the root of the state tree,
  ensuring correct state transitions within nested branches. 
