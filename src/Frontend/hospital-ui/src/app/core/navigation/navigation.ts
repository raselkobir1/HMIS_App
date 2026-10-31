export interface NavItem {
  label: string;
  icon: string;
  route?: string;
  /** Shown when the user holds ANY of these permissions; empty = every signed-in user. */
  permissions: string[];
  /** Highlight only on an exact URL match (for routes that prefix other menu routes, e.g. /patients vs /patients/new). */
  exact?: boolean;
  children?: NavItem[];
}

/**
 * Sidebar menu (docs/NAVIGATION.md). Items are added here as each module is approved and built.
 */
export const NAVIGATION: NavItem[] = [
  { label: 'Dashboard', icon: 'dashboard', route: '/dashboard', permissions: [] },
  {
    label: 'Patients',
    icon: 'personal_injury',
    permissions: [],
    children: [
      { label: 'Find Patient', icon: 'person_search', route: '/patients', permissions: ['Patient.View'], exact: true },
      { label: 'Register Patient', icon: 'person_add', route: '/patients/new', permissions: ['Patient.Create'] },
      { label: 'Merge Requests', icon: 'merge', route: '/patients/merge-requests', permissions: ['Patient.Merge.Approve'] },
    ],
  },
  {
    label: 'HR',
    icon: 'groups',
    permissions: [],
    children: [{ label: 'Employees', icon: 'badge', route: '/hr/employees', permissions: ['HR.Employees.View'] }],
  },
  {
    label: 'Administration',
    icon: 'apartment',
    permissions: [],
    children: [
      { label: 'Hospital Profile', icon: 'local_hospital', route: '/administration/hospital-profile', permissions: ['Admin.HospitalProfile.View'] },
      { label: 'Departments', icon: 'account_tree', route: '/administration/departments', permissions: ['Admin.Departments.View'] },
      { label: 'Designations', icon: 'badge', route: '/administration/designations', permissions: ['Admin.Designations.View'] },
      { label: 'Settings & Numbering', icon: 'settings', route: '/administration/settings', permissions: ['Admin.Settings.Manage'] },
    ],
  },
  {
    label: 'Security',
    icon: 'shield_person',
    permissions: [],
    children: [
      { label: 'Users', icon: 'group', route: '/security/users', permissions: ['Identity.Users.View'] },
      { label: 'Roles & Permissions', icon: 'admin_panel_settings', route: '/security/roles', permissions: ['Identity.Roles.View'] },
      { label: 'Login History', icon: 'history', route: '/security/login-history', permissions: ['Identity.LoginHistory.View'] },
      { label: 'Audit Log', icon: 'policy', route: '/audit-log', permissions: ['Audit.View'] },
    ],
  },
];
