using Microsoft.EntityFrameworkCore;
using CryptoWallet.Models;

namespace CryptoWallet.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public DbSet<Transaction> Transactions { get; set; }
    }
}