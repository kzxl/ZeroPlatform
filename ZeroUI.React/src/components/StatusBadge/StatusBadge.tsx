import React from 'react';
import { StatusBadgeProps } from '../../core/models/StatusBadgeModel';
import './StatusBadge.css';

export const StatusBadge: React.FC<StatusBadgeProps> = ({
  variant = 'normal',
  label,
  count,
  pulse = false,
  styleType = 'tag',
  icon,
  className = '',
}) => {
  return (
    <span
      className={`zero-status-badge variant-${variant} style-${styleType} ${className}`}
    >
      <span className="zero-status-dot-wrap">
        {pulse && <span className="zero-status-dot-pulse" />}
        <span className="zero-status-dot" />
      </span>

      {icon && <span className="zero-status-icon">{icon}</span>}
      {label && <span className="zero-status-label">{label}</span>}
      {count !== undefined && <span className="zero-status-count">{count}</span>}
    </span>
  );
};
