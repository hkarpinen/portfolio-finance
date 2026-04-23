# Use Case: Create Household

**Actor:** Authenticated user  
**Entry point:** `POST /households`  
**Manager:** `HouseholdWorkflowManager.CreateAsync`

## Flow

```mermaid
sequenceDiagram
    participant C as Client (HTTP)
    participant Ctrl as HouseholdsController
    participant Mgr as HouseholdWorkflowManager
    participant H as Household
    participant M as HouseholdMembership
    participant HR as IHouseholdRepository
    participant MR as IHouseholdMembershipRepository

    C->>Ctrl: POST /households {name, currency, description}
    Ctrl->>Mgr: CreateAsync(request)
    Mgr->>H: Household.Create(name, ownerId, currency)
    H-->>Mgr: household (+HouseholdCreated event)
    Mgr->>HR: AddAsync(household)
    Mgr->>M: HouseholdMembership.Create(householdId, ownerId, Owner)
    M-->>Mgr: ownerMembership (+HouseholdMemberJoined event)
    Mgr->>MR: AddAsync(ownerMembership)
    Mgr-->>Ctrl: HouseholdResponse
    Ctrl-->>C: 201 Created
```

## Notes

- Creating a household atomically creates the owner's `HouseholdMembership` with `Role = Owner`.
- The household and membership are saved in separate repository calls (no transaction scope — eventual consistency via outbox).
