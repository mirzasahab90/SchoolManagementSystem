using System.Linq.Expressions;
using BindingFlags = System.Reflection.BindingFlags;
using MethodInfo = System.Reflection.MethodInfo;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using SchoolManagementSystem.Domain.Abstractions;
using SchoolManagementSystem.Domain.Common;
using SchoolManagementSystem.Domain.Entities.Academics;
using SchoolManagementSystem.Domain.Entities.Identity;
using SchoolManagementSystem.Domain.Entities.Org;

namespace SchoolManagementSystem.Infrastructure;

/// <summary>
/// Single EF Core context. Mappings live in Configurations/.
/// SaveChanges stamps audit columns and turns deletes into soft deletes.
/// </summary>
public class SchoolManagementSystemDbContext(DbContextOptions<SchoolManagementSystemDbContext> options, ICurrentUser currentUser) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<Module> Modules => Set<Module>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<RoleModulePermission> RoleModulePermissions => Set<RoleModulePermission>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<School> Schools => Set<School>();
    public DbSet<Campus> Campuses => Set<Campus>();
    public DbSet<UserCampus> UserCampuses => Set<UserCampus>();
    public DbSet<AcademicSession> AcademicSessions => Set<AcademicSession>();
    public DbSet<SchoolClass> Classes => Set<SchoolClass>();
    public DbSet<Subject> Subjects => Set<Subject>();
    public DbSet<ClassSubject> ClassSubjects => Set<ClassSubject>();
    public DbSet<Section> Sections => Set<Section>();

    /// <summary>Campuses the current request may read; evaluated per query by the EF filters below.</summary>
    private IReadOnlyCollection<int> CampusFilterIds => currentUser.DataCampusIds ?? [];

    private bool CampusFilterDisabled => currentUser.UserId == null || currentUser.DataCampusIds == null;

    private static readonly MethodInfo ConfigureFiltersMethod =
        typeof(SchoolManagementSystemDbContext).GetMethod(nameof(ConfigureFilters), BindingFlags.NonPublic | BindingFlags.Instance)!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SchoolManagementSystemDbContext).Assembly);

        foreach (var entityType in modelBuilder.Model.GetEntityTypes().Where(t => t.BaseType == null).ToList())
        {
            ConfigureFiltersMethod.MakeGenericMethod(entityType.ClrType).Invoke(this, [modelBuilder]);
        }
    }

    /// <summary>Soft delete for auditable tables; campus isolation for <see cref="ICampusScoped"/> tables.</summary>
    private void ConfigureFilters<TEntity>(ModelBuilder modelBuilder) where TEntity : class
    {
        var softDelete = typeof(AuditableEntity).IsAssignableFrom(typeof(TEntity));
        var campusScoped = typeof(ICampusScoped).IsAssignableFrom(typeof(TEntity));
        var campusOwnable = typeof(ICampusOwnable).IsAssignableFrom(typeof(TEntity));

        if (campusOwnable)
        {
            // HQ rows (null campus) are visible everywhere; campus rows only within the request's campuses.
            Expression<Func<TEntity, bool>> owned = softDelete
                ? e => !EF.Property<bool>(e, nameof(AuditableEntity.IsDeleted))
                       && (EF.Property<int?>(e, nameof(ICampusOwnable.CampusId)) == null
                           || CampusFilterDisabled
                           || CampusFilterIds.Contains(EF.Property<int?>(e, nameof(ICampusOwnable.CampusId))!.Value))
                : e => EF.Property<int?>(e, nameof(ICampusOwnable.CampusId)) == null
                       || CampusFilterDisabled
                       || CampusFilterIds.Contains(EF.Property<int?>(e, nameof(ICampusOwnable.CampusId))!.Value);
            modelBuilder.Entity<TEntity>().HasQueryFilter(owned);
            return;
        }

        Expression<Func<TEntity, bool>>? filter = (softDelete, campusScoped) switch
        {
            (true, true) => e => !EF.Property<bool>(e, nameof(AuditableEntity.IsDeleted))
                                 && (CampusFilterDisabled || CampusFilterIds.Contains(EF.Property<int>(e, nameof(ICampusScoped.CampusId)))),
            (true, false) => e => !EF.Property<bool>(e, nameof(AuditableEntity.IsDeleted)),
            (false, true) => e => CampusFilterDisabled || CampusFilterIds.Contains(EF.Property<int>(e, nameof(ICampusScoped.CampusId))),
            _ => null
        };

        if (filter != null)
        {
            modelBuilder.Entity<TEntity>().HasQueryFilter(filter);
        }
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var userId = currentUser.UserId;
        GuardCampusScopedChanges();

        foreach (var entry in ChangeTracker.Entries<AuditableEntity>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedOn = now;
                entry.Entity.CreatedBy = userId;
                entry.Entity.IsDeleted = false;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Entity.ModifiedOn = now;
                entry.Entity.ModifiedBy = userId;
            }
            else if (entry.State == EntityState.Deleted)
            {
                entry.State = EntityState.Modified;
                entry.Entity.IsDeleted = true;
                entry.Entity.ModifiedOn = now;
                entry.Entity.ModifiedBy = userId;
            }
        }

        return await base.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// HQ rows (null campus) may only be written by HQ. A campus user's new rows belong to their working campus.
    /// </summary>
    private void GuardCampusOwnableChanges()
    {
        foreach (var entry in ChangeTracker.Entries<ICampusOwnable>())
        {
            if (entry.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted))
            {
                continue;
            }

            if (entry.State == EntityState.Added && entry.Entity.CampusId == null && !currentUser.IsHq)
            {
                entry.Entity.CampusId = currentUser.ActiveCampusId
                    ?? throw new CampusAccessException("Select a campus.");
            }

            var owners = new List<int?> { entry.Entity.CampusId };
            if (entry.State == EntityState.Modified)
            {
                owners.Add((int?)entry.Property(nameof(ICampusOwnable.CampusId)).OriginalValue);
            }

            foreach (var owner in owners)
            {
                if (owner == null && !currentUser.IsHq)
                {
                    throw new CampusAccessException("Only HQ can change head-office items.");
                }

                if (owner is int campusId && !currentUser.CanAccessCampus(campusId))
                {
                    throw new CampusAccessException("You do not have access to this campus.");
                }
            }
        }
    }

    /// <summary>
    /// New campus data gets the working campus; nobody may write rows of a campus they cannot access.
    /// Skipped for background work (no signed-in user), e.g. seeding.
    /// </summary>
    private void GuardCampusScopedChanges()
    {
        if (currentUser.UserId == null)
        {
            return;
        }

        GuardCampusOwnableChanges();

        foreach (var entry in ChangeTracker.Entries<ICampusScoped>())
        {
            if (entry.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted))
            {
                continue;
            }

            if (entry.State == EntityState.Added && entry.Entity.CampusId == 0)
            {
                entry.Entity.CampusId = currentUser.ActiveCampusId
                    ?? throw new CampusAccessException("Select a working campus before adding campus records.");
            }

            if (!currentUser.CanAccessCampus(entry.Entity.CampusId))
            {
                throw new CampusAccessException("You do not have access to this campus.");
            }

            if (entry.State == EntityState.Modified
                && entry.Property(nameof(ICampusScoped.CampusId)).IsModified
                && entry.Property(nameof(ICampusScoped.CampusId)).OriginalValue is int original
                && !currentUser.CanAccessCampus(original))
            {
                throw new CampusAccessException("You do not have access to this campus.");
            }
        }
    }
}
