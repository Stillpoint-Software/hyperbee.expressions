---
layout: default
title: State Machine Builder
parent: State Machines
nav_order: 3
---
# State Machine Builder

State machine generation involves converting user expression trees into state machine representations and executing them.
This process involves several steps, including tree traversal, state creation, and managing state transitions. The transformation
process is essential for handling complex branching scenarios like conditional expressions and asynchronous operations.

**The second step** in the process builds the state machine based on a transformed expression tree structure. This involves 
creating a state-machine type, hoisting variables, and wiring the execution flow according to the control constructs defined during the 
expression tree lowering traversal.

- Key Concepts:
    - **State Machine Type:** Dynamically generate a state machine type to manages asynchronous execution.
    - **MoveNext Method:** Core execution function that controls state transitions, awaits task completion, and manages exceptions.
    - **Hoist Variables:** Variables that persist across state transitions are hoisted into fields in the state machine type.

The `StateMachineBuilder` creates and manages state machines. It constructs state machine types that manage control flow 
across asynchronous calls by creating appropriate state transitions and handling task results or exceptions.

## Implementation Overview
`StateMachineBuilder` creates a state machine by emitting Types, IL, and building expression trees that correspond to the transformed states. 
It ensures that the state machine can handle asynchronous tasks, manage state transitions, and resolve variables across different states. This 
transformation allows for asynchronous tasks to be suspended and resumed without blocking the main execution thread.

### Key Functions of the State Machine Builder

#### Building the State Machine Type

##### TypeBuilder
The TypeBuilder constructs the state machine type dynamically at runtime. This type includes several fields necessary for tracking state, 
hoisting variables, and managing asynchronous operations.

- **State Field:** The `__state<>`` field keeps track of the current state of the machine. It dictates which part of the state machine to execute next.

- **Builder Field:** The `__builder<>` field is an instance of `AsyncTaskMethodBuilder<TResult>`, which manages the lifecycle of the async 
  operation, including completing the task and handling exceptions.

- **Hoisted Variables:** Variables that need to persist across await points are lifted to fields in the state machine type. These fields 
  store values across state transitions.

- **Awaiter Fields:** Awaiters, such as `awaiter<0>` and `awaiter<1>`, are stored in fields so they can be accessed before and after the task 
  resumes. These fields are essential for managing the progress of asynchronous tasks.

- **Deferred Initialization:** The state machine type must be fully defined before it can be used in method calls. To handle this, the type
  is created first, and a lambda expression containing the `MoveNext` method is stored in the `SetMoveNext` method. The actual `MoveNext` method 
  is executed when the state machine is ready.

Here is a simplified version of a generated state machine type:

```csharp
public class StateMachineType : StateMachineBaseType 
{ 
    public AsyncTaskMethodBuilder<int> __builder<>; 
    public int __state<> == -1;
    
    // Example hoisted variables
    public int _variable1;  
    public string _variable2;

    // Example awaiter fields
    private TaskAwaiter<int> __awaiter<1>;
    private ConfiguredTaskAwaitable<int>.ConfiguredTaskAwaiter __awaiter<2>;

    private Action<StateMachineBaseType> __moveNextLambda<>;

    public void SetMoveNext(Action<StateMachineBaseType> moveNext)
    {
        __moveNextLambda<> = moveNext;
    }

    public override void MoveNext()
    {
        __moveNextLambda<>?.Invoke(this);
    }
}
```

#### Building the MoveNext Expression
The MoveNext method is the core execution function of the state machine. It controls the state transitions, handles task completions, and 
manages exceptions. Below is a visualization of a generated MoveNext method.

Here is an example of a generated MoveNext (expressed as c# code):
```csharp
var stateMachine = new RuntimeStateMachine(); 
    
