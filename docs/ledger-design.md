# Double-Entry Ledger — Design (accounting-grounded)

> Supersedes the `payer-reimbursement-*` model. Those docs designed a *transfer*
> log (`Reimbursement(from,to,amount)`); this is the real **double-entry ledger**.
> The work already shipped (signed `Money`, the append-only reversal mechanic, the
> `reimbursements` table) feeds straight in — a reimbursement becomes one *journal
> entry type*, and its rows migrate into postings.
>
> **Boundary (non-negotiable):** finance owns the ledger and knows nothing of
> "household." Everything is scoped by opaque `GroupId` / `UserId`. The ledger is
> exposed only through read-query endpoints (`GET /api/finance/groups/{groupId}/ledger`);
> household/frontend pull from those — no shared types, no event-replication of postings.

---

## 1. Accounting principles this design must honor

A domain expert (CPA / ledger-systems architect) would hold the design to these.
Each row states the principle and how we satisfy it.

| # | Principle | How we satisfy it |
|---|---|---|
| P1 | **The accounting equation** — Assets = Liabilities + Equity (+ Income − Expense). | Every account has a type; the equation is a derivable invariant per ledger. |
| P2 | **Double-entry / duality** — every transaction touches ≥2 accounts; **Σ debits = Σ credits**. | `JournalEntry` rejects unless its postings balance to zero. Enforced in the aggregate. |
| P3 | **Normal balances** — Asset/Expense are debit-normal; Liability/Equity/Income are credit-normal. | `Account.NormalBalance` derived from `AccountType`; increases/decreases map to Dr/Cr accordingly. |
| P4 | **Immutability of the journal** — posted entries are never edited or deleted; corrections are **reversing entries**. | Append-only. A correction posts a new entry that negates the original (we already built this for reversals). |
| P5 | **Trial balance** — at any instant, Σ all debits = Σ all credits across a ledger. | A reconciliation query; must return zero imbalance. Tested as an invariant. |
| P6 | **Conservation** — value is neither created nor destroyed; it only moves between accounts. | Follows from P2; asserted in property tests. |
| P7 | **Recognition basis** — cash vs accrual must be explicit and consistent. | **Decision §6.1.** Recommend cash-basis for v1 with an accrual-ready shape. |
| P8 | **Entity / inter-entity integrity** — each ledger (entity) self-balances; cross-entity flows use **reciprocal control accounts** that reconcile (intercompany). | **Decision §6.2.** Cross-ledger transfers post a balanced entry in each ledger, linked, with reciprocal "Due to/from" accounts. |
| P9 | **Source / audit trail** — every entry references its originating document. | `JournalEntry.Source` (expenseId, reimbursement, bank txn). |
| P10 | **Monetary unit** — one currency per ledger (multi-currency needs FX accounts). | Single currency per `Ledger` in v1; multi-currency deferred. |
| P11 | **Nominal vs real accounts** — Income/Expense are temporary (closed to equity each period); Assets/Liabilities/Equity are permanent. | Modelled via `AccountType`; period-close is deferred (household app), noted as future. |

---

## 2. Core model

```
Ledger        — a self-balancing book of accounts.
                { LedgerId, OwnerType {Group|User}, OwnerId (opaque Guid), Currency }

Account       — a line in ONE ledger's chart of accounts.
                { AccountId, LedgerId, Code, Name, AccountType, ParentAccountId? }
                AccountType ∈ {Asset, Liability, Equity, Income, Expense}
                NormalBalance = Debit for {Asset,Expense}; Credit for {Liability,Equity,Income}
                ParentAccountId enables rollup: balance(parent) = Σ children + own postings.

JournalEntry  — one economic event posted to ONE ledger (book of original entry).
                { EntryId, LedgerId, Date (value date), Description, Source, RecordedAt,
                  ReversalOfEntryId? }
                Invariant (P2): Σ postings.signedAmount == 0.

Posting       — one line of a journal entry against one account.
                { PostingId, EntryId, AccountId, Direction {Debit|Credit}, Amount (Money, > 0) }
                signedAmount = +Amount for Debit, −Amount for Credit (or per-type convention).
```

We store **Direction + positive Amount** (true debit/credit, P3), and compute a
signed amount for summing. This preserves accountant-facing semantics rather than
hiding everything behind a sign.

**Balance of an account** (P1/P3): `Σ over postings of (signed by direction)`, oriented
to the account's normal balance so a debit-normal account shows a positive balance when
net-debited. Rollup adds children (P-hierarchy).

---

## 3. Invariants (enforced, not aspirational)

