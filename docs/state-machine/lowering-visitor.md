---
layout: default
title: Lowering Visitor
parent: State Machines
nav_order: 2
---
# Lowering Visitor: Transforming Expressions into States

State machine generation involves converting user expression trees into state machine representations and executing them.
This process involves several steps, including tree traversal, state creation, and managing state transitions. The transformation
process is essential for handling complex branching scenarios like conditional expressions and asynchronous operations.

We employ a Lowering Technique to transform flow control constructs (such as if, switch, loops, and awaits) into a state tree that
can be used to generate a flattened goto state machine. This step systematically traverses the expression tree and replaces branching 
constructs with simplified state nodes that manage control flow using transitions and goto operations. This step also identifies 
variables that persist across state transitions and which need to be hoisted ny the builder.


## Introduction

The `LoweringVisitor` is responsible for traversing the expression tree and transforming its flow control constructs into a lowered 
representation of discrete states for a state machine. Lowering turns complex flow control constructs like conditionals, switches, and 
loops into a simplified state tree that will be used to generate the final state machine. This is essential in scenarios involving 
asynchronous operations and branching logic, where execution may need to be paused and resumed.

The purpose of this phase is to "lower" high-level constructs, such as `await`, `if/else`, `switch`, and loops, into individual 
`NodeExpression` objects that the state machine can later process.

## 1. Handling Control Flow Constructs (Branching and Loops)

### What is being done?
The `LoweringVisitor` handles control flow constructs like conditionals (`if/else`), loops (`for`, `while`), and switches. 
Each construct is transformed into a state, and transitions are defined to manage the execution flow between branches and loops.

### Problem
Branching and looping introduce multiple execution paths that require the program to pause execution at certain points and resume it 
later. These paths must be preserved accurately within a state machine to ensure correct execution flow.

### Discussion
The `LoweringVisitor` uses the `VisitBranch` method to create distinct states for each branch and loop. For each conditional or loop, a 
new state is created, and transitions are added to connect these states. The visitor maintains a `JoinState` that serves as the 
reconnection point after the conditional or loop is executed. Additionally, the `SourceState` represents the state before the branch or 
loop is executed.

Every branching construct must eventually rejoin the main flow of execution. The join state represents the point where diverging branches
reunite, ensuring that the state machine continues to execute correctly.

If you think about each unique branch segment (e.g. the 'if' or 'else' path in a conditional expression) as a single linked list of states, 
the `BranchTailState`, is the last node in the conditional or loop path. This tail node must be re-joined to the main execution path; the place where the 'if' and 'else' branches again begin to execute the
same code again. This re-convergance is critical, as branching structures are often nested, and all of the potential paths in a nesting
structure must be correctly re-joined.

Each branch (such as `if/else` or the body of a loop) becomes a distinct state, and the transitions ensure that the state machine can 
resume from the correct point when execution continues.

### Solution
By creating separate states for each branch and loop, and by using `JoinState` and `SourceState`, the state machine can accurately manage
control flow across complex branching and looping structures. This ensures that execution can pause and resume from the correct points, 
preserving the integrity of the program's logic.

## 2. Handling Await

### What is being done?
The `await` expression is used to pause the execution of a method until the awaited task completes. The `LoweringVisitor` splits the 
execution into two states: one before the `await`, and one after the task has completed.

### Problem
The `await` expression introduces natural pauses in execution. Managing this in a state machine requires the program to be split into 
two distinct states: one before the `await`, and one after, to resume execution from where it left off.

### Discussion
The `VisitAwait` method handles asynchronous operations by creating two separate states: a state that handles the suspension of execution 
(before the `await`), and another state that handles resumption after the awaited task completes. This ensures that the state machine can
pause execution, wait for the task to complete, and then resume execution in the appropriate state.

### Solution
By breaking the execution into two states, the state machine can handle the suspension and resumption of execution around `await` 
expressions. This allows asynchronous operations to be integrated into the state machine seamlessly.

## 3. Variable Hoisting

### What is being done?
In scenarios such as loops or asynchronous operations, variables need to persist across states. To achieve this, the `LoweringVisitor` 
hoists variables to a higher scope, ensuring they are accessible after state transitions.

### Problem
Variables declared within a state need to persist across multiple states. For example, a variable declared before an `await` must still 
be available after the `await` completes, even though the program has moved to a new state.

### Discussion
The `LoweringVisitor` identifies variables that need to persist across states and hoists them to a higher scope. This ensures that 
variables are not lost when the state machine transitions from one state to another. By hoisting these variables, the state machine can 
continue using them after pausing and resuming execution.

### Solution
Hoisting variables to a higher scope ensures that they are accessible across multiple states in the state machine. This is critical for 
maintaining the integrity of the program's execution, especially in the context of loops and asynchronous operations.

## 4. Managing Intermediate Expression Values

### What is being done?
The `LoweringVisitor` manages intermediate values produced by expressions and ensures that these values persist across state transitions. 
This is accomplished by separating the `ResultValue` (the expression producing the value) from the `ResultVariable` 
(where the result is stored).

### Problem
Intermediate values need to persist across state transitions to ensure that the program’s logic flows correctly. Without proper management
of these values, the state machine could lose track of intermediate results, leading to incorrect behavior.

### Discussion
The `ResultValue` represents the value produced by an expression, while the `ResultVariable` is the storage for that value. This 
separation allows the state machine to manage intermediate results effectively. The `ResultValue` is evaluated before transitioning to a 
new state, and the result is stored in the `ResultVariable` for future use.

### Solution
By separating the `ResultValue` and `ResultVariable`, the state machine can manage intermediate values across state transitions. This 
ensures that results are not lost when the machine moves from one state to another.

## Conclusion
The `LoweringVisitor` is a critical component in transforming complex control flow expressions into manageable states for a state machine. 
By handling control flow constructs, asynchronous operations, and intermediate values, the `LoweringVisitor` ensures that execution can 
pause and resume as needed, without losing the integrity of the program’s logic.
