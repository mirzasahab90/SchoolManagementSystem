export type ApiResponse<T> = {
  succeeded: boolean;
  code: number;
  message?: string | null;
  data?: T | null;
};

export type MenuItem = {
  key: string;
  name: string;
  route?: string | null;
  icon?: string | null;
  displayOrder: number;
  children: MenuItem[];
};

export type CampusOption = {
  id: number;
  code: string;
  name: string;
  isPrimary: boolean;
};

export type CurrentUser = {
  id: number;
  firstName: string;
  lastName: string;
  fullName: string;
  email: string;
  isHq: boolean;
  isSuperAdmin: boolean;
  roles: string[];
  mustChangePassword: boolean;
  menu: MenuItem[];
  campuses: CampusOption[];
  activeCampusId?: number | null;
  schoolName?: string | null;
  schoolShortName?: string | null;
  permissions: Record<string, string[]>;
};

export type AuthResult = {
  tokens: {
    accessToken: string;
    accessExpiresUtc: string;
    refreshToken: string;
    refreshExpiresUtc: string;
  };
  user: CurrentUser;
};

export type Campus = {
  id: number;
  code: string;
  name: string;
  address?: string | null;
  city?: string | null;
  state?: string | null;
  country?: string | null;
  postalCode?: string | null;
  phone?: string | null;
  email?: string | null;
  website?: string | null;
  inChargeName?: string | null;
  inChargePhone?: string | null;
  inChargeEmail?: string | null;
  inChargeDesignation?: string | null;
  establishedOn?: string | null;
  studentCapacity?: number | null;
  notes?: string | null;
  isActive: boolean;
  adminUserCount: number;
  admins?: CampusAdminRef[];
};

export type SaveCampusRequest = {
  code: string;
  name: string;
  address?: string;
  city?: string;
  state?: string;
  country?: string;
  postalCode?: string;
  phone?: string;
  email?: string;
  website?: string;
  inChargeName?: string;
  inChargePhone?: string;
  inChargeEmail?: string;
  inChargeDesignation?: string;
  establishedOn?: string | null;
  studentCapacity?: number | null;
  notes?: string;
  isActive: boolean;
  admin?: NewCampusAdmin | null;
};

export type RoleRef = { id: number; name: string };

export type UserListItem = {
  id: number;
  firstName: string;
  lastName: string;
  fullName: string;
  email: string;
  phone?: string | null;
  isActive: boolean;
  isHq: boolean;
  lastLoginOn?: string | null;
  roles: RoleRef[];
  campusIds: number[];
};

export type SaveUserRequest = {
  firstName: string;
  lastName: string;
  email: string;
  phone?: string;
  isActive: boolean;
  isHq: boolean;
  roleIds: number[];
  campusIds: number[];
  password?: string;
  mustChangePassword?: boolean;
};

export type RoleListItem = {
  id: number;
  name: string;
  description?: string | null;
  isSystem: boolean;
  isActive: boolean;
  userCount: number;
  permissionCount: number;
  campusId?: number | null;
  campusCode?: string | null;
  hqAssignOnly: boolean;
};

export type ModulePermissionCell = {
  moduleKey: string;
  moduleName: string;
  view: boolean;
  add: boolean;
  edit: boolean;
  delete: boolean;
};

export type RoleDetail = {
  id: number;
  name: string;
  description?: string | null;
  isSystem: boolean;
  isActive: boolean;
  matrix: ModulePermissionCell[];
  campusId?: number | null;
  campusCode?: string | null;
  hqAssignOnly?: boolean;
};

export type KpiCard = {
  id: string;
  label: string;
  value: string;
  hint?: string | null;
  tone: string;
  href?: string | null;
};

export type QuickAction = {
  id: string;
  label: string;
  description: string;
  href: string;
};

export type Dashboard = {
  schoolName: string;
  schoolShortName?: string | null;
  activeCampusName?: string | null;
  kpis: KpiCard[];
  quickActions: QuickAction[];
};

