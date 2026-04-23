# Use Case: Bill Splits

**Manager:** `BillWorkflowManager`

A split represents one member's share of a bill. Each member can have at most one split per bill.

---

## Upsert Split

**Entry point:** `POST /bills/{id}/splits`  
Creates a new split, or updates an existing one if `splitId` is provided.

```mermaid
sequenceDiagram
    participant C as Client
    participant Ctrl as BillsController
    participant Mgr as BillWorkflowManager
    participant S as BillSplit
    participant SR as IBillSplitRepository

    C->>Ctrl: POST /bills/{id}/splits {membershipId, amount, splitId?}
    Ctrl->>Mgr: UpsertSplitAsync(request)

    alt splitId provided
        Mgr->>SR: GetByIdAsync(splitId)
        SR-->>Mgr: existing split
        Mgr->>S: split.Update(newAmount)
        S-->>Mgr: (+BillSplitUpdated event)
        Mgr->>SR: UpdateAsync(split)
    else no splitId
        Mgr->>SR: GetByBillAndMembershipAsync(billId, membershipId)
        SR-->>Mgr: duplicate? → throw
        Mgr->>S: BillSplit.Create(billId, householdId, membershipId, userId, amount)
        S-->>Mgr: split (+BillSplitCreated event)
        Mgr->>SR: AddAsync(split)
    end

    Mgr-->>Ctrl: SplitResponse
    Ctrl-->>C: 200 OK
```

---

## Remove Split

**Entry point:** `DELETE /bills/{id}/splits/{splitId}`

```mermaid
sequenceDiagram
    participant C as Client
    participant Ctrl as BillsController
    participant Mgr as BillWorkflowManager
    participant S as BillSplit
    participant SR as IBillSplitRepository

    C->>Ctrl: DELETE /bills/{id}/splits/{splitId}
    Ctrl->>Mgr: RemoveSplitAsync(request)
    Mgr->>SR: GetByIdAsync(splitId)
    SR-->>Mgr: split (or null → 404)
    Mgr->>S: split.Remove()
    S-->>Mgr: (+BillSplitRemoved event)
    Mgr->>SR: RemoveAsync(split)
    Mgr-->>Ctrl: SplitResponse
    Ctrl-->>C: 200 OK
```

## Guard failures

| Guard | Error |
|---|---|
| Duplicate split for same member+bill | `InvalidOperationException` |
| Amount negative | `ArgumentException` |
| Claiming already claimed split | `InvalidOperationException` |
