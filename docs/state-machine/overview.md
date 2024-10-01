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

The C# compiler generates state machines automatically. However, when working with runtime expression trees, the compiler does 
not provide this functionality. As a result, developers must manually generate state machines.

State machine generation involves transforming user expression trees into state machine representations and executing them.
This process involves several steps, including tree traversal, state creation, and managing state transitions. The transformation
process is essential for handling complex branching scenarios like conditional expressions and asynchronous operations.

State machine creation occurs in two passes:

### Pass 1: Expression Tree Transformation
The first pass transforms flow control constructs (such as if, switch, loops, and awaits) in the expression tree into a 
state tree that can be used to generate a flattened goto state machine. This step systematically traverses the expression tree
and replaces branching constructs with state nodes that manage control flow using transitions and goto operations. This step also 
identifies variables that persist across state transitions.

### Pass 2: State Machine Builder
The second pass builds the state machine based on the transformed structure. This involves creating a state-machine type,
hoisting variables, and wiring the execution flow according to the control constructs defined during the expression tree traversal.

