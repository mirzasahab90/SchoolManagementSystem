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
};

export type RoleListItem = {
  id: number;
  name: string;
  description?: string | null;
  isSystem: boolean;
  isActive: boolean;
  userCount: number;
  permissionCount: number;
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
