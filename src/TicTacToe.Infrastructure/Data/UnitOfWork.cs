using Microsoft.EntityFrameworkCore;
using TicTacToe.Domain.Contracts;

namespace TicTacToe.Infrastructure.Data;

public abstract class UnitOfWork : IUnitOfWork, IDisposable
{
    private readonly DbContext _dbContext;

    protected UnitOfWork(DbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public void Save()
    {
        _dbContext.SaveChanges();
    }

    public async Task SaveAsync()
    {
        await _dbContext.SaveChangesAsync();
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        GC.SuppressFinalize(this);
    }
}
