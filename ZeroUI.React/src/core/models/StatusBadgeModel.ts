/**
 * Industrial status badges and indicators model.
 * Synchronized with ZeroUI.WinForms.Industrial.StatusBadge
 */

export type StatusVariant =
  | 'normal'
  | 'running'
  | 'warning'
  | 'critical'
  | 'offline'
  | 'maintenance'
  | 'info';

export type StatusBadgeStyle = 'tag' | 'outline' | 'pill';

export interface StatusBadgeProps {
  variant?: StatusVariant;
  label?: string;
  count?: number;
  pulse?: boolean;
  styleType?: StatusBadgeStyle;
  icon?: string;
  className?: string;
}