/* ------------------------------------------------------------------ */
/* Ownership: campusId / campusCode are absent for Main Campus rows */
/* ------------------------------------------------------------------ */

export type Owned = {
  campusId?: number | null;
  campusCode?: string | null;
};

export type CampusAdminRef = { id: number; fullName: string; email: string };

export type NewCampusAdmin = { firstName: string; lastName: string; email: string; password: string };

/* ------------------------------------------------------------------ */
/* Academics                                                           */
/* ------------------------------------------------------------------ */

export type SessionStatus = "Planned" | "Active" | "Closed";
export type ClassStage = "PrePrimary" | "Primary" | "Middle" | "Secondary" | "HigherSecondary";
export type SubjectType = "Theory" | "Practical" | "Activity";
export type SectionShift = "Morning" | "Afternoon" | "Evening";

export const stageLabel: Record<ClassStage, string> = {
  PrePrimary: "Pre-primary",
  Primary: "Primary",
  Middle: "Middle",
  Secondary: "Secondary",
  HigherSecondary: "Higher secondary",
};

export const shiftLabel: Record<SectionShift, string> = {
  Morning: "Morning",
  Afternoon: "Afternoon",
  Evening: "Evening",
};

export type AcademicSession = Owned & {
  id: number;
  name: string;
  startDate: string;
  endDate: string;
  status: SessionStatus;
  isCurrent: boolean;
  sectionCount: number;
};

export type AcademicSessionList = { items: AcademicSession[]; effectiveCurrentId?: number | null };

export type SaveAcademicSessionRequest = {
  name: string;
  startDate: string;
  endDate: string;
  status: SessionStatus;
  campusId?: number | null;
};

export type Subject = Owned & {
  id: number;
  code: string;
  name: string;
  type: SubjectType;
  isActive: boolean;
  classCount: number;
};

export type SaveSubjectRequest = {
  code: string;
  name: string;
  type: SubjectType;
  isActive: boolean;
  campusId?: number | null;
};

export type SchoolClass = Owned & {
  id: number;
  code: string;
  name: string;
  stage: ClassStage;
  displayOrder: number;
  isActive: boolean;
  subjectCount: number;
  sectionCount: number;
};

export type ClassSubject = {
  subjectId: number;
  code: string;
  name: string;
  type: SubjectType;
  isOptional: boolean;
  displayOrder: number;
};

export type ClassDetail = { class: SchoolClass; subjects: ClassSubject[] };

export type SaveClassRequest = {
  code: string;
  name: string;
  stage: ClassStage;
  displayOrder: number;
  isActive: boolean;
  campusId?: number | null;
};

export type Section = {
  id: number;
  campusId: number;
  campusCode: string;
  campusName: string;
  academicSessionId: number;
  sessionName: string;
  classId: number;
  classCode: string;
  className: string;
  classOrder: number;
  name: string;
  shift: SectionShift;
  capacity?: number | null;
  room?: string | null;
  isActive: boolean;
  activeStudents?: number;
  classTeacherStaffId?: number | null;
  classTeacherName?: string | null;
};

export type SaveSectionRequest = {
  classId: number;
  academicSessionId?: number | null;
  name: string;
  shift: SectionShift;
  capacity?: number | null;
  room?: string | null;
  isActive: boolean;
  campusId?: number | null;
};

export type CopySectionsResult = { copied: number; skipped: number };

/* ------------------------------------------------------------------ */
/* Students & guardians                                                */
/* ------------------------------------------------------------------ */

export type Gender = "Male" | "Female" | "Other";
export type StudentStatus = "Active" | "Left";
export type GuardianRelation = "Father" | "Mother" | "Guardian" | "Other";
export type EnrollmentStatus = "Active" | "Left";
export type EnrollmentAction = "Admitted" | "SectionChanged" | "Left" | "Rejoined";
export type DocumentType = "BForm" | "BirthCertificate" | "PreviousResult" | "LeavingCertificate" | "Other";

