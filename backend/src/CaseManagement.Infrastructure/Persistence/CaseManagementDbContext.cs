using System.Reflection;
using CaseManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CaseManagement.Infrastructure.Persistence;

public class CaseManagementDbContext(DbContextOptions<CaseManagementDbContext> options)
    : DbContext(options)
{
    public DbSet<Case> Cases => Set<Case>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
        base.OnModelCreating(modelBuilder);
    }
}
