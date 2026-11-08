using SchoolManagementSystem.Infrastructure;

namespace SchoolManagementSystem.Application.Repository;

public class SchoolManagementSystemRepositories(SchoolManagementSystemDbContext dbContext)
{
    private AccessRepository? _access;
    private UserRepository? _users;
    private TokenRepository? _tokens;
    private CampusRepository? _campuses;
    private RoleRepository? _roles;
    private SchoolRepository? _schools;

    public AccessRepository Access => _access ??= new AccessRepository(dbContext);
    public UserRepository Users => _users ??= new UserRepository(dbContext);
    public TokenRepository Tokens => _tokens ??= new TokenRepository(dbContext);
    public CampusRepository Campuses => _campuses ??= new CampusRepository(dbContext);
    public RoleRepository Roles => _roles ??= new RoleRepository(dbContext);
    public SchoolRepository Schools => _schools ??= new SchoolRepository(dbContext);

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        dbContext.SaveChangesAsync(cancellationToken);
}
