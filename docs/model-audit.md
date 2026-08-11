# Model audit — finance

An accountant's read of every aggregate in `Finance.Domain`: what it is for, what it integrates
with, and where the model does not yet say what we mean. Written 2026-08-11, after the personal
ledger landed (`3ad2802`) and the correction-period fix (`b3df90b`).

The verdict up front: **the bookkeeping is sound, the accounting policy around it is not.** The
journal enforces real double-entry. What is missing sits one level up — a period, a document, and a
single declared basis of accounting.

---

## 1. The three families

The sixteen types divide cleanly by who owns the truth. Confusing the families is the source of
most findings below.

| Family | Types | Truth lives |
| --- | --- | --- |
| **The books** | `Ledger`, `Account`, `JournalEntry`, `Posting`, `LedgerMath`, `GroupChart`, `PersonalChart` | Here. Immutable, derived, self-proving. |
| **The intent** | `Charge`, `Allocation`, `ChargePayment`, `IncomeSource`, `DebtTerms` | Here — then projected into the books. |
| **The outside world** | `FinancialConnection`, `FinancialAccount`, `FinancialTransaction`, `BankSyncSuggestion`, `RecurringSuggestion` | At the provider. Never authoritative. |

### The books

- **`Ledger`** — one set of books for one accounting entity. `OwnerType` is `{Group, User}` and
  currency is fixed at open. This is the entity boundary and it is correct: the accounting equation
  only balances *within* an entity.
- **`Account`** — a place postings land. Holds no balance by design; `NormalBalance` is derived from
  `AccountType`, never stored. Hierarchical via `ParentAccountId`.
- **`JournalEntry` / `Posting`** — the strongest part of the model. An entry cannot exist unless
  Σdebits = Σcredits, ≥2 lines, all positive, one currency. Immutable — corrections are mirror
  entries, never edits. Provenance is columnar (`SourceChargeId`, `SourceAllocationId`,
  `SourceOccurrence`, `SourceMemberId`) rather than parsed from free text.
- **`LedgerMath`** — `AccountBalance`, `TrialBalance`, `IsBalanced`. The proof obligations exist.

### The intent

- **`Charge`** — a cost, personal (`GroupId == null`) or shared. Carries `FundingSource`
  (`PayerMember` / `GroupCash`) and one `Amount`.
- **`Allocation`** — one member's share of a charge.
- **`ChargePayment`** — that a member paid *a given occurrence*. Occurrence-aware.
- **`IncomeSource`** — recurring income with a `TaxWithholdingProfile`.
- **`DebtTerms`** — rate, limit, statement day for a liability account. Deliberately beside
  `Account`, not on it: a rate changes by agreement, a balance by transaction.

### The outside world

`FinancialConnection` → `FinancialAccount` → `FinancialTransaction`, plus two suggestion read
models. Correctly namespaced `Finance.Domain.ReadModels` and correctly non-authoritative.

---

## 2. How they integrate

```
Charge / Allocation / settlement          (intent, mutable)
        │  domain event → outbox → LedgerPostingConsumer
        ▼
BookkeepingManager                        (the only writer to the books)
        │  IJournalizingEngine → balanced drafts
        ▼
JournalEntry + Posting                    (books, immutable)
        │
        ├─ LedgerQuery         → group ledger, account statement
        └─ /api/finance/me/position → personal position, DERIVED from
                                     Member:{userId} equity in each group book
```

Two integration rules hold today and are worth keeping:

1. **`BookkeepingManager` is the only writer to the books.** Controllers never journalise.
2. **Posting is convergent, not incremental.** Each sync re-derives what *should* be on the books
   for a source key and reconciles. Redeliveries and no-op edits fall out as no-ops, which is what
   makes event-driven posting safe.

---

## 3. Findings

### F1 — There is no period *(material, systemic)*

Nothing in the model represents an accounting period. No close, no lock, no fiscal calendar. Any
entry can be posted to any date at any time.

Everything below is a symptom. Fixing F1 makes F2–F4 tractable; fixing them individually does not.

### F2 — Recurring charges have no occurrence *(material)*

A recurring bill is one `Charge` with one `Amount` and a `RecurrenceSchedule`. Every month's
instance is *computed*. Consequences:

