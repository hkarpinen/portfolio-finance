# Use Case: Manage Household

Covers update, ownership transfer, and deletion.

**Actor:** Household owner  
**Manager:** `HouseholdWorkflowManager`

---

## Update Household

**Entry point:** `PUT /households/{id}`

```mermaid
sequenceDiagram
    participant C as Client
    participant Ctrl as HouseholdsController
    participant Mgr as HouseholdWorkflowManager
    participant H as Household
    participant HR as IHouseholdRepository

    C->>Ctrl: PUT /households/{id} {name, description}
    Ctrl->>Mgr: UpdateAsync(request)
    Mgr->>HR: GetByIdAsync(householdId)
    HR-->>Mgr: household (or null → 404)
    Mgr->>H: household.Update(name, description)
    H-->>Mgr: (+HouseholdUpdated event)
    Mgr->>HR: UpdateAsync(household)
    Mgr-->>Ctrl: HouseholdResponse
    Ctrl-->>C: 200 OK
```

---

## Transfer Ownership

**Entry point:** `POST /households/{id}/transfer-ownership`

```mermaid
sequenceDiagram
    participant C as Client
    participant Ctrl as HouseholdsController
    participant Mgr as HouseholdWorkflowManager
    participant H as Household
    participant HR as IHouseholdRepository
    participant MR as IHouseholdMembershipRepository

    C->>Ctrl: POST /households/{id}/transfer-ownership {newOwnerId}
    Ctrl->>Mgr: TransferOwnershipAsync(request)
    Mgr->>HR: GetByIdAsync(householdId)
    HR-->>Mgr: household
    Mgr-->>Mgr: guard: requestingUserId == household.OwnerId
    Mgr->>MR: ListByHouseholdAsync(householdId)
    MR-->>Mgr: memberships
    Mgr-->>Mgr: guard: newOwner must be active member
    Mgr->>H: household.TransferOwnership(newOwnerId)
    H-->>Mgr: (+HouseholdOwnershipTransferred event)
    Mgr->>HR: UpdateAsync(household)
    Mgr-->>Ctrl: HouseholdResponse
    Ctrl-->>C: 200 OK
```

---

## Delete Household

**Entry point:** `DELETE /households/{id}`

```mermaid
sequenceDiagram
    participant C as Client
    participant Ctrl as HouseholdsController
    participant Mgr as HouseholdWorkflowManager
    participant H as Household
    participant HR as IHouseholdRepository
    participant MR as IHouseholdMembershipRepository

    C->>Ctrl: DELETE /households/{id}
    Ctrl->>Mgr: DeleteAsync(request)
    Mgr->>HR: GetByIdAsync(householdId)
    HR-->>Mgr: household (or null → false)
    Mgr-->>Mgr: guard: requestingUserId == household.OwnerId
    Mgr->>MR: ListByHouseholdAsync(householdId)
    MR-->>Mgr: memberships
    Mgr-->>Mgr: guard: activeMemberCount <= 1
    Mgr->>H: household.Deactivate()
    H-->>Mgr: (+HouseholdDeleted event)
    Mgr->>HR: UpdateAsync(household)
    Mgr-->>Ctrl: true
    Ctrl-->>C: 204 No Content
```

## Guard failures

| Guard | Error |
|---|---|
| Requester is not owner (transfer/delete) | `UnauthorizedAccessException` |
| New owner not an active member | `InvalidOperationException` |
| Deleting with >1 active member | `InvalidOperationException` |
