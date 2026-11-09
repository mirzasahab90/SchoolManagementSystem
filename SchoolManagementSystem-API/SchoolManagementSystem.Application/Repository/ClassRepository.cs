using SchoolManagementSystem.Infrastructure;

namespace SchoolManagementSystem.Application.Repository;

public class ClassRepository(SchoolManagementSystemDbContext dbContext) : BaseRepository(dbContext)
{
}
