import { PaginationQuery } from '../../core/models/api-response.model';

export const AdminPermissions = {
  profileView: 'Admin.HospitalProfile.View',
  profileManage: 'Admin.HospitalProfile.Manage',
  departmentsView: 'Admin.Departments.View',
  departmentsManage: 'Admin.Departments.Manage',
  designationsView: 'Admin.Designations.View',
  designationsManage: 'Admin.Designations.Manage',
  settingsManage: 'Admin.Settings.Manage',
} as const;

export interface HospitalProfile {
  name: string;
  nameBangla: string | null;
  shortName: string | null;
  address: string | null;
  phone: string | null;
  email: string | null;
  website: string | null;
  registrationNumber: string | null;
  licenseNumber: string | null;
  taxId: string | null;
  openingHours: string | null;
  hasLogo: boolean;
  rowVersion: number;
}

export type HospitalProfileUpdate = Omit<HospitalProfile, 'hasLogo'>;

export interface SystemSettings {
  timeZone: string;
  currencyCode: string;
  currencySymbol: string;
  financialYearStartMonth: number;
  rowVersion: number;
}

export type DepartmentType = 'Clinical' | 'Diagnostic' | 'Administrative' | 'Support';
export const DEPARTMENT_TYPES: DepartmentType[] = ['Clinical', 'Diagnostic', 'Administrative', 'Support'];

export interface Department {
  id: string;
  code: string;
  name: string;
  type: DepartmentType;
  parentDepartmentId: string | null;
  parentName: string | null;
  description: string | null;
  isActive: boolean;
  subDepartmentCount: number;
  rowVersion: number;
}

export interface DepartmentLookupItem {
  id: string;
  code: string;
  name: string;
  parentDepartmentId: string | null;
  type: DepartmentType;
}

export interface DepartmentQuery extends PaginationQuery {
  isActive?: boolean;
  type?: DepartmentType;
}

export type DesignationCategory = 'Doctor' | 'Nurse' | 'Technician' | 'Pharmacist' | 'Administrative' | 'Support' | 'Other';
export const DESIGNATION_CATEGORIES: DesignationCategory[] = [
  'Doctor', 'Nurse', 'Technician', 'Pharmacist', 'Administrative', 'Support', 'Other',
];

export interface Designation {
  id: string;
  name: string;
  category: DesignationCategory;
  isActive: boolean;
  rowVersion: number;
}

export interface DesignationQuery extends PaginationQuery {
  isActive?: boolean;
  category?: DesignationCategory;
}

export type YearFormat = 'None' | 'TwoDigit' | 'FourDigit';
export type ResetPolicy = 'Never' | 'Yearly';

export interface NumberSequence {
  key: string;
  name: string;
  prefix: string;
  yearFormat: YearFormat;
  padLength: number;
  resetPolicy: ResetPolicy;
  currentValue: number;
  nextNumberPreview: string;
  rowVersion: number;
}

export const MONTHS = [
  'January', 'February', 'March', 'April', 'May', 'June',
  'July', 'August', 'September', 'October', 'November', 'December',
];
