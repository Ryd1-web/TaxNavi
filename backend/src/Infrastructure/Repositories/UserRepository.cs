namespace TaxNavi.Infrastructure.Repositories;

using Microsoft.EntityFrameworkCore;
using TaxNavi.Domain.Entities;
using TaxNavi.Domain.Interfaces;
using Data;

public class UserRepository : BaseRepository<User>, IUserRepository
{
    public UserRepository(TaxNaviDbContext context) : base(context)
    {
    }

    public async Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        var normalized = email.Trim().ToLowerInvariant();
        return await _context.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == normalized, cancellationToken);
    }
}
