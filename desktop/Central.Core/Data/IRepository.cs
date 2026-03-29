using Central.Core.Models;

namespace Central.Core.Data;

/// <summary>
/// Generic async repository interface.
/// Each domain entity gets a typed repository.
/// </summary>
public interface IRepository<T> where T : EntityBase
{
    Task<List<T>> GetAllAsync();
    Task<T?> GetByIdAsync(int id);
    Task<int> InsertAsync(T entity);
    Task UpdateAsync(T entity);
    Task DeleteAsync(int id);
}
