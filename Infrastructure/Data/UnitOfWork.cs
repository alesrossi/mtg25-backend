using System.Collections;
using Core.Interfaces;
using Core.Models;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Data;

public class UnitOfWork : IUnitOfWork
{
    private readonly MainContext _context;
    private readonly ILogger<UnitOfWork> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private Hashtable? _repositories;

    public UnitOfWork(MainContext context, ILogger<UnitOfWork> logger, ILoggerFactory loggerFactory)
    {
        _context = context;
        _logger = logger;
        _loggerFactory = loggerFactory;
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    public IGenericRepository<TModel> Repository<TModel>() where TModel : BaseModel
    {
        _repositories ??= new Hashtable();

        var type = typeof(TModel).Name;
        _logger.LogTrace("Resolving repository for {Entity}", type);

        if (_repositories.ContainsKey(type)) return (IGenericRepository<TModel>)_repositories[type]!;
        
        var repositoryType = typeof(GenericRepository<>).MakeGenericType(typeof(TModel));
        var repositoryLogger = _loggerFactory.CreateLogger<GenericRepository<TModel>>();
        var repositoryInstance = Activator.CreateInstance(repositoryType, _context, repositoryLogger);

        _repositories.Add(type, repositoryInstance);

        return (IGenericRepository<TModel>) _repositories[type]!;
    }

    public async Task<int> Complete()
    {
        _logger.LogDebug("Saving unit of work changes");
        try
        {
            var changes = await _context.SaveChangesAsync();
            _logger.LogInformation("Unit of work saved {Changes} change(s)", changes);
            return changes;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unit of work save failed");
            throw;
        }
    }
}
