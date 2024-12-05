---
layout: default
title: Overview
parent: State Machines
nav_order: 1
---
## Introduction

In c#, the `async` and `await` keywords are used to simplify asynchronous programming. Under the hood, the compiler transforms 
asynchronous methods into state machines. This transformation allows the method to pause execution at `await` points and resume 
later without blocking the main thread. The state machine keeps track of the current state of the method, enabling it to handle 
asynchronous operations seamlessly.

The c# compiler generates state machines automatically. However, when working with runtime expression trees, the compiler does 
not provide this functionality. 

State machine generation involves several steps, including tree traversal, state creation, and managing state transitions. The 
transformation process handles complex branching scenarios like conditional expressions, and loops, as well as asynchronous 
operations that must suspend and resume execution.

State machine creation occurs in two passes:

### Pass 1: Expression Tree Transformation
The first pass uses a Lowering Technique to transform `BlockAsyncExpression`s into state trees, and handles the lowering of
complex flow control constructs (ifs, switches, loops, try/catch, and awaits) into more primitive representations. This step 
also identifies variables that persist across states and require hoisting.

### Pass 2: State Machine Builder
The second pass builds the state machine based on the transformed structure. This involves creating a state-machine type,
hoisting variables, and wiring the execution flow according to the control constructs defined during the expression tree traversal.

