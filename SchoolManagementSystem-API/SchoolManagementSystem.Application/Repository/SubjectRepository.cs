using SchoolManagementSystem.Infrastructure;

namespace SchoolManagementSystem.Application.Repository;

public class SubjectRepository(SchoolManagementSystemDbContext dbContext) : BaseRepository(dbContext)
{
}
