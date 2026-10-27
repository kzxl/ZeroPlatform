/**
 * Models and definitions for industrial workflow pipelines and milestone tracking.
 * Synchronized with ZeroUI.WinForms.Industrial.WorkflowCard
 */

export type StepStatus = 'Waiting' | 'InProgress' | 'Warning' | 'Completed';
export type StepGlyph = 'Gear' | 'Checkmark' | 'Warehouse' | 'Truck';

export interface WorkflowStage {
  key: string;
  title: string;
  quantity: number;
  updatedTime?: string;
  status: StepStatus;
  glyph: StepGlyph;
}

export interface WorkflowCardProps {
  stepNumber?: number;
  stepText?: string;
  badgeColor?: string;
  title: string;
  subtitle?: string;
  statusTag?: string;
  statusTagColor?: string;
  footerText?: string;
  stages: WorkflowStage[];
  onStageClick?: (stage: WorkflowStage, index: number) => void;
  className?: string;
}
