using Core.Models;
using Core.Specifications;

namespace Core.Interfaces;

public interface IGenericRepository<T> where T : BaseModel
{
    IQueryable<T> Query { get; }
    Task<T?> GetByIdAsync(int id, bool tracking = true);
    Task<IReadOnlyList<T>?> ListAllAsync(bool tracking = true);
    Task<T?> GetEntityWithSpec(ISpecification<T> spec, bool tracking = true);
    Task<IReadOnlyList<T>?> ListAsync(ISpecification<T> spec, bool tracking = true);
    Task<int> CountAsync(ISpecification<T> spec, bool tracking = false);
    void Add(T entity);
    void Add(IList<T> entity);
    void Update(T entity);
    void Delete(T entity);
    Task<int> Delete(List<int> entitiesIds);
}
