---
layout: default
title: Overview
parent: State Machines
nav_order: 1
---
## Introduction

In C#, the `async` and `await` keywords are used to simplify asynchronous programming. Under the hood, the compiler transforms 
asynchronous methods into state machines. This transformation allows the method to pause execution at `await` points and resume 
later without blocking the main thread. The state machine keeps track of the current state of the method, enabling it to handle 
asynchronous operations seamlessly.

In normal, non-expression tree scenarios, the compiler generates the state machine automatically. However, when working with
expression trees, the compiler does not provide this functionality. As a result, developers must manually generate state machines.

State machine generation involves converting user expression trees into state machine representations and executing them.
This process involves several steps, including tree traversal, state creation, and managing state transitions. The transformation
process is essential for handling complex branching scenarios like conditional expressions and asynchronous operations.

State machine creation occurs in two passes:

### Pass 1: Expression Tree Transformation
The first pass converts flow control constructs (such as if, switch, and loops) in the expression tree into a flattened goto 
structure. This step systematically traverses the expression tree and replaces branching constructs with state nodes that manage 
control flow using transitions and goto operations.

- Key Concepts:
    - **Flow Control Constructs:** Handling of flow control structures such as conditional expressions ('if'), switches, and loops.
    - **Goto-Based State Machine:** Each state is represented by a label, and transitions between states are managed using goto operations.
 
### Pass 2: State Machine Builder
The second pass builds the state machine based on the transformed expression tree. This involves creating a state-machine type,
and wiring the execution flow according to the control constructs defined during the expression tree traversal.

- Key Concepts:
    - **State Machine Type:** Dynamically generating a state machine type that manages asynchronous execution.
    - **MoveNext Method:** Core execution function that controls state transitions, awaits task completion, and manages exceptions.
    - **Hoisted Variables:** Variables that persist across state transitions are hoisted into fields in the state machine type.