stateMachine.SetMoveNext( (RuntimeStateMachineBase stateMachine) => 
{ 
    try 
    { 
        switch (stateMachine.__state<>) 
        { 
        case 0: 
            stateMachine.__state<> = -1; 
            goto ST_0002; 
            break;

        case 1:
            stateMachine.__state<> = -1;
            goto ST_0004;
            break;

        default:
            break;
        }

        ST_0000:
        stateMachine.x = 10;
        stateMachine.awaiter<0> = Task<VoidTaskResult>.GetAwaiter();

        if (!stateMachine.awaiter<0>.IsCompleted) {
            stateMachine.__state<> = 0;
            stateMachine.__builder<>.AwaitUnsafeOnCompleted(ref stateMachine.awaiter<0>, ref stateMachine);
            return;
        }

        goto ST_0002;

        ST_0001:
        stateMachine.x = stateMachine.x + 1;
        stateMachine.awaiter<1> = Task<VoidTaskResult>.GetAwaiter();

        if (!stateMachine.awaiter<1>.IsCompleted) {
            stateMachine.__state<> = 1;
            stateMachine.__builder<>.AwaitUnsafeOnCompleted(ref stateMachine.awaiter<1>, ref stateMachine);
            return;
        }

        goto ST_0004;

        ST_0002:
        stateMachine.awaiter<0>.GetResult();
        goto ST_0001;

        ST_0003:
        stateMachine.__finalResult<> = {
            AsyncBlockTests.AreEqual(11, stateMachine.x);
            stateMachine.x = stateMachine.x + 1;

            return AsyncBlockTests.AreEqual(12, stateMachine.x);
        };

        stateMachine.__state<> = -2;
        stateMachine.__builder<>.SetResult(stateMachine.__finalResult<>);
        goto ST_FINAL;

        ST_0004:
        stateMachine.awaiter<1>.GetResult();
        goto ST_0003;
    } 
    catch (Exception ex) 
    {
        stateMachine.__state<> = -2;
        stateMachine.__builder<>.SetException(ex);
        return;
    }

    ST_FINAL:
});

stateMachine.__builder<>.Start(ref stateMachine); 
return stateMachine.__builder<>.Task; 
```

#### Breakdown of MoveNext
Breakdown of MoveNext

- **State Tracking:** The `__state<>` field directs which block of code the method should jump to after an await completes. 
  Different case blocks correspond to different states in the state machine.

- **Awaiter Management:** Each `TaskAwaiter` (`awaiter<0>`, `awaiter<1>`) checks if the corresponding task has completed. If not, 
  the state is updated, and execution is suspended using `AwaitUnsafeOnCompleted`. When the task completes, execution resumes 
  at the corresponding state.

- **Goto Statements:** The goto labels (e.g., ST_0001, ST_0002) handle jumping between different execution points in the state
  machine, simulating the flow of code that would normally occur after await.

- **Exception Handling:** The try-catch block ensures that exceptions during the execution are caught, and the task is marked 
  as faulted by calling `SetException`.

- **Task Completion:** When the state machine reaches the final state (ST_0003), it calls SetResult to complete the task 
  successfully. If an exception occurs, SetException is called instead, signaling task failure.

#### Resolving Fields and Hoisting Variables
When variables need to persist across await points, they are "hoisted" into fields within the state machine. The `FieldResolverVisitor`
maps these variables to fields, ensuring that their values are maintained across state transitions.

- **Variable Hoisting:** Variables that cross state boundaries or await points are hoisted into fields in the generated state 
  machine type. This prevents their values from being lost when the state machine suspends execution.

- **Field Resolution:** The state machine dynamically resolves fields during execution, allowing it to retrieve and update 
  variable values as needed. This ensures that the state machine operates as expected, even when variables are involved in 
  multiple states or async operations.

## Summary
The `StateMachineBuilder` constructs a dynamic state machine that manages asynchronous execution. By transforming control flow constructs into 
state transitions and handling tasks using `AsyncTaskMethodBuilder<TResult>`, the builder creates an efficient runtime representation of the 
state machine. This process involves managing state transitions, hoisting variables across state boundaries, and handling exceptions to ensure
correct task completion.