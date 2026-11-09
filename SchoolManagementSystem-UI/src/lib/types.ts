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
/* Ownership: campusId / campusCode are absent for head-office (HQ) rows */
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
