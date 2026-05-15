# portfolio-finance

Personal finance service. Tracks income sources, recurring personal expenses, household shared expenses with configurable splits, and produces a budget coverage analysis showing how much of your obligations your income actually covers.

Also integrates with Plaid for bank linking and transaction sync — so instead of manually entering every bill, you can connect a bank account and have recurring transactions detected automatically.

## What it does

- **Income** — record salary, freelance, rental income, or any recurring/one-time source; stored with frequency so monthly totals can be normalised
- **Expenses** — personal recurring obligations (rent, subscriptions, insurance, gym); categorised and tracked against income
- **Household expenses** — shared bills attached to a household (from the household service); split equally or by custom amounts; tracks payment status per member
- **Contributions** — monthly snapshot of what each household member has contributed vs what they owe; used to compute "your share" on the frontend
- **Budget analysis** — gross income vs total obligations, coverage percentage, surplus/deficit
- **Plaid** — bank link flow (link token → public token exchange), cursor-based transaction sync, recurring stream detection

## Stack

- .NET 8 / ASP.NET Core Web API
- PostgreSQL 17 (EF Core)
- RabbitMQ (event publishing via MassTransit)
- Plaid .NET SDK
- Clean Architecture: Domain → Application → Infrastructure → Client

## Running locally

```bash
# From repo root — requires postgres + rabbitmq (see infra/)
dotnet run --project src/Client
```

Or via the full stack:

```bash
docker compose -f infra/compose.dev.yaml up finance
```

## Structure

```
src/
  Domain/          Aggregates, value objects, domain events
  Application/     Managers (commands), query interfaces, repository interfaces
  Infrastructure/  EF Core, query implementations, repositories, Plaid client, messaging
  Client/          ASP.NET Core controllers, FluentValidation validators, DI wiring
```

## API surface

| Controller | Routes | Purpose |
|---|---|---|
| `IncomeController` | `GET/POST /api/finance/income`, `PUT/DELETE …/{id}` | Income sources |
| `ExpensesController` | `GET/POST /api/finance/expenses`, `PUT/DELETE …/{id}` | Personal expenses |
| `HouseholdExpensesController` | `GET/POST /api/finance/households/{id}/expenses` | Shared household bills |
| `ContributionsController` | `GET /api/finance/contributions/summary` | Monthly contribution rollup |
| `PlaidController` | `POST /api/finance/plaid/link-token`, `…/exchange`, `…/sync`, `…/webhook` | Plaid bank linking |

## Environment variables

| Variable | Description |
|---|---|
| `ConnectionStrings__Finance` | PostgreSQL connection string |
| `Jwt__Secret` | JWT signing key (≥ 32 chars) |
| `RabbitMq__Host` | RabbitMQ hostname |
| `RabbitMq__Username` | RabbitMQ username |
| `RabbitMq__Password` | RabbitMQ password |
| `Plaid__ClientId` | Plaid dashboard client ID |
| `Plaid__Secret` | Plaid dashboard secret (per environment) |
| `Plaid__Environment` | `sandbox` \| `development` \| `production` |
| `Plaid__WebhookUrl` | Public HTTPS URL terminating at `/api/finance/plaid/webhook` |

## Plaid integration

Bank linking, cursor-based transaction sync, and recurring-stream detection.
See [docs/use-cases/plaid-integration.md](docs/use-cases/plaid-integration.md)
for the full design, sequence diagrams, and production hardening checklist.

## Docs

- [Domain model & invariants](docs/Domain.md)
- Use cases: [`docs/use-cases/`](docs/use-cases/)

