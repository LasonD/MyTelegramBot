using DAL.Entities;
using Microsoft.EntityFrameworkCore;

namespace DAL.Data
{

    public class TelegramDbContext : DbContext
    {
        public static string Connection { get; set; }
        public TelegramDbContext(string connection)
        {
            Connection = connection;
            Database.EnsureCreated();
        }
        public DbSet<TelegramUser> TelegramUsers { get; set; }
        public DbSet<TelegramMessage> Messages { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlServer(Connection);
        }
    }
}
