export interface ZeroThemePalette {
  name: string;
  isDark: boolean;
  background: string;
  surface: string;
  card: string;
  border: string;
  textPrimary: string;
  textSecondary: string;
  textMuted: string;
  primary: string;
  primaryHover: string;
  hover: string;
  success: string;
  danger: string;
  warning: string;
  radius: number;
}

export const darkTheme: ZeroThemePalette = {
  name: 'Dark Enterprise',
  isDark: true,
  background: '#0F172A', // Slate 900
  surface: '#1E293B',    // Slate 800
  card: '#1E293B',
  border: '#334155',     // Slate 700
  textPrimary: '#F8FAFC',
  textSecondary: '#94A3B8',
  textMuted: '#64748B',
  primary: '#0EA5E9',    // Sky 500
  primaryHover: '#0284C7',
  hover: 'rgba(255, 255, 255, 0.06)',
  success: '#10B981',    // Emerald 500
  danger: '#EF4444',     // Red 500
  warning: '#F59E0B',    // Amber 500
  radius: 6,
};

export const lightTheme: ZeroThemePalette = {
  name: 'Light Enterprise',
  isDark: false,
  background: '#F8FAFC', // Slate 50
  surface: '#FFFFFF',
  card: '#FFFFFF',
  border: '#E2E8F0',     // Slate 200
  textPrimary: '#0F172A',
  textSecondary: '#475569',
  textMuted: '#94A3B8',
  primary: '#0284C7',    // Sky 600
  primaryHover: '#0369A1',
  hover: 'rgba(0, 0, 0, 0.04)',
  success: '#059669',    // Emerald 600
  danger: '#DC2626',     // Red 600
  warning: '#D97706',    // Amber 600
  radius: 6,
};
