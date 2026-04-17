namespace Central.Engine.Data;

/// <summary>
/// Transaction scope for multi-entity operations.
/// Replaces XPO's UnitOfWork pattern with Npgsql transactions.
/// </summary>
public interface IUnitOfWork : IAsyncDisposable
{
    Task BeginAsync();
    Task CommitAsync();
    Task RollbackAsync();
}
