using System;
using DAL.Data;
using Microsoft.Extensions.Configuration;

namespace DAL
{
    public static class DbContextSingletone
    {
        private static readonly IConfiguration Configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json")
            .Build();

        private static Lazy<TelegramDbContext> _context = new Lazy<TelegramDbContext>(() =>
            new TelegramDbContext("Server=(localdb)\\mssqllocaldb;Database=TelegramBotDatabase;Trusted_Connection=True;"));//Configuration.GetSection("TelegramBotToken").Value));

        public static TelegramDbContext GetContext() =>
            new TelegramDbContext(
                "Server=(localdb)\\mssqllocaldb;Database=TelegramBotDatabase;Trusted_Connection=True;"); //_context.Value;
    }
}
