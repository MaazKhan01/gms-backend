using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Core.Interfaces.Repositories;
using DomainPersistence.Entities;

namespace Infrastructure.Database.Repositories;

public class GenericRepository<T> : IGenericRepository<T> where T : class
{
    protected readonly ApplicationDBContext _context;
    protected readonly DbSet<T> _dbSet;

    public GenericRepository(ApplicationDBContext context)
    {
        _context = context;
        _dbSet = context.Set<T>();
    }

    public async Task<T> GetByIdAsync(int id, CancellationToken ct = default)
        => await _dbSet.FindAsync(new object[] { id }, ct);

    public async Task<T> GetByIdAsync(int id, Expression<Func<T, object>> include, CancellationToken ct = default)
        => await _dbSet.Include(include).FirstOrDefaultAsync(x => EF.Property<int>(x, "Id") == id, ct);

    public async Task<T> GetByPublicIdAsync(Guid publicId, CancellationToken ct = default)
        => await _dbSet.FirstOrDefaultAsync(x => EF.Property<Guid>(x, "PublicId") == publicId, ct);

    public async Task<T> GetByPublicIdAsync(Guid publicId, Expression<Func<T, object>> include, CancellationToken ct = default)
        => await _dbSet.Include(include).FirstOrDefaultAsync(x => EF.Property<Guid>(x, "PublicId") == publicId, ct);

    public async Task<IEnumerable<T>> GetAllAsync(CancellationToken ct = default)
        => await _dbSet.ToListAsync(ct);

    public async Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default)
        => await _dbSet.Where(predicate).ToListAsync(ct);

    public async Task<T> FindFirstOrDefaultAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default)
        => await _dbSet.FirstOrDefaultAsync(predicate, ct);

    public IQueryable<T> Query() => _dbSet.AsQueryable();

    public IQueryable<T> QueryNoTracking() => _dbSet.AsNoTracking();

    public async Task<int> CountAsync(Expression<Func<T, bool>> predicate = null, CancellationToken ct = default)
        => predicate == null ? await _dbSet.CountAsync(ct) : await _dbSet.CountAsync(predicate, ct);

    public async Task<bool> AnyAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default)
        => await _dbSet.AnyAsync(predicate, ct);

    public async Task AddAsync(T entity, CancellationToken ct = default)
        => await _dbSet.AddAsync(entity, ct);

    public async Task AddRangeAsync(IEnumerable<T> entities, CancellationToken ct = default)
        => await _dbSet.AddRangeAsync(entities, ct);

    public void Update(T entity) => _dbSet.Update(entity);

    public void UpdateRange(IEnumerable<T> entities) => _dbSet.UpdateRange(entities);

    public void Remove(T entity) => _dbSet.Remove(entity);

    public void RemoveRange(IEnumerable<T> entities) => _dbSet.RemoveRange(entities);
}
