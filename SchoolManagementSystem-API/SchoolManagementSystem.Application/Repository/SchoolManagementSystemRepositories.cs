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
    private SessionRepository? _sessions;
    private SubjectRepository? _subjects;
    private ClassRepository? _classes;
    private SectionRepository? _sections;
    private StudentRepository? _students;
    private EnrollmentRepository? _enrollments;
    private GuardianRepository? _guardians;
    private SequenceRepository? _sequences;
    private VacancyRepository? _vacancies;
    private CandidateRepository? _candidates;
    private StaffRepository? _staff;
    private TeachingRepository? _teaching;
    private CalendarRepository? _calendar;
    private AttendanceRepository? _attendance;
    private AttendanceReportRepository? _attendanceReports;
    private StaffAttendanceRepository? _staffAttendance;
    private GradingRepository? _grading;
    private ExamRepository? _exams;
    private MarkRepository? _marks;
    private ResultRepository? _results;
    private FeeSetupRepository? _feeSetup;
    private ConcessionRepository? _concessions;
    private VoucherRepository? _vouchers;
    private FeeReportRepository? _feeReports;

    public AccessRepository Access => _access ??= new AccessRepository(dbContext);
    public UserRepository Users => _users ??= new UserRepository(dbContext);
    public TokenRepository Tokens => _tokens ??= new TokenRepository(dbContext);
    public CampusRepository Campuses => _campuses ??= new CampusRepository(dbContext);
    public RoleRepository Roles => _roles ??= new RoleRepository(dbContext);
    public SchoolRepository Schools => _schools ??= new SchoolRepository(dbContext);
    public SessionRepository Sessions => _sessions ??= new SessionRepository(dbContext);
    public SubjectRepository Subjects => _subjects ??= new SubjectRepository(dbContext);
    public ClassRepository Classes => _classes ??= new ClassRepository(dbContext);
    public SectionRepository Sections => _sections ??= new SectionRepository(dbContext);
    public StudentRepository Students => _students ??= new StudentRepository(dbContext);
    public EnrollmentRepository Enrollments => _enrollments ??= new EnrollmentRepository(dbContext);
    public GuardianRepository Guardians => _guardians ??= new GuardianRepository(dbContext);
    public SequenceRepository Sequences => _sequences ??= new SequenceRepository(dbContext);
    public VacancyRepository Vacancies => _vacancies ??= new VacancyRepository(dbContext);
    public CandidateRepository Candidates => _candidates ??= new CandidateRepository(dbContext);
    public StaffRepository Staff => _staff ??= new StaffRepository(dbContext);
    public TeachingRepository Teaching => _teaching ??= new TeachingRepository(dbContext);
    public CalendarRepository Calendar => _calendar ??= new CalendarRepository(dbContext);
    public AttendanceRepository Attendance => _attendance ??= new AttendanceRepository(dbContext);
    public AttendanceReportRepository AttendanceReports => _attendanceReports ??= new AttendanceReportRepository(dbContext);
    public StaffAttendanceRepository StaffAttendance => _staffAttendance ??= new StaffAttendanceRepository(dbContext);
    public GradingRepository Grading => _grading ??= new GradingRepository(dbContext);
    public ExamRepository Exams => _exams ??= new ExamRepository(dbContext);
    public MarkRepository Marks => _marks ??= new MarkRepository(dbContext);
    public ResultRepository Results => _results ??= new ResultRepository(dbContext);
    public FeeSetupRepository FeeSetup => _feeSetup ??= new FeeSetupRepository(dbContext);
    public ConcessionRepository Concessions => _concessions ??= new ConcessionRepository(dbContext);
    public VoucherRepository Vouchers => _vouchers ??= new VoucherRepository(dbContext);
    public FeeReportRepository FeeReports => _feeReports ??= new FeeReportRepository(dbContext);

    public Task<Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction> BeginTransactionAsync(CancellationToken ct = default) =>
        dbContext.Database.BeginTransactionAsync(ct);

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        dbContext.SaveChangesAsync(cancellationToken);
}
