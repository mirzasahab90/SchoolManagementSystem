using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoolManagementSystem.Domain.Entities.Identity;

namespace SchoolManagementSystem.Infrastructure.Configurations.Identity;

public class RoleModulePermissionConfiguration : IEntityTypeConfiguration<RoleModulePermission>
{
    public void Configure(EntityTypeBuilder<RoleModulePermission> builder)
    {
        builder.ToTable("RoleModulePermissions");
        builder.HasKey(x => new { x.RoleId, x.ModuleId, x.PermissionId });
        builder.HasOne(x => x.Role).WithMany(x => x.Permissions).HasForeignKey(x => x.RoleId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.Module).WithMany().HasForeignKey(x => x.ModuleId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Permission).WithMany().HasForeignKey(x => x.PermissionId).OnDelete(DeleteBehavior.Restrict);
    }
}
