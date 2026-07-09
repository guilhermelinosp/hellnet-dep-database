using Hellnet.Database;
using Hellnet.Database.Abstractions;
using Hellnet.Database.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

// ── Config via env ou explícita ──────────────────────────────
// Para rodar: export HELLNET_DATABASE_NAME=mydb etc
// Ou usar options explícitas abaixo

var options = new HellnetDatabaseOptions
{
    Host = Environment.GetEnvironmentVariable("HELLNET_DATABASE_HOST") ?? "192.168.1.254",
    Port = 5432,
    Database = Environment.GetEnvironmentVariable("HELLNET_DATABASE_NAME") ?? "postgres",
    Username = Environment.GetEnvironmentVariable("HELLNET_DATABASE_USERNAME") ?? "postgres",
    Password = Environment.GetEnvironmentVariable("HELLNET_DATABASE_PASSWORD") ?? "",
    PoolMaxSize = 10,
    CommandTimeoutSeconds = 10,
};

// ── DI ───────────────────────────────────────────────────────
var services = new ServiceCollection();
services.AddLogging(b => b.AddConsole().SetMinimumLevel(LogLevel.Warning));
services.AddHellnetDatabase(options);
await using var sp = services.BuildServiceProvider();

var db = sp.GetRequiredService<IDatabaseExecutor>();

Console.WriteLine("🔌 Conectando ao PostgreSQL...");
Console.WriteLine($"   Host: {options.Host}:{options.Port}");
Console.WriteLine($"   DB:   {options.Database}");
Console.WriteLine();

// ── Health check ─────────────────────────────────────────────
try
{
    var version = await db.ExecuteScalarAsync<string>("SELECT version()");
    Console.WriteLine($"✅ Conectado!");
    Console.WriteLine($"   PostgreSQL: {version}");
    Console.WriteLine();
}
catch (Exception ex)
{
    Console.WriteLine($"❌ Falha na conexão: {ex.Message}");
    return;
}

// ── Query simples ────────────────────────────────────────────
Console.WriteLine("📊 Listando tabelas públicas...");
var tables = await db.QueryAsync<TableRow>(
    "SELECT table_name, table_schema FROM information_schema.tables WHERE table_schema = 'public' ORDER BY table_name");

foreach (var t in tables)
{
    Console.WriteLine($"   • {t.TableName}");
}

Console.WriteLine();
Console.WriteLine($"Total: {tables.Count} tabelas encontradas");

// ── Transação ────────────────────────────────────────────────
Console.WriteLine();
Console.WriteLine("🔄 Testando transação...");
var txService = sp.GetRequiredService<IDatabaseTransaction>();

try
{
    await txService.ExecuteAsync(async tx =>
    {
        var now = await tx.ExecuteScalarAsync<DateTime>("SELECT NOW()");
        Console.WriteLine($"   ✅ Transação OK. Horário do banco: {now:O}");
    });
}
catch (Exception ex)
{
    Console.WriteLine($"   ❌ Transação falhou: {ex.Message}");
}

// ── Repository ───────────────────────────────────────────────
Console.WriteLine();
Console.WriteLine("📦 Testando Repository pattern...");
var repo = ActivatorUtilities.CreateInstance<PostgresRepository<TableRow>>(sp);

var spec = new PublicTablesSpecification();
var allTables = await repo.FindAsync(spec);
Console.WriteLine($"   ✅ Repository OK. {allTables.Count} tabelas encontradas");

Console.WriteLine();
Console.WriteLine("✅ Teste concluído!");

// ── Tipos auxiliares ─────────────────────────────────────────
public sealed record TableRow
{
    public string TableName { get; init; } = "";
    public string TableSchema { get; init; } = "";
}

public sealed class PublicTablesSpecification : ISpecification<TableRow>
{
    public string Sql => "SELECT table_name as TableName, table_schema as TableSchema FROM information_schema.tables WHERE table_schema = 'public'";
    public object? Parameters => null;
    public string? OrderBy => null;
}

// ── Repository concreto (já existe na lib, mas aqui é explícito) ─
public sealed class PostgresRepository<T>(IDatabaseExecutor executor) : IRepository<T>
    where T : class
{
    public Task<T?> GetByIdAsync<TId>(TId id, CancellationToken ct = default)
        => throw new NotImplementedException();

    public Task<IReadOnlyList<T>> GetAllAsync(CancellationToken ct = default)
        => throw new NotImplementedException();

    public async Task<IReadOnlyList<T>> FindAsync(ISpecification<T> spec, CancellationToken ct = default)
        => await executor.QueryAsync<T>(spec.Sql, spec.Parameters, ct);

    public Task<PageResult<T>> PaginateAsync(ISpecification<T> spec, int page, int pageSize, CancellationToken ct = default)
        => throw new NotImplementedException();

    public Task<int> CountAsync(ISpecification<T> spec, CancellationToken ct = default)
        => throw new NotImplementedException();
}
