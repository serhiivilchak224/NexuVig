using Microsoft.EntityFrameworkCore;
using NexuVig.Server.Models;

namespace NexuVig.Server.Data;

public class NexuVigContext(DbContextOptions<NexuVigContext> opcoes) : DbContext(opcoes)
{
    public DbSet<Computador> Computadores => Set<Computador>();
    public DbSet<Medicao> Medicoes => Set<Medicao>();
}
