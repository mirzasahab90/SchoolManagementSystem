using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoolManagementSystem.Domain.Entities.Org;

namespace SchoolManagementSystem.Infrastructure.Configurations.Org;

public class UserCampusConfiguration : IEntityTypeConfiguration<UserCampus>
{
    public void Configure(EntityTypeBuilder<UserCampus> builder)
    {
        builder.ToTable("UserCampuses");
        builder.HasKey(x => new { x.UserId, x.CampusId });
        builder.HasOne(x => x.User).WithMany(x => x.UserCampuses).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.Campus).WithMany(x => x.UserCampuses).HasForeignKey(x => x.CampusId).OnDelete(DeleteBehavior.Restrict);
    }
}
