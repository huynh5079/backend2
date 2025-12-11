using DataLayer.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using System;
using System.IO;

namespace DataLayer
{
    public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<TpeduContext>
    {
        public TpeduContext CreateDbContext(string[] args)
        {
            // Lấy connection string từ appsettings.json hoặc User Secrets
            var basePath = Path.Combine(Directory.GetCurrentDirectory(), "..", "TPEdu_API");
            
            var configuration = new ConfigurationBuilder()
                .SetBasePath(basePath)
                .AddJsonFile("appsettings.json", optional: false)
                .AddJsonFile("appsettings.Development.json", optional: true)
                .AddUserSecrets<DesignTimeDbContextFactory>(optional: true)
                .AddEnvironmentVariables()
                .Build();

            var connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found. Please configure it in appsettings.json or User Secrets.");

            var optionsBuilder = new DbContextOptionsBuilder<TpeduContext>();
            optionsBuilder.UseSqlServer(connectionString);

            return new TpeduContext(optionsBuilder.Options);
        }
    }
}

