using Core.Models;
using Core.Interfaces;
using Core.Specifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Data;

public class GenericRepository<T> : IGenericRepository<T> where T : BaseModel
{
    private readonly MainContext _context;
    private readonly ILogger<GenericRepository<T>> _logger;

    public GenericRepository(MainContext context, ILogger<GenericRepository<T>> logger)
    {
        _context = context;
        this._logger = logger;
    }
        
    public async Task<T?> GetByIdAsync(int id, bool tracking = true)
    {
        _logger.LogTrace("Fetching {Entity} by id {Id} tracking={Tracking}", typeof(T).Name, id, tracking);

        var query = tracking
            ? _context.Set<T>().AsQueryable()
            : _context.Set<T>().AsNoTracking();

        return await query.FirstOrDefaultAsync(e => e.Id == id);
    }


    public async Task<IReadOnlyList<T>?> ListAllAsync(bool tracking = true)
    {
        _logger.LogTrace("Listing all {Entity} records tracking={Tracking}", typeof(T).Name, tracking);

        var query = tracking
            ? _context.Set<T>().AsQueryable()
            : _context.Set<T>().AsNoTracking();

        return await query.ToListAsync();
    }

    public async Task<T?> GetEntityWithSpec(ISpecification<T> spec, bool tracking = true)
    {
        _logger.LogTrace("Fetching {Entity} with spec {Spec} tracking={Tracking}", typeof(T).Name, GetSpecName(spec), tracking);
        return await ApplySpecification(spec, tracking).FirstOrDefaultAsync();
    }

    public async Task<T?> GetEntity(ISpecification<T> spec, bool tracking = true)
    {
        _logger.LogTrace("Fetching single {Entity} with spec {Spec} tracking={Tracking}", typeof(T).Name, GetSpecName(spec), tracking);
        return await ApplySpecification(spec, tracking).FirstOrDefaultAsync();
    }

    public async Task<IReadOnlyList<T>?> ListAsync(ISpecification<T> spec, bool tracking = true)
    {
        _logger.LogTrace("Listing {Entity} with spec {Spec} tracking={Tracking}", typeof(T).Name, GetSpecName(spec), tracking);
        return await ApplySpecification(spec, tracking).ToListAsync();
    }

    public async Task<int> CountAsync(ISpecification<T> spec, bool tracking = false)
    {
        _logger.LogTrace("Counting {Entity} with spec {Spec}", typeof(T).Name, GetSpecName(spec));
        return await ApplySpecification(spec, tracking).CountAsync();
    }

    public void Add(T entity)
    {
        _logger.LogDebug("Adding {Entity} entity", typeof(T).Name);
        _context.Set<T>().Add(entity);
    }
        
    public void Add(IList<T> listEntity)
    {
        _logger.LogDebug("Adding {Entity} list count={Count}", typeof(T).Name, listEntity.Count);
        _context.Set<T>().AddRange(listEntity);
    }

    public void Update(T entity)
    {
        _logger.LogDebug("Updating {Entity} entity {Id}", typeof(T).Name, entity.Id);
        _context.Set<T>().Attach(entity);
        _context.Entry(entity).State = EntityState.Modified;
    }

    public void Delete(T entity)
    {
        _logger.LogDebug("Deleting {Entity} entity {Id}", typeof(T).Name, entity.Id);
        _context.Set<T>().Remove(entity);
    }
        
    public async Task<int> Delete(List<int> entitiesIds)
    {
        _logger.LogDebug("Deleting {Entity} entities count={Count}", typeof(T).Name, entitiesIds.Count);
        var entitiesToBeDeleted = await _context.Set<T>().ToListAsync();
        _context.Set<T>().RemoveRange(entitiesToBeDeleted.Where(x => entitiesIds.Contains(x.Id)));
        return entitiesIds.Count;
    }

    private IQueryable<T> ApplySpecification(ISpecification<T> spec, bool tracking)
    {
        var query = SpecificationEvaluator<T>.GetQuery(_context.Set<T>().AsQueryable(), spec);
        return tracking ? query : query.AsNoTracking();
    }

    private static string GetSpecName(ISpecification<T> spec)
    {
        return spec?.GetType().Name ?? "None";
    }
}