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
];
