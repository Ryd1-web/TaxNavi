namespace TaxNavi.Infrastructure.Repositories;

using Microsoft.EntityFrameworkCore;
using TaxNavi.Domain.Entities;
using TaxNavi.Domain.Interfaces;
using Data;

public class BaseRepository<T> : IRepository<T> where T : class
{
    protected readonly TaxNaviDbContext _context;

    public BaseRepository(TaxNaviDbContext context)
    {
        _context = context;
    }

    public async Task<T?> GetByIdAsync(Guid id)
    {
        return await _context.Set<T>().FindAsync(id);
    }

    public async Task<IEnumerable<T>> GetAllAsync()
    {
        return await _context.Set<T>().ToListAsync();
    }

    public async Task AddAsync(T entity)
    {
        await _context.Set<T>().AddAsync(entity);
        await SaveChangesAsync();
    }

    public async Task UpdateAsync(T entity)
    {
        _context.Set<T>().Update(entity);
        await SaveChangesAsync();
    }

    public async Task DeleteAsync(Guid id)
    {
        var entity = await GetByIdAsync(id);
        if (entity != null)
        {
            _context.Set<T>().Remove(entity);
            await SaveChangesAsync();
        }
    }

    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }
}

public class UserRepository : BaseRepository<User>, IUserRepository
{
    public UserRepository(TaxNaviDbContext context) : base(context)
    {
    }

    public async Task<User?> GetByEmailAsync(string email)
    {
        return await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
    }
}

public class TaxProfileRepository : BaseRepository<TaxProfile>, ITaxProfileRepository
{
    public TaxProfileRepository(TaxNaviDbContext context) : base(context)
    {
    }

    public async Task<IEnumerable<TaxProfile>> GetByUserIdAsync(Guid userId)
    {
        return await _context.TaxProfiles.Where(tp => tp.UserId == userId).ToListAsync();
    }
}

public class TaxReportRepository : BaseRepository<TaxReport>, ITaxReportRepository
{
    public TaxReportRepository(TaxNaviDbContext context) : base(context)
    {
    }

    public async Task<IEnumerable<TaxReport>> GetByUserIdAsync(Guid userId)
    {
        return await _context.TaxReports
            .Where(tr => tr.UserId == userId)
            .OrderByDescending(tr => tr.CreatedAt)
            .ToListAsync();
    }
}

public class PaymentRepository : BaseRepository<Payment>, IPaymentRepository
{
    public PaymentRepository(TaxNaviDbContext context) : base(context)
    {
    }

    public async Task<IEnumerable<Payment>> GetByUserIdAsync(Guid userId)
    {
        return await _context.Payments
            .Where(p => p.UserId == userId)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();
    }

    public async Task<Payment?> GetByReferenceAsync(string reference)
    {
        return await _context.Payments.FirstOrDefaultAsync(p => p.Reference == reference);
    }
}
