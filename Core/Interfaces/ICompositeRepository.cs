namespace Core.Interfaces;

public interface ICompositeRepository<T> where T : class
{
    IQueryable<T> Query { get; }
    void Add(T entity);
    void AddRange(IEnumerable<T> entities);
    void Update(T entity);
    void UpdateRange(IEnumerable<T> entities);
    void Remove(T entity);
    void RemoveRange(IEnumerable<T> entities);
}
