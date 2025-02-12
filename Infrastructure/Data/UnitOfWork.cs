using System.Collections;
using Core.Interfaces;
using Core.Models;

namespace Infrastructure.Data;

public class UnitOfWork : IUnitOfWork
{
    private readonly MainContext _context;
    private Hashtable _repositories;

    public UnitOfWork(MainContext context)
    {
        _context = context;
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    public IGenericRepository<TModel> Repository<TModel>() where TModel : BaseModel
    {
        if (_repositories == null) _repositories = new Hashtable();

        var type = typeof(TModel).Name;

        if (!_repositories.ContainsKey(type))
        {
            var repositoryType = typeof(GenericRepository<>);
            var repositoryInstance =
                Activator.CreateInstance(repositoryType.MakeGenericType(typeof(TModel)), _context);

            _repositories.Add(type, repositoryInstance);
        }

        return (IGenericRepository<TModel>) _repositories[type];
    }

    public async Task<int> Complete()
    {
        return await _context.SaveChangesAsync();
    }
}