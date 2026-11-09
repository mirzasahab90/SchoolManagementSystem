using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using SchoolManagementSystem.Domain.Abstractions;

namespace SchoolManagementSystem.Infrastructure;

/// <summary>Lets EF tools create migrations without running the full API host.</summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<SchoolManagementSystemDbContext>
{
    public SchoolManagementSystemDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<SchoolManagementSystemDbContext>()
            .UseSqlServer("Server=DESKTOP-FC8TD88\\SQLEXPRESS;Database=SchoolManagementSystem;Trusted_Connection=True;TrustServerCertificate=True;")
            .Options;

        return new SchoolManagementSystemDbContext(options, new DesignTimeCurrentUser());
    }

    private sealed class DesignTimeCurrentUser : ICurrentUser
    {
        public int? UserId => null;
        public int? ActiveCampusId => null;
        public bool IsHq => false;
        public bool IsSuperAdmin => false;
        public IReadOnlySet<int> AssignedCampusIds { get; } = new HashSet<int>();
        public string? IpAddress => null;
        public string? UserAgent => null;
    }
}
