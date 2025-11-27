using System;
using System.Collections;
using Core.Interfaces;
using Core.Models;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Data;

public class UnitOfWork : IUnitOfWork
{
    private readonly MainContext _context;
    private readonly ILogger<UnitOfWork> logger;
    private readonly ILoggerFactory loggerFactory;
    private Hashtable? _repositories;

    public UnitOfWork(MainContext context, ILogger<UnitOfWork> logger, ILoggerFactory loggerFactory)
    {
        _context = context;
        this.logger = logger;
        this.loggerFactory = loggerFactory;
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    public IGenericRepository<TModel> Repository<TModel>() where TModel : BaseModel
    {
        _repositories ??= new Hashtable();

        var type = typeof(TModel).Name;
        logger.LogTrace("Resolving repository for {Entity}", type);

        if (!_repositories.ContainsKey(type))
        {
            var repositoryType = typeof(GenericRepository<>).MakeGenericType(typeof(TModel));
            var repositoryLogger = loggerFactory.CreateLogger<GenericRepository<TModel>>();
            var repositoryInstance = Activator.CreateInstance(repositoryType, _context, repositoryLogger);

            _repositories.Add(type, repositoryInstance);
        }

        return (IGenericRepository<TModel>) _repositories[type]!;
    }

    public async Task<int> Complete()
    {
        logger.LogDebug("Saving unit of work changes");
        try
        {
            var changes = await _context.SaveChangesAsync();
            logger.LogInformation("Unit of work saved {Changes} change(s)", changes);
            return changes;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unit of work save failed");
            throw;
        }
    }
}
