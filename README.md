# Hellnet.Database

PostgreSQL-first database infrastructure library for .NET. Env-first, modular, cloud-native.

```
Env vars → HellnetDatabaseOptions → NpgsqlDataSource → IDatabaseExecutor / IRepository<T>
```

[![NuGet](https://img.shields.io/nuget/v/Hellnet.Database)](https://www.nuget.org/packages/Hellnet.Database)
[![ADR](https://img.shields.io/badge/ADR-Architecture%20Decision%20Records-blue)](https://gist.github.com/guilhermelinosp/f8856a8731fa95becc0af87a75379659)

---

## Instalação

```bash
dotnet add package Hellnet.Database
```

## Configuração

### Via environment variables (recomendado)

```bash
export HELLNET_DATABASE_HOST=localhost
export HELLNET_DATABASE_PORT=5432
export HELLNET_DATABASE_NAME=mydb
export HELLNET_DATABASE_USERNAME=postgres
export HELLNET_DATABASE_PASSWORD=password
```

```csharp
builder.Services.AddHellnetDatabase();
```

### Via options explícitas

```csharp
builder.Services.AddHellnetDatabase(new HellnetDatabaseOptions
{
    Host = "pg.internal",
    Database = "orders",
    Username = "app",
    Password = "secret",
});
```

---

## Uso

### IDatabaseExecutor — SQL puro com Dapper

```csharp
public class OrderService
{
    private readonly IDatabaseExecutor _db;

    public OrderService(IDatabaseExecutor db) => _db = db;

    // Query
    public async Task<IReadOnlyList<Order>> GetPendingAsync()
        => await _db.QueryAsync<Order>(
            "SELECT * FROM orders WHERE status = @Status",
            new { Status = "pending" });

    // Single row
    public async Task<Order?> GetByIdAsync(int id)
        => await _db.QueryFirstOrDefaultAsync<Order>(
            "SELECT * FROM orders WHERE id = @Id", new { Id = id });

    // Execute (insert/update/delete)
    public async Task<int> UpdateStatusAsync(int id, string status)
        => await _db.ExecuteAsync(
            "UPDATE orders SET status = @Status WHERE id = @Id",
            new { Id = id, Status = status });

    // Scalar
    public async Task<int> CountAsync()
        => await _db.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM orders");
}
```

### Transações

```csharp
await db.TransactionAsync(async tx =>
{
    await tx.ExecuteAsync("UPDATE accounts SET balance = balance - 100 WHERE id = 1");
    await tx.ExecuteAsync("UPDATE accounts SET balance = balance + 100 WHERE id = 2");
});
// Commit automático. Se exception → rollback automático.
```

### Repository Pattern

```csharp
public class ProductService
{
    private readonly IRepository<Product> _products;

    public ProductService(IRepository<Product> products) => _products = products;

    public async Task<Product?> GetAsync(int id) => await _products.GetByIdAsync(id);

    public async Task<IReadOnlyList<Product>> SearchAsync(ProductSpec spec)
        => await _products.FindAsync(spec);
}

public sealed class ProductSpec(string term) : ISpecification<Product>
{
    public string Sql => "SELECT * FROM products WHERE name ILIKE @Term";
    public object? Parameters => new { Term = $"%{term}%" };
    public string? OrderBy => null;
}
```

### Resultados tipados

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

## Variáveis de Ambiente

| Variável | Obrigatório | Padrão | Descrição |
|----------|-------------|--------|-----------|
| `HELLNET_DATABASE_HOST` | ❌ | `localhost` | Host do PostgreSQL |
| `HELLNET_DATABASE_PORT` | ❌ | `5432` | Porta |
| `HELLNET_DATABASE_NAME` | ✅ | — | Nome do banco |
| `HELLNET_DATABASE_USERNAME` | ✅ | — | Usuário |
| `HELLNET_DATABASE_PASSWORD` | ✅ | — | Senha |
| `HELLNET_DATABASE_POOL_MIN_SIZE` | ❌ | `10` | Pool mínimo |
| `HELLNET_DATABASE_POOL_MAX_SIZE` | ❌ | `100` | Pool máximo |
| `HELLNET_DATABASE_COMMAND_TIMEOUT_SECONDS` | ❌ | `30` | Timeout de comandos |
| `HELLNET_DATABASE_RETRY_ENABLED` | ❌ | `true` | Habilitar retry |
| `HELLNET_DATABASE_RETRY_MAX_COUNT` | ❌ | `3` | Máximo de tentativas |
| `HELLNET_DATABASE_RETRY_BASE_DELAY_MS` | ❌ | `100` | Delay base do backoff |

---

## Resiliência (Polly)

Retry automático com backoff exponencial. Erros permanentes **não** são retentados:

| SQL State | Erro | Motivo |
|-----------|------|--------|
| `42601` | syntax_error | Bug no código |
| `23505` | unique_violation | Dado duplicado |
| `23503` | foreign_key_violation | Referência inválida |
| `42501` | insufficient_privilege | Permissão negada |
| `42P01` | undefined_table | Tabela não existe |
| `42703` | undefined_column | Coluna não existe |

Desabilitar por env:
```bash
export HELLNET_DATABASE_RETRY_ENABLED=false
```

---

## Diagrama da Arquitetura

```
Hellnet.Database
├── Abstractions
│   ├── IDatabaseExecutor       ← QueryAsync, ExecuteAsync, ScalarAsync
│   ├── IDatabaseTransaction    ← Begin/Commit/Rollback
│   ├── IDatabaseConnectionFactory ← Factory para conexões
│   ├── IRepository<T>          ← CRUD genérico
│   ├── ISpecification<T>       ← Filtros reutilizáveis
│   ├── DatabaseResult<T>       ← Resultado tipado
│   └── PageResult<T>           ← Resultado paginado
├── Configuration
│   ├── HellnetDatabaseOptions  ← Options imutáveis (init)
│   └── DatabaseEnvBinder       ← Leitura de env vars
├── PostgreSql
│   ├── PostgresConnectionFactory  ← NpgsqlDataSource
│   ├── NpgsqlExecutor             ← Dapper
│   ├── NpgsqlTransaction          ← Transação
│   └── PostgresRepository<T>      ← Repository implementation
└── Resilience
    └── DatabaseRetryPolicy     ← Polly + SQL state discrimination
```

---

## Decisões Técnicas (ADR)

Todas as decisões arquiteturais estão documentadas no [ADR - Architecture Decision Records](https://gist.github.com/guilhermelinosp/f8856a8731fa95becc0af87a75379659).

| ADR | Decisão |
|-----|---------|
| ADR-0001 | Configuração env-first com campos individuais |
| ADR-0002 | Abstrações agnósticas a provider |
| ADR-0003 | NpgsqlDataSource como gerenciador de conexões |
| ADR-0004 | Transação com escopo e rollback automático |
| ADR-0005 | Resiliência com Polly + discriminação de erros |
| ADR-0006 | Repository pattern sem ORM |
| ADR-0007 | Zero observabilidade (delegado ao Hellnet.Observability) |
| ADR-0008 | Dapper como mapper padrão |

---

## Observabilidade

`Hellnet.Database` não possui instrumentação própria. Use os pacotes OpenTelemetry padrão:

```csharp
builder.Services.AddOpenTelemetry()
    .WithTracing(t => t.AddNpgsql())
    .WithMetrics(m => m.AddNpgsqlInstrumentation());
```

Health checks e logging são delegados ao [`Hellnet.Observability`](https://github.com/guilhermelinosp/hellnet-dep-observability).

---

## Repositórios Relacionados

| Repo | Propósito |
|------|-----------|
| [`hellnet-dep-kafka`](https://github.com/guilhermelinosp/hellnet-dep-kafka) | Kafka pub/sub |
| [`hellnet-dep-observability`](https://github.com/guilhermelinosp/hellnet-dep-observability) | OpenTelemetry + logging |
| [`hellnet-dep-cache`](https://github.com/guilhermelinosp/hellnet-dep-cache) | Cache multi-layer |
| [`hellnet-dep-schema`](https://github.com/guilhermelinosp/hellnet-dep-schema) | Schema registry management |

---

## Licença

Apache 2.0 — veja [LICENSE](LICENSE).
