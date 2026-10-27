import React from 'react';
import { WorkflowCardProps, StepGlyph } from '../../core/models/WorkflowModel';
import './WorkflowCard.css';

const renderStageIcon = (glyph: StepGlyph) => {
  switch (glyph) {
    case 'Checkmark':
      return (
        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5">
          <polyline points="20 6 9 17 4 12" />
        </svg>
      );
    case 'Warehouse':
      return (
        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
          <path d="M3 10L12 3l9 7v10a1 1 0 0 1-1 1H4a1 1 0 0 1-1-1V10z" />
          <path d="M9 21V12h6v9" />
        </svg>
      );
    case 'Truck':
      return (
        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
          <rect x="1" y="5" width="15" height="13" rx="1" />
          <polygon points="16 8 20 8 23 11 23 18 16 18 16 8" />
          <circle cx="5.5" cy="18.5" r="2.5" />
          <circle cx="18.5" cy="18.5" r="2.5" />
        </svg>
      );
    case 'Gear':
    default:
      return (
        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
          <circle cx="12" cy="12" r="3" />
          <path d="M19.4 15a1.65 1.65 0 0 0 .33 1.82l.06.06a2 2 0 1 1-2.83 2.83l-.06-.06a1.65 1.65 0 0 0-1.82-.33 1.65 1.65 0 0 0-1 1.51V21a2 2 0 1 1-4 0v-.09A1.65 1.65 0 0 0 9 19.4a1.65 1.65 0 0 0-1.82.33l-.06.06a2 2 0 1 1-2.83-2.83l.06-.06a1.65 1.65 0 0 0 .33-1.82 1.65 1.65 0 0 0-1.51-1H3a2 2 0 1 1 0-4h.09A1.65 1.65 0 0 0 4.6 9a1.65 1.65 0 0 0-.33-1.82l-.06-.06a2 2 0 1 1 2.83-2.83l.06.06a1.65 1.65 0 0 0 1.82.33H9a1.65 1.65 0 0 0 1-1.51V3a2 2 0 1 1 4 0v.09a1.65 1.65 0 0 0 1 1.51 1.65 1.65 0 0 0 1.82-.33l.06-.06a2 2 0 1 1 2.83 2.83l-.06.06a1.65 1.65 0 0 0-.33 1.82V9a1.65 1.65 0 0 0 1.51 1H21a2 2 0 1 1 0 4h-.09a1.65 1.65 0 0 0-1.51 1z" />
        </svg>
      );
  }
};

export const WorkflowCard: React.FC<WorkflowCardProps> = ({
  stepNumber,
  stepText,
  badgeColor,
  title,
  subtitle,
  statusTag = 'Operating',
  statusTagColor = '#10b981',
  footerText = 'Click any stage to view details or transition production step',
  stages,
  onStageClick,
  className = '',
}) => {
  const badgeLabel = stepText ?? (stepNumber !== undefined ? String(stepNumber) : '');

  return (
    <div className={`zero-workflow-card ${className}`}>
      {/* Header */}
      <div className="zero-workflow-header">
        <div className="zero-workflow-header-left">
          {badgeLabel && (
            <div
              className="zero-workflow-step-badge"
              style={badgeColor ? { backgroundColor: badgeColor } : undefined}
            >
              {badgeLabel}
            </div>
          )}
          <div className="zero-workflow-title-wrap">
            <div className="zero-workflow-title">{title}</div>
            {subtitle && <div className="zero-workflow-subtitle">{subtitle}</div>}
          </div>
        </div>

        {statusTag && (
          <div
            className="zero-workflow-status-tag"
            style={{
              backgroundColor: `${statusTagColor}20`,
              color: statusTagColor,
              border: `1px solid ${statusTagColor}60`,
            }}
          >
            {statusTag}
          </div>
        )}
      </div>

      {/* Stages Milestone Pipeline */}
      <div className="zero-workflow-pipeline">
        {stages.map((stage, idx) => {
          const isCompleted = stage.status === 'Completed';

          return (
            <React.Fragment key={stage.key || idx}>
              <div
                className={`zero-workflow-stage-box stage-status-${stage.status}`}
                onClick={() => onStageClick?.(stage, idx)}
              >
                <div className="zero-workflow-stage-icon-circle">
                  {renderStageIcon(stage.glyph)}
                </div>
                <div className="zero-workflow-stage-info">
                  <span className="zero-workflow-stage-title" title={stage.title}>
                    {stage.title}
                  </span>
                  <span className="zero-workflow-stage-qty">
                    Qty: {stage.quantity.toLocaleString()}
                  </span>
                  <span className="zero-workflow-stage-time">
                    Updated: {stage.updatedTime ?? '--'}
                  </span>
                </div>
              </div>

              {idx < stages.length - 1 && (
                <div
                  className={`zero-workflow-arrow-connector ${
                    isCompleted ? 'is-completed' : ''
                  }`}
                >
                  ➔
                </div>
              )}
            </React.Fragment>
          );
        })}
      </div>

      {/* Footer */}
      {footerText && <div className="zero-workflow-footer">{footerText}</div>}
    </div>
  );
};
