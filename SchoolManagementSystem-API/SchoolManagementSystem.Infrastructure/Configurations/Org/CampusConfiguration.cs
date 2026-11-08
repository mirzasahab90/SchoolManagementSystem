using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoolManagementSystem.Domain.Entities.Org;

namespace SchoolManagementSystem.Infrastructure.Configurations.Org;

public class CampusConfiguration : IEntityTypeConfiguration<Campus>
{
    public void Configure(EntityTypeBuilder<Campus> builder)
    {
        builder.ToTable("Campuses");
        builder.Property(x => x.Code).HasMaxLength(30).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(150).IsRequired();
        builder.Property(x => x.Address).HasMaxLength(500);
        builder.Property(x => x.City).HasMaxLength(100);
        builder.Property(x => x.State).HasMaxLength(100);
        builder.Property(x => x.Country).HasMaxLength(100);
        builder.Property(x => x.PostalCode).HasMaxLength(20);
        builder.Property(x => x.Phone).HasMaxLength(50);
        builder.Property(x => x.Email).HasMaxLength(256);
        builder.Property(x => x.Website).HasMaxLength(256);
        builder.Property(x => x.InChargeName).HasMaxLength(150);
        builder.Property(x => x.InChargePhone).HasMaxLength(50);
        builder.Property(x => x.InChargeEmail).HasMaxLength(256);
        builder.Property(x => x.InChargeDesignation).HasMaxLength(100);
        builder.Property(x => x.Notes).HasMaxLength(2000);
        builder.HasIndex(x => x.Code).IsUnique().HasFilter("[IsDeleted] = 0");
        builder.HasOne(x => x.School).WithMany(x => x.Campuses).HasForeignKey(x => x.SchoolId).OnDelete(DeleteBehavior.Restrict);
    }
}
