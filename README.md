# portfolio-finance

Personal finance service. Tracks income sources, household expenses and expense splitting, and budget coverage analysis.

## Stack

- .NET 8 / ASP.NET Core Web API
- PostgreSQL 17 (EF Core)
- RabbitMQ (event publishing)
- Clean Architecture: Domain → Application → Infrastructure → Client

## Running locally

```bash
# From repo root — requires postgres + rabbitmq (see portfolio-infra)
dotnet run --project src/Client
```

Or via the full stack in `portfolio-infra`:

```bash
docker compose up finance
```

## Structure

```
src/
  Domain/          Aggregates, value objects, domain events
  Application/     Managers (commands), query interfaces
  Infrastructure/  EF Core, query implementations, repositories, messaging
  Client/          ASP.NET Core controllers, validators, DI wiring
```

## Docs

- [Domain model & invariants](docs/Domain.md)
- Use cases: [`docs/use-cases/`](docs/use-cases/)

## Environment variables

| Variable | Description |
|---|---|
| `ConnectionStrings__Finance` | PostgreSQL connection string |
| `Jwt__Secret` | JWT signing key (≥ 32 chars) |
| `RabbitMq__Host` | RabbitMQ hostname |
| `RabbitMq__Username` | RabbitMQ username |
| `RabbitMq__Password` | RabbitMQ password |
| `Plaid__ClientId` | Plaid dashboard client id |
| `Plaid__Secret` | Plaid dashboard secret (per environment) |
| `Plaid__Environment` | `sandbox` \| `development` \| `production` |
| `Plaid__WebhookUrl` | Public HTTPS URL terminating at `/api/finance/plaid/webhook` |

## Plaid integration

Bank linking, cursor-based transaction sync, and recurring-stream detection.
See [docs/use-cases/plaid-integration.md](docs/use-cases/plaid-integration.md)
for the full design, sequence diagrams, and production hardening checklist.
