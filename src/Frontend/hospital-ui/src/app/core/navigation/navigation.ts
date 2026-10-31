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
    label: 'Appointments',
    icon: 'event',
    permissions: [],
    children: [
      { label: 'Book Appointment', icon: 'event_available', route: '/appointments/book', permissions: ['Appointment.Book'] },
      { label: 'Appointments', icon: 'calendar_month', route: '/appointments', permissions: ['Appointment.View'], exact: true },
      { label: 'My Queue', icon: 'format_list_numbered', route: '/appointments/my-queue', permissions: ['Appointment.Consult'] },
      { label: 'Doctor Sessions', icon: 'schedule', route: '/appointments/sessions', permissions: ['Appointment.View'] },
      { label: 'Queue Display', icon: 'tv', route: '/queue-display', permissions: ['Appointment.QueueDisplay'] },
    ],
  },
  {
    label: 'OPD',
    icon: 'stethoscope',
    permissions: [],
    children: [{ label: 'OPD Vitals', icon: 'monitor_heart', route: '/opd/vitals', permissions: ['Clinical.Vitals.Record'] }],
  },
  {
    label: 'Billing',
    icon: 'receipt_long',
    permissions: [],
    children: [
      { label: 'Invoices', icon: 'receipt', route: '/billing/invoices', permissions: ['Billing.View'], exact: true },
      { label: 'New Invoice', icon: 'note_add', route: '/billing/invoices/new', permissions: ['Billing.Invoice.Manage'] },
      {
        label: 'Approvals',
        icon: 'approval',
        route: '/billing/approvals',
        permissions: ['Billing.Discount.Approve', 'Billing.Refund.Approve', 'Billing.Refund.Request'],
      },
      { label: 'Collections', icon: 'account_balance_wallet', route: '/billing/collections', permissions: ['Billing.View'] },
      { label: 'Price List', icon: 'sell', route: '/billing/services', permissions: ['Billing.View'] },
    ],
  },
  {
    label: 'HR',
    icon: 'groups',
    permissions: [],
    children: [
      { label: 'Employees', icon: 'badge', route: '/hr/employees', permissions: ['HR.Employees.View'] },
      { label: 'Doctors', icon: 'stethoscope', route: '/hr/doctors', permissions: ['HR.Doctors.View'] },
    ],
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