export const genderLabel: Record<Gender, string> = { Male: "Male", Female: "Female", Other: "Other" };
export const relationLabel: Record<GuardianRelation, string> = { Father: "Father", Mother: "Mother", Guardian: "Guardian", Other: "Other" };
export const documentTypeLabel: Record<DocumentType, string> = {
  BForm: "B-Form",
  BirthCertificate: "Birth certificate",
  PreviousResult: "Previous result",
  LeavingCertificate: "Leaving certificate",
  Other: "Other",
};

export type PagedResult<T> = { items: T[]; total: number };

export type StudentListItem = {
  id: number;
  admissionNo: string;
  fullName: string;
  gender: Gender;
  hasPhoto: boolean;
  className?: string | null;
  sectionName?: string | null;
  rollNo?: number | null;
  guardianName?: string | null;
  guardianPhone?: string | null;
  status: StudentStatus;
  campusCode: string;
};

export type StudentDetail = {
  id: number;
  campusId: number;
  campusCode: string;
  admissionNo: string;
  firstName: string;
  lastName: string;
  fullName: string;
  gender: Gender;
  dateOfBirth: string;
  bFormNo?: string | null;
  bloodGroup?: string | null;
  religion?: string | null;
  nationality?: string | null;
  address?: string | null;
  phone?: string | null;
  admissionDate: string;
  previousSchool?: string | null;
  notes?: string | null;
  hasPhoto: boolean;
  status: StudentStatus;
  leftOn?: string | null;
  leaveReason?: string | null;
};

export type SaveStudentRequest = {
  firstName: string;
  lastName: string;
  gender: Gender;
  dateOfBirth: string;
  bFormNo?: string | null;
  bloodGroup?: string | null;
  religion?: string | null;
  nationality?: string | null;
  address?: string | null;
  phone?: string | null;
  admissionDate: string;
  previousSchool?: string | null;
  notes?: string | null;
};

export type SaveGuardianRequest = {
  fullName: string;
  cnic?: string | null;
  phone: string;
  altPhone?: string | null;
  email?: string | null;
  occupation?: string | null;
  address?: string | null;
};

export type GuardianLinkRequest = {
  guardianId?: number | null;
  newGuardian?: SaveGuardianRequest | null;
  relation: GuardianRelation;
  isPrimary: boolean;
  isEmergencyContact: boolean;
};

export type AdmissionRequest = {
  student: SaveStudentRequest;
  admissionNo?: string | null;
  enrollment: { sessionId?: number | null; sectionId: number; rollNo?: number | null };
  guardians: GuardianLinkRequest[];
};

export type StudentGuardian = {
  guardianId: number;
  fullName: string;
  cnic?: string | null;
  phone: string;
  altPhone?: string | null;
  email?: string | null;
  occupation?: string | null;
  relation: GuardianRelation;
  isPrimary: boolean;
  isEmergencyContact: boolean;
};

export type Enrollment = {
  id: number;
  sessionId: number;
  sessionName: string;
  classId: number;
  className: string;
  sectionId: number;
  sectionName: string;
  rollNo: number;
  status: EnrollmentStatus;
  startDate: string;
  endDate?: string | null;
};

export type EnrollmentHistory = {
  id: number;
  action: EnrollmentAction;
  fromSection?: string | null;
  toSection?: string | null;
  reason?: string | null;
  occurredOn: string;
  byUserName?: string | null;
};

export type StudentDocument = {
  id: number;
  type: DocumentType;
  fileName: string;
  contentType: string;
  sizeBytes: number;
  uploadedOn: string;
};

export type Sibling = {
  studentId?: number | null;
  fullName: string;
  campusCode: string;
  className?: string | null;
  sectionName?: string | null;
};

