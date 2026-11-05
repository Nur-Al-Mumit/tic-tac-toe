using Microsoft.EntityFrameworkCore;

namespace TicTacToe.Infrastructure.Persistence;

/// <summary>M0 placeholder context — becomes IdentityDbContext<ApplicationUser> in M1.</summary>
public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
}
