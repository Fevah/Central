using Npgsql;
using Central.Core.Data;

namespace Central.Data;

/// <summary>
/// Npgsql-backed unit of work for transactional operations.
/// </summary>
public class NpgsqlUnitOfWork : IUnitOfWork
{
    private readonly NpgsqlConnection _connection;
    private NpgsqlTransaction? _transaction;

    public NpgsqlUnitOfWork(string dsn)
    {
        _connection = new NpgsqlConnection(dsn);
    }

    public NpgsqlConnection Connection => _connection;
    public NpgsqlTransaction? Transaction => _transaction;

    public async Task BeginAsync()
    {
        await _connection.OpenAsync();
        _transaction = await _connection.BeginTransactionAsync();
    }

    public async Task CommitAsync()
    {
        if (_transaction != null) await _transaction.CommitAsync();
    }

    public async Task RollbackAsync()
    {
        if (_transaction != null) await _transaction.RollbackAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (_transaction != null) await _transaction.DisposeAsync();
        await _connection.DisposeAsync();
    }
}
