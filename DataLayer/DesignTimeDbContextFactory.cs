using DataLayer.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using System;

namespace DataLayer
{
    public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<TpeduContext>
    {
        public TpeduContext CreateDbContext(string[] args)
        {
            // Lấy connection string từ environment variable hoặc dùng default
            var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
                ?? "Server=tcp:tpedu1.database.windows.net,1433;Initial Catalog=TPEdu;Persist Security Info=False;User ID=sep490;Password=Capstone490;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=60;Command Timeout=60;";

            var optionsBuilder = new DbContextOptionsBuilder<TpeduContext>();
            optionsBuilder.UseSqlServer(connectionString);

            return new TpeduContext(optionsBuilder.Options);
        }
    }
}

