import { PaginationQuery } from '../../core/models/api-response.model';

export const HrPermissions = {
  employeesView: 'HR.Employees.View',
  employeesManage: 'HR.Employees.Manage',
} as const;

export type Gender = 'Male' | 'Female' | 'Other';
export const GENDERS: Gender[] = ['Male', 'Female', 'Other'];

export type EmploymentType = 'Permanent' | 'Contract' | 'Visiting' | 'Intern';
export const EMPLOYMENT_TYPES: EmploymentType[] = ['Permanent', 'Contract', 'Visiting', 'Intern'];

export type EmploymentStatus = 'Active' | 'OnLeave' | 'Resigned' | 'Terminated';
export const EMPLOYMENT_STATUSES: { value: EmploymentStatus; label: string }[] = [
  { value: 'Active', label: 'Active' },
  { value: 'OnLeave', label: 'On leave' },
  { value: 'Resigned', label: 'Resigned' },
  { value: 'Terminated', label: 'Terminated' },
];

export interface EmployeeListItem {
  id: string;
  employeeCode: string;
  fullName: string;
  mobileNumber: string;
  departmentId: string;
  departmentName: string | null;
  designationId: string;
  designationName: string | null;
  employmentType: EmploymentType;
  joiningDate: string;
  status: EmploymentStatus;
}

export interface EmployeeDetail extends EmployeeListItem {
  gender: Gender;
  dateOfBirth: string | null;
  email: string | null;
  address: string | null;
  exitDate: string | null;
  createdAt: string;
  rowVersion: number;
}

export interface EmployeeLookupItem {
  id: string;
  employeeCode: string;
  fullName: string;
  designationName: string | null;
}

export interface EmployeeQuery extends PaginationQuery {
  departmentId?: string;
  status?: EmploymentStatus;
  employmentType?: EmploymentType;
}

export interface EmployeeRequest {
  fullName: string;
  gender: Gender;
  dateOfBirth: string | null;
  mobileNumber: string;
  email: string | null;
  address: string | null;
  departmentId: string;
  designationId: string;
  employmentType: EmploymentType;
  joiningDate: string;
}
