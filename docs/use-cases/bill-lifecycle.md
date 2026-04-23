# Use Case: Bill Lifecycle

**Manager:** `BillWorkflowManager`

---

## Create Bill

**Entry point:** `POST /bills`

```mermaid
sequenceDiagram
    participant C as Client
    participant Ctrl as BillsController
    participant Mgr as BillWorkflowManager
    participant B as Bill
    participant BR as IBillRepository

    C->>Ctrl: POST /bills {householdId, title, amount, category, dueDate, recurrence?}
    Ctrl->>Mgr: CreateAsync(request)
    Mgr->>B: Bill.Create(householdId, title, money, category, createdBy, dueDate, recurrence?)
    B-->>Mgr: bill (+BillCreated event)
    Mgr->>BR: AddAsync(bill)
    Mgr-->>Ctrl: BillResponse
    Ctrl-->>C: 201 Created
```

---

## Update Bill

**Entry point:** `PUT /bills/{id}`

```mermaid
sequenceDiagram
    participant C as Client
    participant Ctrl as BillsController
    participant Mgr as BillWorkflowManager
    participant B as Bill
    participant BR as IBillRepository

    C->>Ctrl: PUT /bills/{id} {title, amount, category, dueDate, recurrence?}
    Ctrl->>Mgr: UpdateAsync(request)
    Mgr->>BR: GetByIdAsync(billId)
    BR-->>Mgr: bill (or null → 404)
    Mgr->>B: bill.Update(title, money, category, dueDate, recurrence?)
    B-->>Mgr: (+BillUpdated event)
    Mgr->>BR: UpdateAsync(bill)
    Mgr-->>Ctrl: BillResponse
    Ctrl-->>C: 200 OK
```

---

## Deactivate Bill

**Entry point:** `DELETE /bills/{id}`

```mermaid
sequenceDiagram
    participant C as Client
    participant Ctrl as BillsController
    participant Mgr as BillWorkflowManager
    participant B as Bill
    participant BR as IBillRepository

    C->>Ctrl: DELETE /bills/{id}
    Ctrl->>Mgr: DeactivateAsync(request)
    Mgr->>BR: GetByIdAsync(billId)
    BR-->>Mgr: bill (or null → 404)
    Mgr->>B: bill.Deactivate()
    B-->>Mgr: (+BillDeactivated event)
    Mgr->>BR: UpdateAsync(bill)
    Mgr-->>Ctrl: BillResponse
    Ctrl-->>C: 200 OK
```

## Guard failures

| Guard | Error |
|---|---|
| Title empty | `ArgumentException` |
| Amount negative | `ArgumentException` |
| Due date in the past (create only) | `ArgumentException` |
| Bill already inactive | `InvalidOperationException` |
