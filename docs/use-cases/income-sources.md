# Use Case: Income Sources

**Manager:** `IncomeManager`

Income sources track a user's recurring income, optionally linked to a household membership for coverage analysis.

---

## Create Income Source

**Entry point:** `POST /income`

```mermaid
sequenceDiagram
    participant C as Client
    participant Ctrl as IncomeController
    participant Mgr as IncomeManager
    participant I as IncomeSource
    participant IR as IIncomeSourceRepository

    C->>Ctrl: POST /income {userId, amount, source, frequency, householdId?, membershipId?}
    Ctrl->>Mgr: CreateAsync(request)
    Mgr->>I: IncomeSource.Create(userId, money, source, recurrence, householdId?, membershipId?)
    I-->>Mgr: incomeSource (+IncomeSourceCreated event)
    Mgr->>IR: AddAsync(incomeSource)
    Mgr-->>Ctrl: IncomeResponse
    Ctrl-->>C: 201 Created
```

---

## Update Income Source

**Entry point:** `PUT /income/{id}`

```mermaid
sequenceDiagram
    participant C as Client
    participant Ctrl as IncomeController
    participant Mgr as IncomeManager
    participant I as IncomeSource
    participant IR as IIncomeSourceRepository

    C->>Ctrl: PUT /income/{id} {amount, source, frequency}
    Ctrl->>Mgr: UpdateAsync(request)
    Mgr->>IR: GetByIdAsync(incomeId)
    IR-->>Mgr: incomeSource (or null → 404)
    Mgr->>I: incomeSource.Update(money, source, recurrence)
    I-->>Mgr: (+IncomeSourceUpdated event)
    Mgr->>IR: UpdateAsync(incomeSource)
    Mgr-->>Ctrl: IncomeResponse
    Ctrl-->>C: 200 OK
```

---

## Delete Income Source

**Entry point:** `DELETE /income/{id}`

Uses `TryDeactivate` — idempotent, no error if already inactive.

```mermaid
sequenceDiagram
    participant C as Client
    participant Ctrl as IncomeController
    participant Mgr as IncomeManager
    participant I as IncomeSource
    participant IR as IIncomeSourceRepository

    C->>Ctrl: DELETE /income/{id}
    Ctrl->>Mgr: DeleteAsync(request)
    Mgr->>IR: GetByIdAsync(incomeId)
    IR-->>Mgr: incomeSource (or null → 404)
    Mgr->>I: incomeSource.TryDeactivate()
    Note over I: Returns false (no-op) if already inactive
    alt was active
        I-->>Mgr: true (+IncomeSourceDeactivated event)
        Mgr->>IR: UpdateAsync(incomeSource)
    end
    Mgr-->>Ctrl: IncomeResponse
    Ctrl-->>C: 200 OK
```

## Guard failures

| Guard | Error |
|---|---|
| Source label empty | `ArgumentException` |
| Amount negative | `ArgumentException` |
| `Deactivate` on already inactive | `InvalidOperationException` |
| `TryDeactivate` on already inactive | No-op, returns `false` |