export type StudentProfile = {
  student: StudentDetail;
  guardians: StudentGuardian[];
  enrollment?: Enrollment | null;
  history: EnrollmentHistory[];
  documents: StudentDocument[];
  siblings: Sibling[];
};

export type GuardianListItem = { id: number; fullName: string; cnic?: string | null; phone: string; childrenCount: number };

export type GuardianDetail = {
  id: number;
  fullName: string;
  cnic?: string | null;
  phone: string;
  altPhone?: string | null;
  email?: string | null;
  occupation?: string | null;
  address?: string | null;
  children: Sibling[];
};

export type GuardianLookup = { id: number; fullName: string; childrenCount: number };

/* ------------------------------------------------------------------ */
/* Hiring & staff                                                     */
/* ------------------------------------------------------------------ */

export type StaffDesignation =
  | "Teacher" | "Principal" | "Coordinator" | "Accountant" | "Clerk" | "Librarian" | "Driver" | "Guard" | "Cleaner" | "Other";
export type StaffCategory = "Teaching" | "NonTeaching";
export type VacancyStatus = "Open" | "OnHold" | "Closed" | "Filled";
export type CandidateStage = "Applied" | "Shortlisted" | "Interview" | "Offered" | "Hired" | "Rejected" | "Withdrawn";
export type CandidateSource = "WalkIn" | "Referral" | "Email" | "JobPortal" | "Other";
export type InterviewResult = "Scheduled" | "Pass" | "Fail" | "Hold";
export type EmploymentType = "Permanent" | "Contract" | "Visiting" | "PartTime";
export type OfferStatus = "Pending" | "Accepted" | "Declined" | "Withdrawn";
export type StaffStatus = "Onboarding" | "Active" | "OnLeave" | "Resigned" | "Terminated";

export const designationLabel: Record<StaffDesignation, string> = {
  Teacher: "Teacher", Principal: "Principal", Coordinator: "Coordinator", Accountant: "Accountant", Clerk: "Clerk",
  Librarian: "Librarian", Driver: "Driver", Guard: "Guard", Cleaner: "Cleaner", Other: "Other",
};
export const teachingDesignations: StaffDesignation[] = ["Teacher", "Principal", "Coordinator"];
export const categoryLabel: Record<StaffCategory, string> = { Teaching: "Teaching", NonTeaching: "Non-teaching" };
export const vacancyStatusLabel: Record<VacancyStatus, string> = { Open: "Open", OnHold: "On hold", Closed: "Closed", Filled: "Filled" };
export const candidateStageLabel: Record<CandidateStage, string> = {
  Applied: "Applied", Shortlisted: "Shortlisted", Interview: "Interview", Offered: "Offered", Hired: "Hired",
  Rejected: "Rejected", Withdrawn: "Withdrawn",
};
export const candidateStageOrder: CandidateStage[] = ["Applied", "Shortlisted", "Interview", "Offered", "Hired"];
export const sourceLabel: Record<CandidateSource, string> = {
  WalkIn: "Walk-in", Referral: "Referral", Email: "Email", JobPortal: "Job portal", Other: "Other",
};
export const employmentTypeLabel: Record<EmploymentType, string> = {
  Permanent: "Permanent", Contract: "Contract", Visiting: "Visiting", PartTime: "Part-time",
};
export const staffStatusLabel: Record<StaffStatus, string> = {
  Onboarding: "Onboarding", Active: "Active", OnLeave: "On leave", Resigned: "Resigned", Terminated: "Terminated",
};

/** Manual moves, mirroring the server's pipeline table (Offered and Hired come from the offer and hire actions). */
export const allowedMoves: Record<CandidateStage, CandidateStage[]> = {
  Applied: ["Shortlisted", "Rejected", "Withdrawn"],
  Shortlisted: ["Interview", "Rejected", "Withdrawn"],
  Interview: ["Rejected", "Withdrawn"],
  Offered: ["Rejected", "Withdrawn"],
  Rejected: ["Applied"],
  Withdrawn: ["Applied"],
  Hired: [],
};

