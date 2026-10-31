export interface NavItem {
  label: string;
  icon: string;
  route?: string;
  /** Shown when the user holds ANY of these permissions; empty = every signed-in user. */
  permissions: string[];
  children?: NavItem[];
}

/**
 * Sidebar menu (docs/NAVIGATION.md). Items are added here as each module is approved and built.
 */
export const NAVIGATION: NavItem[] = [
  { label: 'Dashboard', icon: 'dashboard', route: '/dashboard', permissions: [] },
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
