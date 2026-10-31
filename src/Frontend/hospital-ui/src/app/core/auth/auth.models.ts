export interface LoginRequest {
  userNameOrEmail: string;
  password: string;
}

export interface ChangePasswordRequest {
  currentPassword: string;
  newPassword: string;
}

export interface AuthUser {
  id: string;
  userName: string;
  fullName: string;
  email: string | null;
  roles: string[];
  permissions: string[];
  mustChangePassword: boolean;
}

export interface LoginResponse {
  accessToken: string;
  accessTokenExpiresAt: string;
  user: AuthUser;
}