export type StageCounts = { applied: number; shortlisted: number; interview: number; offered: number; hired: number };

export type Vacancy = {
  id: number;
  campusId: number;
  campusCode: string;
  title: string;
  designation: StaffDesignation;
  category: StaffCategory;
  positions: number;
  hired: number;
  classIds: number[];
  subjectIds: number[];
  requirements?: string | null;
  closingDate?: string | null;
  status: VacancyStatus;
  counts: StageCounts;
};

export type SaveVacancyRequest = {
  title: string;
  designation: StaffDesignation;
  category: StaffCategory;
  positions: number;
  classIds: number[];
  subjectIds: number[];
  requirements: string | null;
  closingDate: string | null;
};

export type CandidateListItem = {
  id: number;
  vacancyId: number;
  vacancyTitle: string;
  fullName: string;
  phone: string;
  qualification?: string | null;
  experienceYears?: number | null;
  stage: CandidateStage;
  latestRating?: number | null;
  hasCv: boolean;
  campusCode: string;
};

export type SaveCandidateRequest = {
  vacancyId: number;
  fullName: string;
  cnic: string | null;
  phone: string;
  email: string | null;
  qualification: string | null;
  experienceYears: number | null;
  currentSalary: number | null;
  expectedSalary: number | null;
  source: CandidateSource;
};

export type InterviewRound = {
  id: number;
  name: string;
  scheduledAt: string;
  interviewerUserId: number;
  interviewerName: string;
  result: InterviewResult;
  rating?: number | null;
  notes?: string | null;
};

export type Offer = {
  id: number;
  designation: StaffDesignation;
  employmentType: EmploymentType;
  offeredSalary: number;
  proposedJoiningDate: string;
  status: OfferStatus;
  respondedOn?: string | null;
};

export type StageHistory = {
  fromStage?: CandidateStage | null;
  toStage: CandidateStage;
  reason?: string | null;
  occurredOn: string;
  byUserName?: string | null;
};

export type CandidateDetail = {
  id: number;
  campusId: number;
  campusCode: string;
  vacancyId: number;
  vacancyTitle: string;
  fullName: string;
  cnic?: string | null;
  phone: string;
  email?: string | null;
  qualification?: string | null;
  experienceYears?: number | null;
  currentSalary?: number | null;
  expectedSalary?: number | null;
  source: CandidateSource;
  stage: CandidateStage;
  stageReason?: string | null;
  hasCv: boolean;
  cvFileName?: string | null;
  staffId?: number | null;
  existingStaffWarning?: string | null;
};

export type CandidateProfile = {
  candidate: CandidateDetail;
  interviews: InterviewRound[];
  offer?: Offer | null;
  history: StageHistory[];
  allowedMoves: CandidateStage[];
};

export type StaffListItem = {
  id: number;
  employeeNo: string;
  fullName: string;
  designation: StaffDesignation;
  category: StaffCategory;
  joiningDate: string;
  status: StaffStatus;
  campusCode: string;
};

export type StaffDetail = StaffListItem & {
  campusId: number;
  firstName: string;
  lastName: string;
  cnic?: string | null;
  phone: string;
  email?: string | null;
  employmentType: EmploymentType;
  basicSalary: number;
  sourceCandidateId?: number | null;
};

export type UserPick = { id: number; fullName: string };

/* ------------------------------------------------------------------ */
/* Staff onboarding (Part 2)                                          */
/* ------------------------------------------------------------------ */

export type StaffDocumentType = "Cnic" | "Degree" | "ExperienceLetter" | "Contract" | "Photo" | "Other";

export const staffDocumentTypeLabel: Record<StaffDocumentType, string> = {
  Cnic: "CNIC copy",
  Degree: "Degree / certificate",
  ExperienceLetter: "Experience letter",
  Contract: "Contract",
  Photo: "Photo",
  Other: "Other",
};

