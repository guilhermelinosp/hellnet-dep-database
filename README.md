# Hellnet Database

PostgreSQL-first database infrastructure library for .NET. Env-first, modular, cloud-native.

```
Env vars → HellnetDatabaseOptions → NpgsqlDataSource → IDatabaseExecutor / IRepository<T>
```

---

## Quick Start

### 1. Install

```bash
dotnet add package Hellnet.Database
```

### 2. Set environment variables

```bash
export HELLNET_DATABASE_HOST=localhost
export HELLNET_DATABASE_PORT=5432
export HELLNET_DATABASE_NAME=mydb
export HELLNET_DATABASE_USERNAME=postgres
export HELLNET_DATABASE_PASSWORD=password

export HELLNET_DATABASE_POOL_MIN_SIZE=10
export HELLNET_DATABASE_POOL_MAX_SIZE=100
export HELLNET_DATABASE_COMMAND_TIMEOUT_SECONDS=30
```

### 3. Register in DI

```csharp
using Hellnet.Database;

var builder = WebApplication.CreateBuilder(args);

// From environment variables (recommended)
builder.Services.AddHellnetDatabase();

// Or with explicit options
builder.Services.AddHellnetDatabase(new HellnetDatabaseOptions
{
    Host = "pg.internal",
    Database = "orders",
    Username = "app",
    Password = "secret",
});
```

### 4. Use

```csharp
public class OrderService
{
    private readonly IDatabaseExecutor _db;
    private readonly IRepository<Order> _orders;

    public OrderService(IDatabaseExecutor db, IRepository<Order> orders)
    {
        _db = db;
        _orders = orders;
    }

    public async Task<IReadOnlyList<Order>> GetPendingAsync()
    {
        return await _db.QueryAsync<Order>(
            "SELECT * FROM orders WHERE status = 'pending'");
    }

    public async Task<Order?> GetByIdAsync(int id)
    {
        return await _orders.GetByIdAsync(id);
    }
}
```

---

## Features

### Connection Management

Uses `NpgsqlDataSource` for connection pooling, lifecycle, and diagnostics.
Configured entirely via environment variables — no connection strings in code.

| Env | Default | Description |
|-----|---------|-------------|
| `HELLNET_DATABASE_HOST` | `localhost` | PostgreSQL host |
| `HELLNET_DATABASE_PORT` | `5432` | PostgreSQL port |
| `HELLNET_DATABASE_NAME` | *(required)* | Database name |
| `HELLNET_DATABASE_USERNAME` | *(required)* | Database user |
| `HELLNET_DATABASE_PASSWORD` | *(required)* | Database password |
| `HELLNET_DATABASE_POOL_MIN_SIZE` | `10` | Minimum pool size |
| `HELLNET_DATABASE_POOL_MAX_SIZE` | `100` | Maximum pool size |
| `HELLNET_DATABASE_COMMAND_TIMEOUT_SECONDS` | `30` | Command timeout |
| `HELLNET_DATABASE_RETRY_ENABLED` | `true` | Enable retry on transient errors |

### Resilience (Polly)

Automatic retry with exponential backoff and permanent error discrimination.
Does **not** retry:

| SQL State | Error |
|-----------|-------|
| `42601` | Syntax error |
| `23505` | Unique violation |
| `23503` | Foreign key violation |
| `42501` | Permission denied |
| `42P01` | Undefined table |
| `42703` | Undefined column |

### Repository Pattern

```csharp
public interface IRepository<T> where T : class
{
    Task<T?> GetByIdAsync<TId>(TId id, CancellationToken ct = default);
    Task<IReadOnlyList<T>> GetAllAsync(CancellationToken ct = default);
    Task<IReadOnlyList<T>> FindAsync(ISpecification<T> spec, CancellationToken ct = default);
    Task<PageResult<T>> PaginateAsync(ISpecification<T> spec, int page, int pageSize, CancellationToken ct = default);
    Task<int> CountAsync(ISpecification<T> spec, CancellationToken ct = default);
}
```

### Transactions

```csharp
await db.TransactionAsync(async tx =>
{
    await tx.ExecuteAsync("UPDATE accounts SET balance = balance - 100 WHERE id = 1");
    await tx.ExecuteAsync("UPDATE accounts SET balance = balance + 100 WHERE id = 2");
});
```

### Typed Results

```csharp
DatabaseResult<User> result = await DatabaseResult<User>.Success(user, duration);
if (result.IsSuccess) { /* use result.Data */ }

PageResult<Order> page = new()
{
    Items = orders,
    TotalCount = 100,
    Page = 1,
    PageSize = 20,
};
```

---

## Architecture

```
Hellnet.Database
├── Abstractions/          ← Pure contracts (no Npgsql dependency)
│   ├── IDatabaseExecutor       Query, Execute, Scalar
│   ├── IDatabaseTransaction    Begin/Commit/Rollback
│   ├── IRepository<T>          Generic repository
│   ├── ISpecification<T>       Query specification
│   ├── DatabaseResult<T>       Typed result wrapper
│   └── PageResult<T>           Paginated result
├── Configuration/         ← Env-first (HELLNET_DATABASE_*)
│   ├── HellnetDatabaseOptions   Individual host/port/user/pass
│   └── DatabaseEnvBinder        Reads env vars, builds connection string
├── PostgreSql/            ← Npgsql implementation
│   ├── PostgresConnectionFactory  NpgsqlDataSource wrapper
│   ├── NpgsqlExecutor             Dapper-based executor
│   ├── NpgsqlTransaction          Transaction support
│   └── PostgresRepository<T>      Repository implementation
└── Resilience/            ← Polly retry with error discrimination
    └── DatabaseRetryPolicy
```

### Package structure (future)

```
Hellnet.Database.Abstractions      → Contracts only (no Npgsql)
Hellnet.Database.PostgreSql        → PostgreSQL implementation
Hellnet.Database.Resilience        → Polly retry policies
Hellnet.Database.SqlServer         → (future)
Hellnet.Database.MySql             → (future)
Hellnet.Database.Testing           → (future)
```

---

## Observability

This library provides **infrastructure only**. Observability (metrics, tracing, health checks, logging) is delegated to [`Hellnet.Observability`](https://github.com/guilhermelinosp/hellnet-dep-observability).

To instrument database calls:
- Use OpenTelemetry's `Npgsql` instrumentation to capture query duration, errors, and connection pool metrics
- [`Hellnet.Observability`](https://github.com/guilhermelinosp/hellnet-dep-observability) handles OTel setup automatically

---

## Configuration precedence

```
Environment Variables  ← highest priority
IConfiguration
HellnetDatabaseOptions
Default values         ← lowest priority
```

---

## License

Apache 2.0 — see [LICENSE](LICENSE).

## Related repos

| Repo | Purpose |
|------|---------|
| [`hellnet-dep-kafka`](https://github.com/guilhermelinosp/hellnet-dep-kafka) | Kafka pub/sub library |
| [`hellnet-dep-observability`](https://github.com/guilhermelinosp/hellnet-dep-observability) | OpenTelemetry + structured logging |
| [`hellnet-dep-cache`](https://github.com/guilhermelinosp/hellnet-dep-cache) | Multi-layer cache (Memory → Valkey) |
| [`hellnet-dep-schema`](https://github.com/guilhermelinosp/hellnet-dep-schema) | Schema registry management |
