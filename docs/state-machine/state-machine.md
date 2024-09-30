---
layout: default
title: State Machines
has_children: true
nav_order: 1
---
# State Machines

In C#, the `async` and `await` keywords are used to simplify asynchronous programming. Under the hood, the compiler transforms 
asynchronous methods into state machines. This transformation allows the method to pause execution at `await` points and resume 
later without blocking the main thread. The state machine keeps track of the current state of the method, enabling it to handle 
asynchronous operations seamlessly.

In normal, non-expression tree scenarios, the compiler generates the state machine automatically. However, when working with
expression trees, the compiler does not provide this functionality. As a result, developers must manually generate state machines.
