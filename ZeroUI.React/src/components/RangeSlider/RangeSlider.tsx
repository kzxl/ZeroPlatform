import React, { useRef, useCallback } from 'react';
import {
  RangeSliderProps,
  RangeSliderEngine,
} from '../../core/models/RangeSliderModel';
import './RangeSlider.css';

export const RangeSlider: React.FC<RangeSliderProps> = ({
  min,
  max,
  step = 1,
  value,
  onChange,
  formatLabel = (v) => String(v),
  disabled = false,
  className = '',
}) => {
  const trackRef = useRef<HTMLDivElement>(null);

  const minPercent = RangeSliderEngine.toPercent(value.min, min, max);
  const maxPercent = RangeSliderEngine.toPercent(value.max, min, max);

  const calculateValueFromPointer = useCallback(
    (clientX: number): number => {
      if (!trackRef.current) return min;
      const rect = trackRef.current.getBoundingClientRect();
      const percent = Math.max(0, Math.min(100, ((clientX - rect.left) / rect.width) * 100));
      const raw = RangeSliderEngine.fromPercent(percent, min, max);
      const snapped = RangeSliderEngine.snapToStep(raw, step, min);
      return RangeSliderEngine.clamp(snapped, min, max);
    },
    [min, max, step]
  );

  const handleThumbMouseDown = (type: 'min' | 'max') => (e: React.MouseEvent) => {
    if (disabled) return;
    e.preventDefault();

    const handleMouseMove = (moveEvent: MouseEvent) => {
      const newVal = calculateValueFromPointer(moveEvent.clientX);
      if (type === 'min') {
        onChange({ min: Math.min(newVal, value.max), max: value.max });
      } else {
        onChange({ min: value.min, max: Math.max(newVal, value.min) });
      }
    };

    const handleMouseUp = () => {
      window.removeEventListener('mousemove', handleMouseMove);
      window.removeEventListener('mouseup', handleMouseUp);
    };

    window.addEventListener('mousemove', handleMouseMove);
    window.addEventListener('mouseup', handleMouseUp);
  };

  return (
    <div className={`zero-rangeslider-wrap ${className}`}>
      <div className="zero-rangeslider-header">
        <span>Range Selector</span>
        <span className="zero-rangeslider-value-pill">
          {formatLabel(value.min)} - {formatLabel(value.max)}
        </span>
      </div>

      <div className="zero-rangeslider-track-container" ref={trackRef}>
        <div className="zero-rangeslider-rail" />
        <div
          className="zero-rangeslider-track"
          style={{
            left: `${minPercent}%`,
            width: `${Math.max(0, maxPercent - minPercent)}%`,
          }}
        />

        {/* Min Thumb */}
        <div
          className="zero-rangeslider-thumb"
          style={{ left: `${minPercent}%` }}
          onMouseDown={handleThumbMouseDown('min')}
        />

        {/* Max Thumb */}
        <div
          className="zero-rangeslider-thumb"
          style={{ left: `${maxPercent}%` }}
          onMouseDown={handleThumbMouseDown('max')}
        />
      </div>

      <div className="zero-rangeslider-bounds">
        <span>{formatLabel(min)}</span>
        <span>{formatLabel(max)}</span>
      </div>
    </div>
  );
};
