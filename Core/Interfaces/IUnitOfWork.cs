using Core.Models;

namespace Core.Interfaces;

public interface IUnitOfWork : IDisposable
{
    IGenericRepository<TEntity> Repository<TEntity>() where TEntity : BaseModel;
    ICompositeRepository<TEntity> CompositeRepository<TEntity>() where TEntity : class;
    Task<int> Complete(CancellationToken cancellationToken = default);
}