- **Entry balances (P2):** `JournalEntry.Post(...)` throws unless Σ debits == Σ credits.
- **Ledger trial balance (P5):** `Σ debits == Σ credits` across all entries in a ledger — a query + a test.
- **Append-only (P4):** no update/delete of postings; `Reverse(entry)` posts a mirror entry (swap Dr↔Cr) referencing the original.
- **Single currency (P10):** every posting in an entry shares the ledger's currency.
- **Conservation (P6):** property test over random entries — total debits == total credits.

---

## 4. Chart of accounts

**Group ledger** (`OwnerType=Group`, opaque `GroupId`) — the shared book. No "household" anywhere:

| Code | Account | Type | Meaning |
|---|---|---|---|
| 1000 | Cash | Asset | the shared pool ("household fund" in plain speak) |
| 2000 | Vendor Payable | Liability | obligations to external billers (accrual only) |
| 3000.{userId} | Member:{userId} | Equity | each member's stake = contributions − consumption |
| 4000.{userId} | Due to/from Member:{userId} | Asset/Liability | reciprocal control for cross-ledger (P8) |
| 5000.{cat} | Expense:{category} | Expense | shared cost recognized (optional in cash-basis) |

**User ledger** (`OwnerType=User`, `UserId`) — the member's personal book:

| Code | Account | Type | Meaning |
|---|---|---|---|
| 1000 | Checking / Cash | Asset | their cash (optionally Plaid-linked `FinancialAccount`) |
| 1100 | Savings | Asset | |
| 2000 | Credit Card | Liability | what they owe the card issuer (optionally Plaid-linked) |
| 4000.{groupId} | Due to/from Group:{groupId} | Asset/Liability | reciprocal control (P8) |
| 5000 | Expense | Expense | personal expenses |
| 6000 | Income | Income | paychecks |

An `Account` may carry an optional `FinancialAccountId` linking it to the existing
Plaid-synced `FinancialAccount`, so a real card/bank reconciles against its feed.

---

## 5. Worked journal entries (the proof it's correct)

### 5.1 Pooled model — everyone contributes their share, pool pays the vendor
*(Group ledger, cash-basis. Rent $1,000, Hank $700 / Bob $300.)*

```
① Hank contributes $700        Dr Cash 700      Cr Member:Hank 700
② Bob contributes $300         Dr Cash 300      Cr Member:Bob  300
③ Pool pays vendor $1,000      Dr Member:Hank 700
                               Dr Member:Bob  300   Cr Cash 1,000
   → Cash 0, both members 0. Trial balance: Σdr 2,000 = Σcr 2,000 ✓
```

