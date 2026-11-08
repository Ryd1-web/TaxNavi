'use client';

import { createContext, ReactNode, useCallback, useContext, useEffect, useMemo, useState } from 'react';
import { listGuestReports, removeGuestReport } from '@/lib/guestReports';
import { apiService, SessionData, TOKEN_KEY } from '@/services/apiService';
import { User } from '@/types';

const USER_KEY = 'taxnavi.user';
const LEGACY_KEYS = ['jwtToken', 'user'];

interface RegisterInput {
  email: string;
  password: string;
  firstName: string;
  lastName: string;
  userType: User['userType'];
}

interface AuthContextType {
  user: User | null;
  isAuthenticated: boolean;
  isLoading: boolean;
  login: (email: string, password: string) => Promise<User>;
  register: (input: RegisterInput) => Promise<User>;
  logout: () => void;
  setUser: (user: User) => void;
}

const AuthContext = createContext<AuthContextType | undefined>(undefined);

function tokenExpiry(token: string): number | null {
  try {
    const payload = JSON.parse(atob(token.split('.')[1].replace(/-/g, '+').replace(/_/g, '/')));
    return typeof payload.exp === 'number' ? payload.exp * 1000 : null;
  } catch {
    return null;
  }
}

function isTokenValid(token: string | null): token is string {
  if (!token) return false;
  const exp = tokenExpiry(token);
  return exp !== null && exp > Date.now() + 30_000;
}

/** Moves reports bought as a guest on this device into the account — only when the purchase email matches. */
async function claimGuestReports(email: string) {
  for (const guest of listGuestReports()) {
    if (!guest.email || guest.email.toLowerCase() !== email.toLowerCase()) continue;
    try {
      await apiService.claimReport(guest.reportId, guest.accessToken);
      removeGuestReport(guest.reportId);
    } catch {
      /* not claimable (e.g. already claimed elsewhere): leave it on the device */
    }
  }
}

function sessionToUser(session: SessionData): User {
  return {
    id: session.userId,
    email: session.email,
    firstName: session.firstName,
    lastName: session.lastName,
    userType: session.userType === 'SME' ? 'SME' : 'Individual',
  };
}

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUserState] = useState<User | null>(null);
  const [isLoading, setIsLoading] = useState(true);

  const persistUser = useCallback((next: User) => {
    window.localStorage.setItem(USER_KEY, JSON.stringify(next));
    setUserState(next);
  }, []);

  const logout = useCallback(() => {
    window.localStorage.removeItem(TOKEN_KEY);
    window.localStorage.removeItem(USER_KEY);
    setUserState(null);
  }, []);

  useEffect(() => {
    LEGACY_KEYS.forEach((key) => window.localStorage.removeItem(key));
    apiService.setUnauthorizedHandler(logout);

    const token = window.localStorage.getItem(TOKEN_KEY);
    if (!isTokenValid(token)) {
      logout();
      setIsLoading(false);
      return;
    }

    try {
      const cached = window.localStorage.getItem(USER_KEY);
      if (cached) setUserState(JSON.parse(cached));
    } catch {
      /* ignore corrupt cache */
    }

    // Refresh the profile from the server so account type changes are picked up.
    apiService
      .me()
      .then((me) => persistUser({ id: me.id, email: me.email, firstName: me.firstName, lastName: me.lastName, userType: me.userType }))
      .catch(() => undefined)
      .finally(() => setIsLoading(false));

    // Sign out exactly when the token expires.
    const exp = tokenExpiry(token);
    if (exp) {
      const timer = window.setTimeout(logout, Math.min(exp - Date.now(), 2_147_000_000));
      return () => window.clearTimeout(timer);
    }
  }, [logout, persistUser]);

  const startSession = useCallback(
    (session: SessionData) => {
      window.localStorage.setItem(TOKEN_KEY, session.jwtToken);
      const next = sessionToUser(session);
      persistUser(next);
      void claimGuestReports(next.email);
      return next;
    },
    [persistUser],
  );

  const login = useCallback(
    async (email: string, password: string) => startSession(await apiService.login(email, password)),
    [startSession],
  );

  const register = useCallback(
    async (input: RegisterInput) => startSession(await apiService.register(input)),
    [startSession],
  );

  const value = useMemo(
    () => ({ user, isAuthenticated: !!user, isLoading, login, register, logout, setUser: persistUser }),
    [user, isLoading, login, register, logout, persistUser],
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth() {
  const context = useContext(AuthContext);
  if (!context) throw new Error('useAuth must be used within an AuthProvider');
  return context;
}
