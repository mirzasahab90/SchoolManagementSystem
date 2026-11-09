using SchoolManagementSystem.Infrastructure;

namespace SchoolManagementSystem.Application.Repository;

public class SectionRepository(SchoolManagementSystemDbContext dbContext) : BaseRepository(dbContext)
{
}