### 5.2 Front-and-reimburse — Hank fronts the whole bill from his own cash
*(Cross-ledger: Hank's cash is in his USER ledger; the GROUP tracks positions. Uses reciprocal accounts, P8.)*

```
GROUP ledger
① Bill allocated, Hank fronts  Dr Member:Bob 300      (Bob consumed his share)
                               Cr Due to Member:Hank 300   (group owes Hank for fronting Bob)
   (Hank's own $700 share: Dr Member:Hank 700 / Cr Due to Member:Hank 700, then his
    contribution clears it — nets out; shown collapsed.)

USER ledger (Hank)
② Hank pays vendor $1,000      Dr Rent Expense 700        (his own share = his cost)
   from Checking               Dr Due from Group 300      (the group owes him for Bob's share)
                               Cr Checking 1,000
   → each book balances independently; "Due from Group" (Hank) == "Due to Member:Hank" (group). ✓ (P8)

③ Bob reimburses Hank $300
   USER ledger (Bob)           Dr Rent Expense 300    Cr Credit Card 300   (Bob pays with his card)
   USER ledger (Hank)          Dr Checking 300        Cr Due from Group 300
   GROUP ledger                Dr Due to Member:Hank 300   Cr Member:Bob 300
   → all reciprocal accounts tie to zero; Bob's cost sits on his card. ✓
```

### 5.3 Credit-card mechanics (the validation that drove this)
```
Pay down card from checking    Dr Credit Card 300   Cr Checking 300     (liability ↓, asset ↓)
Pay a share WITH the card      Dr <expense / Due from Group> 300  Cr Credit Card 300  (liability ↑)
```
Any posting targets any account — a card is just a Liability account (P3).

---

## 6. The two decisions a real accountant would force

### 6.1 Recognition basis (P7) — **cash vs accrual**
- **Cash basis:** recognize cost/obligation when money moves. Simpler; matches a
  household's intuition ("I owe when it's time to pay"). No Vendor Payable needed.
- **Accrual basis:** recognize when the bill is *incurred* (due date), independent of
  payment. Better forward visibility ("you owe your share even though it's unpaid"),
  but adds Vendor Payable + period mechanics.
- **Recommendation:** **cash-basis v1**, but keep `Vendor Payable` in the chart and the
  `JournalEntry.Source`/date shape accrual-ready, so we can switch a single expense to
  accrual later without reshaping. (This mirrors the earlier "recognize at vendor
  payment; pre-payment = advance" decision — that *was* a cash-basis choice.)

### 6.2 Entity model (P8) — **how strict on inter-ledger**
- Group and User ledgers are **separate self-balancing entities**. A flow between them
  (fund the group, pay a group bill with a personal card) is **intercompany**: a
  balanced entry in *each* book, linked by a shared `TransactionId`, reconciled through
  reciprocal **Due to/from** control accounts (shown in §5.2).
- **Recommendation:** model the reciprocal accounts from day one (they're cheap and they
  keep each ledger honest), but **build the Group ledger first** — it's self-contained
  for the pooled model (§5.1) and needs no cross-ledger. Add User ledgers + reciprocal
  postings in phase 2 (this is also when credit-card-as-funding-source lights up).

---

## 7. How existing concepts map onto the ledger

| Today | Becomes |
|---|---|
| `Expense` (group) | a *source document*; its allocation drives journal entries |
| `ExpenseSplit` | the allocation amounts → the per-member postings of the expense entry |
| `Reimbursement` (just built) | one **journal-entry type** (a member-to-member or member-to-pool transfer); its reversal = a reversing entry (we already built that semantics) |
| `VendorPayment` (planned) | a journal-entry type (pool/member → Vendor Payable / external) |
| `ListMemberBalancesAsync` | `balance(Member:{userId})` per account — a ledger query |
| signed `Money` | the posting amount primitive (Dr/Cr + positive amount; signed for sums) |

**Migration of `reimbursements` rows → postings:** each existing reimbursement
(`from`,`to`,`amount`) becomes a balanced `JournalEntry` with two postings
(Dr `Member:{from-payable}` / Cr `Member:{to-receivable}` per the recognition rule),
sourced to the original row. Reversed rows (negative contra) become reversing entries.
The `reimbursements` table is retained as a source archive; postings are derived in the
migration and thereafter authoritative.

---

## 8. Aggregates, persistence, endpoints

- **Aggregates:** `Ledger` (creates accounts, posts entries — guards P2/P10),
  `Account` (typed, hierarchical), `JournalEntry` (balanced postings; immutable; reverse),
  `Posting` (Dr/Cr + Money). Strongly-typed ids as elsewhere.
- **Tables:** `ledgers`, `accounts`, `journal_entries`, `postings` (schema `finance`).
  Indexes: postings by `account_id`; entries by `ledger_id, date`. snake_case `HasFilter`
  where filtered.
- **Read endpoints (boundary §intro):**
  `GET /api/finance/groups/{groupId}/ledger` — the group book + balances,
  `GET /api/finance/ledger` — the caller's personal book,
  `GET /api/finance/groups/{groupId}/accounts/{accountId}/statement` — per-account postings,
  `GET /api/finance/groups/{groupId}/trial-balance` — the P5 reconciliation.

---

## 9. Build sequence (tested layers, like the rename)

1. **Ledger core (Group):** `Ledger`/`Account`/`JournalEntry`/`Posting` aggregates +
   the balanced-entry invariant (P2) + trial-balance test (P5). Pure domain, unit-tested.
2. **Persistence + chart bootstrap:** tables, configs, a group ledger created per group
   with its standard accounts; migrate `reimbursements` → posting entries (§7).
3. **Posting from expenses:** `Expense`/`ExpenseSplit` generate the allocation entries
   (cash-basis, §6.1). Replace `ListMemberBalancesAsync` with `balance(Member:{x})`.
4. **Read endpoints:** group ledger / statement / trial-balance queries.
5. **User ledgers + intercompany (phase 2):** personal accounts incl. Credit Card
   (optional Plaid link), reciprocal Due-to/from postings (§6.2), cross-ledger transfers.
6. **Frontend:** ledger/statement views; the funds/cash-position view becomes a query
   over the user ledger's asset accounts.

Each step leaves a balanced, queryable ledger and is independently shippable.

---

## 10. Open decisions for sign-off
1. **§6.1 recognition basis** — cash-basis v1 (recommended) or accrual now?
2. **§6.2 entity model** — group ledger first with reciprocal accounts reserved (recommended), or build user ledgers + intercompany in the same phase?
3. **Expense accounts in the group ledger** — track nominal `Expense:{category}` accounts (full P&L, P11) or collapse expenses directly into member-equity draws (simpler, §5.1)? Recommend nominal accounts for reporting, but it's a real choice.

---

## 11. Remodel — funding account, source-document language, settlement collapse

> Decided in the remodel session (2026-05-31). This section supersedes the ad-hoc
> "payer / reimbursement" framing of §5 and §7 for the *language* and the *write path*;
> the accounting structure (§1–§4) is unchanged. The trigger was a real modelling
> insight: **there is a payment to the vendor and a payment to another member, and the
> received funds are used as part of the vendor payment.**

### 11.1 The unifying insight — the *funding account* (the one volatility)

A vendor is always paid by **exactly one account** — the **funding account**. Whether
that account is a member's own pocket (today's "one payer, others repay") or a shared
household account (a future pool) is *only which account id is credited*. The journal
structure is identical:

```
Vendor paid:   Dr Expense:{cat} (total)        Cr FundingAccount (total)
Allocate:      Dr Member:{each} (their share)   Cr Expense:{cat} (total)
Settlement:    Dr FundingAccount               Cr Member:{debtor}
```

- **One-payer reality** — `FundingAccount = Member:{payer}`. `Cr Member:Hank 1000` minus
  his own `Dr Member:Hank 700` nets to `+300` (others owe him); a settlement is
  `Dr Member:Hank / Cr Member:Bob`. (This is exactly what the engine does today.)
- **Shared-pool / any future source** — `FundingAccount = Cash` (or a Credit Card liability,
  a bank account, …). `Cr Cash 1000`; every member owes their full share into the pool;
  a settlement is `Dr Cash / Cr Member:Bob`.

**This is the single volatility axis we encapsulate: *what funds a vendor payment.***
`pay-first` (front-and-reimburse) and `collect-first` (contribute-then-pay) become the
**same** `Dr FundingAccount / Cr Member` posting — so there is *one* settlement concept,
not two. We build the funding account as a first-class parameter now so future funding
sources (shared account, card, transfer) are drop-ins, not rebuilds.

### 11.2 Ubiquitous language (the two languages, P-vocabulary)

`Expense` / `ExpenseSplit` / `Reimbursement` were settle-up-app words. Once the **ledger
is the system of record** for who-owes-whom, the write-side aggregates are **source
documents** that *drive* journal entries — and they should speak accounting. Decision:
**full rename.** `Expense` survives only as the nominal account *category* `5000:{cat}`.

| Old (app word) | New — domain / accounting | Role |
|---|---|---|
| `Expense` (aggregate) | **`Charge`** | source document: a cost incurred by the group, allocated across members, funded by an account |
| `ExpenseSplit` | **`Allocation`** (lines = **`Share`**) | the apportionment of a Charge across members |
| — (new) | **`Payment`** | funding account → vendor (recognizes the expense, cash-basis). Implicit at Charge creation for one-payer; explicit event for a shared pool |
| `Reimbursement` | **`Settlement`** | member ↔ funding account: `Dr FundingAccount / Cr Member`. Subsumes "reimburse the payer" and "contribute to the pool" |
| — (concept) | **`FundingAccount`** | the account that pays the vendor (`Member:{payer}` \| `Cash` \| Card \| …) |
| `Expense:{category}` | **unchanged** | the nominal expense *account* in the chart (`5000:{cat}`) — the only place the word "expense" remains |

**UI keeps plain words** regardless: "expense", "your share", "who owes whom",
"mark as paid", "pay the bill". The accounting language lives in `Domain/` + DTOs only.

> Open atom: **`Charge` vs `Bill`.** Recommend **`Charge`** — neutral umbrella covering
> both a member-purchased shared cost (already paid) and a vendor obligation (to be paid).
> Reserve **`Bill`** for the accrual sub-case (a Charge whose funding is deferred via
> Vendor Payable) if/when accrual lands.

### 11.3 Write model — source documents drive the ledger

```
Charge (aggregate root, source document)
  { ChargeId, GroupId, Money total, category, valueDate, FundingAccountRef,
    Allocation, RecurrenceRule? }
Allocation (owned)         — the Shares; Σ Shares ≤ total (funder absorbs remainder)
  Share { MemberId (opaque UserId), Money }
Payment (source document)  — FundingAccount → vendor; one-payer = implicit at Charge create
Settlement (source document, replaces Reimbursement)
  { GroupId, ChargeId, occurrence (DateOnly), fromMemberId, Money, valueDate }
  → drives ONE balanced JournalEntry: Dr FundingAccount / Cr Member:{from}
```

The **`Ledger` is the single source of truth** for balances and settled-state. Charge /
Allocation / Payment / Settlement capture business intent + provenance; the
`JournalizingEngine` turns each into balanced `JournalEntry` drafts. **No dual write** —
the `reimbursements` table is removed; a settlement *is* a journal entry, and reversing
it is one reversing entry (P4).

### 11.4 Read model — structured source attribution (replaces the `reimbursements` reads)

Aggregate "who owes whom" → `balance(Member:{userId})` (already true). The finer
**per-occurrence "is this share settled?"** (contributions grid) needs to attribute a
journal entry back to its origin without parsing the `Source` string. Add nullable,
indexed structured-source columns to `JournalEntry`:

```
JournalEntry.SourceChargeId?   (Guid)
JournalEntry.SourceOccurrence? (DateOnly)   — for recurring charges
JournalEntry.SourceMemberId?   (Guid)       — the settling member, for settlements
```

`BookkeepingManager` populates them when posting Charge and Settlement entries. The five
reads currently keyed on `_db.Reimbursements` (`ExpenseQuery.{ListByHouseholdAsync,
ListSplitsByHouseholdAsync, GetPaidSplitIdsForExpenseAsync, ListMemberBalancesAsync}`,
`IncomeQuery.FetchPaidSplitOccurrences*`) derive "settled per (charge, occurrence, member)"
from settlement journal entries via these columns. `JournalEntry.Source` remains the
human/audit string (P9); the columns are the queryable index.

### 11.5 Cross-service coordination (the household break)

Household's activity feed consumes the finance domain event `ReimbursementRecorded`.
Renaming `Reimbursement → Settlement` renames that event to **`SettlementRecorded`** — a
coordinated cross-service break, the same shape as the earlier `SplitClaimed → SplitPaid`
rename (finance publishes domain events directly; consumers declare matching types in
`Finance.Domain.Events`). Sequence the wire change so both services stay green: finance
emits the renamed event, household binds to it, then the old type is dropped.

### 11.6 Build sequence (tested layers; each step live-verified)

1. **Funding account in the engine** — generalize `ExpenseAllocationContext.PayerAccount`
   and `ReimbursementContext` to a `FundingAccount` (resolves to `Member:{payer}` today).
   Postings stay byte-identical; 109 tests stay green. Add the `Cash`-as-funding tests.
2. **Rename** `Expense→Charge`, `ExpenseSplit→Allocation`/`Share` across Domain → App →
   Infra → Client → Frontend DTOs; `Reimbursement→Settlement` incl. the domain event.
   `Expense:{category}` account code/name unchanged.
3. **Structured source columns** on `JournalEntry` (+ EF config + migration); populate in
   `BookkeepingManager`. Backfill any `reimbursements` rows → settlement entries (prod-shape;
   dev table is empty).
4. **Switch the five reads** off `reimbursements` onto settlement journal entries (§11.4).
5. **Drop** the `Reimbursement` aggregate / `ReimbursementId` / config / repository /
   `reimbursements` table (migration) / the old event. Rewire household to `SettlementRecorded`.
6. **Cash path surfaced** — `Payment` event + a funding-account choice when a shared account
   exists; UI "pay the bill" wired to it. (Engine already supports it after step 1.)

Each step leaves a balanced, queryable ledger and is independently shippable.

### 11.7 Engine primitives — *Allocation* vs *Transfer* (role names earn their place)

The journalizing engine exposes exactly two shapes, and the line between them is a naming rule:

- **`JournalizeExpense` (allocation, 1→many)** — the accounts play *different* parts
  (expense debited then credited, funding credited + absorbs remainder, members debited
  their share). The policy *branches on the roles*, so role-named fields (`ExpenseAccount`,
  `FundingAccount`, `MemberAccount`) are earned. This is the engine's real volatility.
- **`JournalizeTransfer` (1↔1)** — settlement, contribution, payment, payoff are all the same
  thing: a balanced move between **two accounts**. They get symmetric, opposite treatment
  (one debited, one credited), so the engine branches on *nothing*. The context is therefore
  role-free — `TransferContext { DebitAccount, CreditAccount, Amount }` — and the **caller
  resolves the business roles** (which account is the member, which the funding account) and
  picks the direction. Business roles never leak into the posting primitive.

**Rule:** name an account field by its role only when the engine *treats the roles
differently*; otherwise it is just an account, named by direction. Transfer-shaping stays in
the engine (it is on the engine's volatility axis — if recognition policy later routes a
settlement through a Cash/clearing account, this shape changes with it), which also keeps all
debit/credit knowledge out of the Manager (a Manager holding accounting rules would be the
real IDesign violation). The Manager only chooses *which* account is debited for a given event.