export type SaveStaffRequest = {
  firstName: string;
  lastName: string;
  cnic: string | null;
  phone: string;
  email: string | null;
  gender: Gender | null;
  dateOfBirth: string | null;
  address: string | null;
  emergencyName: string | null;
  emergencyRelation: string | null;
  emergencyPhone: string | null;
  category: StaffCategory;
  designation: StaffDesignation;
  employmentType: EmploymentType;
  joiningDate: string;
  basicSalary: number;
  qualification: string | null;
  experienceYears: number | null;
  previousEmployer: string | null;
  bankName: string | null;
  bankAccount: string | null;
};

export type StaffPersonal = {
  gender?: Gender | null;
  dateOfBirth?: string | null;
  address?: string | null;
  hasPhoto: boolean;
  emergencyName?: string | null;
  emergencyRelation?: string | null;
  emergencyPhone?: string | null;
  qualification?: string | null;
  experienceYears?: number | null;
  previousEmployer?: string | null;
  bankName?: string | null;
  bankAccountMasked?: string | null;
  leftOn?: string | null;
  leaveReason?: string | null;
};

export type StaffDocument = { id: number; type: StaffDocumentType; fileName: string; contentType: string; sizeBytes: number; uploadedOn: string };

export type StaffChecklist = {
  documentsReceived: boolean;
  missingDocuments: StaffDocumentType[];
  contractSigned: boolean;
  contractSignedOn?: string | null;
  loginCreated: boolean;
  orientationDone: boolean;
  orientationDoneOn?: string | null;
  doneCount: number;
};

export type StaffAccount = { userId: number; email: string; roles: string[]; isActive: boolean; mustChangePassword: boolean };
export type TeachingAssignment = { id: number; sectionId: number; className: string; sectionName: string; subjectId: number; subjectName: string; startedOn: string };
export type ClassTeacherOf = { sectionId: number; className: string; sectionName: string };

export type StaffProfile = {
  staff: StaffDetail;
  personal: StaffPersonal;
  checklist: StaffChecklist;
  documents: StaffDocument[];
  assignments: TeachingAssignment[];
  classTeacherOf: ClassTeacherOf[];
  account?: StaffAccount | null;
  allowedStatuses: StaffStatus[];
};

export type OptionSubject = { subjectId: number; subjectName: string; teacherStaffId?: number | null; teacherName?: string | null };
export type OptionSection = { sectionId: number; className: string; sectionName: string; subjects: OptionSubject[] };
export type AssignmentOptions = { sessionId: number; sessionName: string; sections: OptionSection[] };

/** Login role suggested from the designation (matched by role name). */
export const roleForDesignation: Partial<Record<StaffDesignation, string>> = {
  Teacher: "Teacher",
  Coordinator: "Teacher",
  Principal: "Principal",
  Accountant: "Accountant",
  Clerk: "Clerk",
};

/* ------------------------------------------------------------------ */
/* Attendance                                                         */
/* ------------------------------------------------------------------ */

export type StudentAttendanceStatus = "Present" | "Absent" | "Late" | "Leave";
export type StaffAttendanceStatus = StudentAttendanceStatus | "HalfDay";
export type DayKind = "Working" | "WeeklyOff" | "Holiday" | "Future" | "OutsideSession";
export type WeekDay = "Sunday" | "Monday" | "Tuesday" | "Wednesday" | "Thursday" | "Friday" | "Saturday";

export const weekDays: WeekDay[] = ["Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday"];

export const attendanceCode: Record<StaffAttendanceStatus, string> = { Present: "P", Absent: "A", Late: "L", Leave: "Lv", HalfDay: "½" };
export const attendanceLabel: Record<StaffAttendanceStatus, string> = {
  Present: "Present",
  Absent: "Absent",
  Late: "Late",
  Leave: "Leave",
  HalfDay: "Half day",
};

