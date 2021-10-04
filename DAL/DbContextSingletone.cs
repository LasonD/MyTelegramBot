using System;
using DAL.Data;
using Microsoft.Extensions.Configuration;

namespace DAL
{
    public static class DbContextSingletone
    {
        private const string ConnectionStringName = "TelegramBotDatabase";
        private static readonly object _locker = new object();
        private static string _connection;

        public static bool IsSet => _connection != null;

        public static void SetConnectionString(string connection)
        {
            lock (_locker)
            {
                if (_connection != null) throw new InvalidOperationException("Connection is already set");

                _connection = connection;
            }
        }

        private static readonly Lazy<TelegramDbContext> _context = new Lazy<TelegramDbContext>(() => new TelegramDbContext(_connection));//Configuration.GetSection("TelegramBotToken").Value));

        public static TelegramDbContext GetContext() => _context.Value;

        public static IConfiguration SetupDbContextString(this IConfiguration configuration)
        {
            _connection = configuration.GetConnectionString(ConnectionStringName);

            return configuration;
        }
    }
}
