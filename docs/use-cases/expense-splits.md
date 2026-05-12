# Use Case: Expense Splits

**Manager:** `ExpenseWorkflowManager`

A split represents one member's share of an expense. Each member can have at most one split per expense.

---

## Upsert Split

**Entry point:** `POST /expenses/{id}/splits`  
Creates a new split, or updates an existing one if `splitId` is provided.

```mermaid
sequenceDiagram
    participant C as Client
    participant Ctrl as ExpensesController
    participant Mgr as ExpenseWorkflowManager
    participant S as ExpenseSplit
    participant SR as IExpenseSplitRepository

    C->>Ctrl: POST /expenses/{id}/splits {membershipId, amount, splitId?}
    Ctrl->>Mgr: UpsertSplitAsync(request)

    alt splitId provided
        Mgr->>SR: GetByIdAsync(splitId)
        SR-->>Mgr: existing split
        Mgr->>S: split.Update(newAmount)
        S-->>Mgr: (+ExpenseSplitUpdated event)
        Mgr->>SR: UpdateAsync(split)
    else no splitId
        Mgr->>SR: GetByExpenseAndMembershipAsync(expenseId, membershipId)
        SR-->>Mgr: duplicate? → throw
        Mgr->>S: ExpenseSplit.Create(expenseId, householdId, membershipId, userId, amount)
        S-->>Mgr: split (+ExpenseSplitCreated event)
        Mgr->>SR: AddAsync(split)
    end

    Mgr-->>Ctrl: SplitResponse
    Ctrl-->>C: 200 OK
```

---

## Remove Split

**Entry point:** `DELETE /expenses/{id}/splits/{splitId}`

```mermaid
sequenceDiagram
    participant C as Client
    participant Ctrl as ExpensesController
    participant Mgr as ExpenseWorkflowManager
    participant S as ExpenseSplit
    participant SR as IExpenseSplitRepository

    C->>Ctrl: DELETE /expenses/{id}/splits/{splitId}
    Ctrl->>Mgr: RemoveSplitAsync(request)
    Mgr->>SR: GetByIdAsync(splitId)
    SR-->>Mgr: split (or null → 404)
    Mgr->>S: split.Remove()
    S-->>Mgr: (+ExpenseSplitRemoved event)
    Mgr->>SR: RemoveAsync(split)
    Mgr-->>Ctrl: SplitResponse
    Ctrl-->>C: 200 OK
```

## Guard failures

| Guard | Error |
|---|---|
| Duplicate split for same member+expense | `InvalidOperationException` |
| Amount negative | `ArgumentException` |
| Claiming already claimed split | `InvalidOperationException` |