export type Holiday = { id: number; campusId?: number | null; campusCode?: string | null; name: string; startDate: string; endDate: string };
export type CalendarInfo = { weeklyOff: WeekDay[]; holidays: Holiday[] };

export type MySection = { sectionId: number; className: string; sectionName: string; isClassTeacher: boolean; markedToday: boolean };
export type SheetRow = { studentId: number; rollNo: number; name: string; hasPhoto: boolean; status: StudentAttendanceStatus; note?: string | null };
export type Sheet = {
  sectionId: number;
  className: string;
  sectionName: string;
  date: string;
  isWorkingDay: boolean;
  reason?: string | null;
  canEdit: boolean;
  editLockedReason?: string | null;
  marked: boolean;
  markedBy?: string | null;
  markedOn?: string | null;
  rows: SheetRow[];
};

export type TodaySection = {
  sectionId: number;
  className: string;
  sectionName: string;
  classTeacherName?: string | null;
  marked: boolean;
  present: number;
  absent: number;
  late: number;
  leave: number;
};
export type Absentee = { studentId: number; name: string; className: string; sectionName: string; status: StudentAttendanceStatus };
export type TodayReport = {
  date: string;
  isWorkingDay: boolean;
  reason?: string | null;
  presentPct?: number | null;
  sections: TodaySection[];
  absentees: Absentee[];
};

export type MonthDay = { date: string; kind: DayKind; holidayName?: string | null; marked: boolean };
export type MonthStudent = {
  studentId: number;
  rollNo: number;
  name: string;
  cells: string[];
  present: number;
  absent: number;
  late: number;
  leave: number;
  pct?: number | null;
};
export type MonthlyReport = { sectionId: number; className: string; sectionName: string; month: string; days: MonthDay[]; students: MonthStudent[] };
export type LowRow = { studentId: number; name: string; className: string; sectionName: string; workingDays: number; attended: number; pct: number };
export type StudentAttendance = {
  pct?: number | null;
  present: number;
  absent: number;
  late: number;
  leave: number;
  workingDaysMarked: number;
  months: { month: string; present: number; absent: number; late: number; leave: number; pct?: number | null }[];
  absences: { date: string; status: StudentAttendanceStatus; note?: string | null }[];
};

export type StaffSheetRow = {
  staffId: number;
  employeeNo: string;
  name: string;
  designation: StaffDesignation;
  status?: StaffAttendanceStatus | null;
  checkIn?: string | null;
  note?: string | null;
  marked: boolean;
};
export type StaffSheet = { date: string; isWorkingDay: boolean; reason?: string | null; rows: StaffSheetRow[] };
export type StaffMonthRow = {
  staffId: number;
  employeeNo: string;
  name: string;
  cells: string[];
  present: number;
  absent: number;
  late: number;
  leave: number;
  halfDay: number;
  pct?: number | null;
};
export type StaffMonthly = { month: string; days: MonthDay[]; staff: StaffMonthRow[] };

// ---------------------------------------------------------------- exams

export type ExamType = "Monthly" | "MidTerm" | "Final" | "Other";
export type ExamStatus = "Draft" | "Open" | "Closed";
export type PaperSectionStatus = "NotStarted" | "InProgress" | "Submitted";
export type MarkStatus = "Present" | "Absent" | "Exempted";

export const examTypeLabel: Record<ExamType, string> = { Monthly: "Monthly test", MidTerm: "Mid-term", Final: "Final", Other: "Other" };

export type GradeBand = { grade: string; minPercent: number; isFail: boolean };
export type GradeScale = { id: number; campusId?: number | null; name: string; bands: GradeBand[] };
export type Scheme = {
  id?: number | null;
  campusId?: number | null;
  sessionId?: number | null;
  classId?: number | null;
  monthlyWeight: number;
  midTermWeight: number;
  finalWeight: number;
  otherWeight: number;
  mustPassFinal: boolean;
};
export type Schemes = { effective: Scheme; owned: Scheme[] };

