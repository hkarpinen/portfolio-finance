# Use Case: Expense Lifecycle

**Manager:** `ExpenseWorkflowManager`

---

## Create Expense

**Entry point:** `POST /expenses`

```mermaid
sequenceDiagram
    participant C as Client
    participant Ctrl as ExpensesController
    participant Mgr as ExpenseWorkflowManager
    participant E as Expense
    participant ER as IExpenseRepository

    C->>Ctrl: POST /expenses {householdId, title, amount, category, dueDate, recurrence?}
    Ctrl->>Mgr: CreateAsync(request)
    Mgr->>E: Expense.Create(householdId, title, money, category, createdBy, dueDate, recurrence?)
    E-->>Mgr: expense (+ExpenseCreated event)
    Mgr->>ER: AddAsync(expense)
    Mgr-->>Ctrl: ExpenseResponse
    Ctrl-->>C: 201 Created
```

---

## Update Expense

**Entry point:** `PUT /expenses/{id}`

```mermaid
sequenceDiagram
    participant C as Client
    participant Ctrl as ExpensesController
    participant Mgr as ExpenseWorkflowManager
    participant E as Expense
    participant ER as IExpenseRepository

    C->>Ctrl: PUT /expenses/{id} {title, amount, category, dueDate, recurrence?}
    Ctrl->>Mgr: UpdateAsync(request)
    Mgr->>ER: GetByIdAsync(expenseId)
    ER-->>Mgr: expense (or null → 404)
    Mgr->>E: expense.Update(title, money, category, dueDate, recurrence?)
    E-->>Mgr: (+ExpenseUpdated event)
    Mgr->>ER: UpdateAsync(expense)
    Mgr-->>Ctrl: ExpenseResponse
    Ctrl-->>C: 200 OK
```

---

## Deactivate Expense

**Entry point:** `DELETE /expenses/{id}`

```mermaid
sequenceDiagram
    participant C as Client
    participant Ctrl as ExpensesController
    participant Mgr as ExpenseWorkflowManager
    participant E as Expense
    participant ER as IExpenseRepository

    C->>Ctrl: DELETE /expenses/{id}
    Ctrl->>Mgr: DeactivateAsync(request)
    Mgr->>ER: GetByIdAsync(expenseId)
    ER-->>Mgr: expense (or null → 404)
    Mgr->>E: expense.Deactivate()
    E-->>Mgr: (+ExpenseDeactivated event)
    Mgr->>ER: UpdateAsync(expense)
    Mgr-->>Ctrl: ExpenseResponse
    Ctrl-->>C: 200 OK
```

## Guard failures

| Guard | Error |
|---|---|
| Title empty | `ArgumentException` |
| Amount negative | `ArgumentException` |
| Due date in the past (create only) | `ArgumentException` |
| Expense already inactive | `InvalidOperationException` |
