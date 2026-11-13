using System.Data;
using System.Data.Common;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Careflow.Application;
namespace Careflow.Infrastructure;

/// <summary>Provider-independent ADO.NET boundary. The official Microsoft.Data.SqlClient
/// package is a deployment dependency; no connection is opened in Demo mode.</summary>
public sealed class SqlDatabase(string connectionString, string providerFactoryType = "Microsoft.Data.SqlClient.SqlClientFactory, Microsoft.Data.SqlClient")
{
    private readonly DbProviderFactory factory = CreateFactory(providerFactoryType);
    private static DbProviderFactory CreateFactory(string type)
    {
        DbProviderFactories.RegisterFactory("Careflow.SqlServer", type);
        return DbProviderFactories.GetFactory("Careflow.SqlServer");
    }
    public async Task<DbConnection> OpenAsync(CancellationToken ct)
    {
        var connection = factory.CreateConnection() ?? throw new InvalidOperationException("SQL provider did not create a connection.");
        connection.ConnectionString = connectionString;
        await connection.OpenAsync(ct);
        return connection;
    }
    public async Task<bool> Ready(CancellationToken ct)
    {
        try { await using var c = await OpenAsync(ct); await using var cmd = c.CreateCommand(); cmd.CommandText = "SELECT 1"; cmd.CommandTimeout = 5; return (await cmd.ExecuteScalarAsync(ct)) is 1; }
        catch { return false; }
    }
    public async Task MigrateAsync(string directory, CancellationToken ct)
    {
        await using var connection = await OpenAsync(ct);
        await using var bootstrap = connection.CreateCommand();
        bootstrap.CommandText = "IF OBJECT_ID('dbo.SchemaMigrations','U') IS NULL CREATE TABLE dbo.SchemaMigrations (Version varchar(80) PRIMARY KEY, Sha256 char(64) NOT NULL, AppliedAtUtc datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME());";
        await bootstrap.ExecuteNonQueryAsync(ct);
        foreach (var file in Directory.GetFiles(directory, "*.sql").OrderBy(x => Path.GetFileName(x), StringComparer.Ordinal))
        {
            var version = Path.GetFileNameWithoutExtension(file); var text = await File.ReadAllTextAsync(file, ct);
            var hash = Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(text))).ToLowerInvariant();
            await using var check = connection.CreateCommand(); check.CommandText = "SELECT Sha256 FROM dbo.SchemaMigrations WHERE Version=@v"; check.Parameters.Add(Parameter(check, "v", version));
            var existing = await check.ExecuteScalarAsync(ct);
            if (existing is string old) { if (old != hash) throw new InvalidOperationException($"Applied migration {version} was modified. Add a new migration instead."); continue; }
            await using var tx = (DbTransaction)await connection.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            try
            {
                foreach (var batch in Regex.Split(text, @"^\s*GO\s*;?\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase).Where(x => !string.IsNullOrWhiteSpace(x)))
                {
                    await using var cmd = connection.CreateCommand(); cmd.Transaction = tx; cmd.CommandTimeout = 120; cmd.CommandText = batch;
                    await cmd.ExecuteNonQueryAsync(ct);
                }
                await using var record = connection.CreateCommand(); record.Transaction = tx; record.CommandText = "INSERT INTO dbo.SchemaMigrations(Version,Sha256) VALUES(@v,@h)";
                record.Parameters.Add(Parameter(record, "v", version)); record.Parameters.Add(Parameter(record, "h", hash)); await record.ExecuteNonQueryAsync(ct);
                await tx.CommitAsync(ct);
            }
            catch { await tx.RollbackAsync(ct); throw; }
        }
    }
    public static DbParameter Parameter(DbCommand command, string name, object? value)
    {
        var p = command.CreateParameter(); p.ParameterName = "@" + name; p.Value = value is DateOnly d ? d.ToDateTime(TimeOnly.MinValue) : value ?? DBNull.Value; return p;
    }
}
public sealed class SqlUnitOfWork(SqlDatabase database) : IUnitOfWork
{
    public async Task<T> ExecuteAsync<T>(Scope scope, Func<IHospitalStore, Task<T>> action, CancellationToken ct)
    {
        await using var connection = await database.OpenAsync(ct);
        await using var transaction = (DbTransaction)await connection.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        try { var result = await action(new SqlHospitalStore(connection, transaction, scope)); await transaction.CommitAsync(ct); return result; }
        catch { await transaction.RollbackAsync(ct); throw; }
    }
}
