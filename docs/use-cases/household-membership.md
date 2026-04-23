# Use Case: Household Membership

**Manager:** `HouseholdMembershipManager`

---

## Invite Member

**Entry point:** `POST /households/{id}/invitations`  
Creates a pending membership with an invitation code. The invited user is not yet known.

```mermaid
sequenceDiagram
    participant C as Client
    participant Ctrl as MembershipsController
    participant Mgr as HouseholdMembershipManager
    participant M as HouseholdMembership
    participant MR as IHouseholdMembershipRepository

    C->>Ctrl: POST /households/{id}/invitations {invitationCode}
    Ctrl->>Mgr: InviteAsync(request)
    Mgr->>M: HouseholdMembership.CreateWithInvitation(householdId, invitedBy, code)
    Note over M: UserId = Guid.Empty, IsActive = false
    M-->>Mgr: membership (+HouseholdMemberInvited event)
    Mgr->>MR: AddAsync(membership)
    Mgr-->>Ctrl: MembershipResponse
    Ctrl-->>C: 201 Created
```

---

## Join by Invitation Code

**Entry point:** `POST /memberships/join`

```mermaid
sequenceDiagram
    participant C as Client
    participant Ctrl as MembershipsController
    participant Mgr as HouseholdMembershipManager
    participant M as HouseholdMembership
    participant MR as IHouseholdMembershipRepository

    C->>Ctrl: POST /memberships/join {invitationCode}
    Ctrl->>Mgr: JoinByCodeAsync(request, userId)
    Mgr->>MR: GetByInvitationCodeAsync(code)
    MR-->>Mgr: membership (or null → 404)
    Mgr->>M: membership.AcceptInvitation(userId)
    Note over M: Sets UserId, IsActive=true, clears InvitationCode
    M-->>Mgr: (+HouseholdMemberJoined event)
    Mgr->>MR: UpdateAsync(membership)
    Mgr-->>Ctrl: MembershipResponse
    Ctrl-->>C: 200 OK
```

---

## Leave Household

**Entry point:** `DELETE /memberships/{id}/leave`

```mermaid
sequenceDiagram
    participant C as Client
    participant Ctrl as MembershipsController
    participant Mgr as HouseholdMembershipManager
    participant M as HouseholdMembership
    participant MR as IHouseholdMembershipRepository

    C->>Ctrl: DELETE /memberships/{id}/leave
    Ctrl->>Mgr: LeaveAsync(request)
    Mgr->>MR: GetByIdAsync(membershipId)
    MR-->>Mgr: membership (or null → 404)
    Mgr->>M: membership.Remove()
    M-->>Mgr: (+HouseholdMemberRemoved event)
    Mgr->>MR: UpdateAsync(membership)
    Mgr-->>Ctrl: MembershipResponse
    Ctrl-->>C: 200 OK
```

---

## Change Role / Remove Member

Both follow the same pattern as Leave: `GetByIdAsync` → domain method → `UpdateAsync`.

| Operation | Entry point | Domain method |
|---|---|---|
| Change role | `PUT /memberships/{id}/role` | `membership.ChangeRole(newRole)` |
| Remove member | `DELETE /memberships/{id}` | `membership.Remove()` |

## Guard failures

| Guard | Error |
|---|---|
| New role equals current role | `InvalidOperationException` |
| Membership already inactive | `InvalidOperationException` |
| Invitation code not found | Returns `null` (404) |
| AcceptInvitation on active membership | `InvalidOperationException` |
| AcceptInvitation without invitation code | `InvalidOperationException` |
