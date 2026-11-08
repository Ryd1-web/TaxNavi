namespace TaxNavi.Domain.Interfaces;

using Entities;

public interface IUserRepository : IRepository<User>
{
    Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);
}