export type Exam = Owned & {
  id: number;
  sessionId: number;
  sessionName: string;
  name: string;
  type: ExamType;
  startDate?: string | null;
  endDate?: string | null;
  status: ExamStatus;
  sectionsTotal: number;
  sectionsSubmitted: number;
  sectionsPublished: number;
};
export type Paper = {
  id: number;
  subjectId: number;
  subjectName: string;
  totalMarks: number;
  passMarks: number;
  hasPractical: boolean;
  practicalMarks?: number | null;
  paperDate?: string | null;
  isActive: boolean;
};
export type SectionProgress = { sectionId: number; sectionName: string; submitted: number; papers: number; published: boolean };
export type ExamClass = { classId: number; className: string; papers: Paper[]; sections: SectionProgress[] };
export type ExamDetail = { exam: Exam; classes: ExamClass[] };

export type MyPaper = {
  examId: number;
  examName: string;
  paperId: number;
  sectionId: number;
  className: string;
  sectionName: string;
  subjectName: string;
  status: PaperSectionStatus;
  published: boolean;
};
export type MarkRow = {
  studentId: number;
  rollNo: number;
  name: string;
  theory?: number | null;
  practical?: number | null;
  status?: MarkStatus | null;
  obtained?: number | null;
  grade?: string | null;
};
export type MarkSheet = {
  paper: Paper;
  examId: number;
  examName: string;
  examStatus: ExamStatus;
  sectionId: number;
  className: string;
  sectionName: string;
  status: PaperSectionStatus;
  published: boolean;
  canEdit: boolean;
  canReturn: boolean;
  lockedReason?: string | null;
  bands: GradeBand[];
  rows: MarkRow[];
};

export type SubjectResult = {
  subjectId: number;
  name: string;
  theory?: number | null;
  practical?: number | null;
  obtained?: number | null;
  total: number;
  percent?: number | null;
  grade?: string | null;
  passed?: boolean | null;
  status?: MarkStatus | null;
};
export type TabRow = {
  studentId: number;
  rollNo: number;
  name: string;
  subjects: SubjectResult[];
  obtained?: number | null;
  total?: number | null;
  percent?: number | null;
  grade?: string | null;
  passed?: boolean | null;
  position?: number | null;
};
export type Tabulation = {
  examId: number;
  examName: string;
  sectionId: number;
  className: string;
  sectionName: string;
  published: boolean;
  publishedOn?: string | null;
  subjects: string[];
  rows: TabRow[];
  pendingSubjects: string[];
};
export type AnnualCalc = {
  percent?: number | null;
  grade?: string | null;
  passed?: boolean | null;
  typePercents: Partial<Record<ExamType, number | null>>;
  effectiveWeights: Partial<Record<ExamType, number>>;
};
export type ExamPercent = { examId: number; name: string; type: ExamType; percent?: number | null };
export type AnnualRow = { studentId: number; rollNo: number; name: string; result: AnnualCalc; exams: ExamPercent[] };
export type StudentCardInfo = {
  studentId: number;
  name: string;
  admissionNo: string;
  className: string;
  sectionName: string;
  rollNo: number;
  hasPhoto: boolean;
};
export type ReportCard = {
  schoolName: string;
  campusName: string;
  title: string;
  sessionName: string;
  student: StudentCardInfo;
  subjects: SubjectResult[];
  obtained?: number | null;
  total?: number | null;
  percent?: number | null;
  grade?: string | null;
  passed?: boolean | null;
  position?: number | null;
  attendancePct?: number | null;
  remark?: string | null;
  annual?: AnnualCalc | null;
  exams?: ExamPercent[] | null;
};
export type StudentExamResult = {
  examId: number;
  name: string;
  type: ExamType;
  percent?: number | null;
  grade?: string | null;
  passed?: boolean | null;
  position?: number | null;
};
export type StudentResults = { exams: StudentExamResult[]; annual?: AnnualCalc | null };