- Editing the amount restates every past month. History is not history.
- The accrual is keyed `charge:{chargeId}` — **one entry for N periods**, so period reporting cannot
  be right even before anyone edits anything.
- Settlements *are* keyed per occurrence (`settlement:{chargeId}:{yyyyMMdd}:{user}`). So after an
  amount edit, expense and member balances disagree and nothing detects it.

`ChargePayment` already carries `OccurrenceDate`, and `JournalEntry` already has `SourceOccurrence`.
The occurrence exists everywhere as a *date key* and nowhere as an *entity with an amount*.

**The fix is the one accounting already names: an invoice.** Materialise the occurrence, freeze its
amount when accrued, key the accrual `charge:{chargeId}:{yyyyMMdd}`. Editing the charge then changes
future occurrences; past ones keep what they were billed at. Anchor-date derivation stays correct
for periods not yet accrued.

### F3 — The basis of accounting is not a policy *(material)*

Two engines exist. `JournalizeAccrual` (Dr Expense / Cr Vendor Payable) is what runs.
`JournalizeCharge` (cash basis, two entries) **is never called outside tests**, and
`ledger-design.md` §6.1 recommends cash-basis v1.

The intended policy, the documented policy and the implemented policy are three different things.
An entity has one basis. Delete the dead engine or promote it, and write down which.

### F4 — The group has no income statement *(design, not defect)*

Live path: `Dr Expense / Cr Payable`, then per allocation `Dr Member / Cr Expense`. Fully allocated,
**every Expense account nets to exactly zero**. They are clearing accounts wearing an expense type,
and "what did the house spend on groceries" is not answerable from the ledger — which contradicts
the claim that the ledger is the single source of truth.

The economics are right: a household allocating costs is a conduit and bears nothing. The *model*
should say so. Now that user ledgers exist, the expense belongs in each member's book where it is
genuinely theirs. Type these as clearing and the model matches reality.

### F5 — Income never reaches any book

`IncomeSource` is the richest aggregate in the domain — recurrence, tax withholding, deductions —
and `BookkeepingManager` never references it. `AccountType.Income` appears **once in the entire
service**, in the normal-balance mapping. No income account is ever opened.

A personal book with expenses and no income cannot answer the only question people ask of one.

### F6 — Personal charges do not post

`LedgerPostingConsumer` skips `GroupId == null`. Correct as written — personal costs must not touch
the group's books — but now that a personal ledger exists there is somewhere for them to go, and
until they go there the personal book contains only opening balances.

### F7 — The charts are the same data written twice

`GroupChart.ExpenseCode` and `PersonalChart.ExpenseCode` are character-for-character identical; both
declare `CashCode = "1000"`. A chart of accounts is *data* — role → (code, name, type), plus a seed
list per ledger kind. Two static classes is that data expressed twice.

### F8 — No actor on an entry

`JournalEntry` records `RecordedAt` but no user. Entries are machine-generated from events so origin
is traceable via `SourceChargeId` — partial mitigation, not an audit trail.

### F9 — Unallocated remainder is treated two ways

Cash basis debits the remainder to the funding account (the funder bears it). Accrual leaves it in
Expense (the entity bears it). Same concept, two answers. Moot while F3 stands, live again the
moment both bases are.

---

## 4. What is genuinely right

Worth stating, because the findings are all about the layer above:

- Balance is an invariant of the entry, not a check someone remembers to run.
- Immutability with mirror-entry correction — the audit trail cannot be rewritten.
- Balances derived from postings, never stored, so an account cannot drift from its journal.
- Entity separation via `Ledger`, with `LedgerOwnerType` admitting both from the start.
- Provenance as columns, so attribution never parses a string.
- Convergent posting, which makes at-least-once delivery safe.
- Terms held beside accounts rather than on them.
- Read models namespaced apart from aggregates and never treated as truth.

---

## 5. Recommended order

1. **Occurrences (F2)** — unblocks period correctness and gives the document to attribute expense to.
2. **Period close (F1)** — once occurrences exist, a period has edges worth locking.
3. **Declare the basis, delete the dead engine (F3, F9).**
4. **Personal charge posting + income accounts (F5, F6)** — makes the personal book answer something.
5. **Clearing-account typing (F4)** and **one chart model (F7)** — cosmetic next to the rest.
6. **Actor on entries (F8)** — cheap, do it with any of the above.
