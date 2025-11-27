using Core.Models;
using Core.Interfaces;
using Core.Specifications;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Data
{
    public class GenericRepository<T> : IGenericRepository<T> where T : BaseModel
    {
        private readonly MainContext _context;
        public GenericRepository(MainContext context)
        {
            _context = context;
        }
        
        public async Task<T?> GetByIdAsync(int id, bool tracking = true)
        {
            var query = tracking
                ? _context.Set<T>().AsQueryable()
                : _context.Set<T>().AsNoTracking();

            return await query.FirstOrDefaultAsync(e => e.Id == id);
        }


        public async Task<IReadOnlyList<T>?> ListAllAsync(bool tracking = true)
        {
            var query = tracking
                ? _context.Set<T>().AsQueryable()
                : _context.Set<T>().AsNoTracking();

            return await query.ToListAsync();
        }

        public async Task<T?> GetEntityWithSpec(ISpecification<T> spec, bool tracking = true)
        {
            return await ApplySpecification(spec, tracking).FirstOrDefaultAsync();
        }

        public async Task<T?> GetEntity(ISpecification<T> spec, bool tracking = true)
        {
            return await ApplySpecification(spec, tracking).FirstOrDefaultAsync();
        }

        public async Task<IReadOnlyList<T>?> ListAsync(ISpecification<T> spec, bool tracking = true)
        {
            return await ApplySpecification(spec, tracking).ToListAsync();
        }

        public async Task<int> CountAsync(ISpecification<T> spec, bool tracking = false)
        {
            return await ApplySpecification(spec, tracking).CountAsync();
        }

        public void Add(T entity)
        {
            _context.Set<T>().Add(entity);
        }
        
        public void Add(IList<T> listEntity)
        {
            _context.Set<T>().AddRange(listEntity);
        }

        public void Update(T entity)
        {
            _context.Set<T>().Attach(entity);
            _context.Entry(entity).State = EntityState.Modified;
        }

        public void Delete(T entity)
        {
            _context.Set<T>().Remove(entity);
        }
        
        public async Task<int> Delete(List<int> entitiesIds)
        {
            var entitiesToBeDeleted = await _context.Set<T>().ToListAsync();
            _context.Set<T>().RemoveRange(entitiesToBeDeleted.Where(x => entitiesIds.Contains(x.Id)));
            return entitiesIds.Count;
        }

        private IQueryable<T> ApplySpecification(ISpecification<T> spec, bool tracking)
        {
            var query = SpecificationEvaluator<T>.GetQuery(_context.Set<T>().AsQueryable(), spec);
            return tracking ? query : query.AsNoTracking();
        }
    }
}
