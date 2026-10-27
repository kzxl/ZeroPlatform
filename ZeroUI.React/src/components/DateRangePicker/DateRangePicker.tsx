import React, { useState, useRef, useEffect, useMemo } from 'react';
import {
  DateRange,
  DateRangeViewMode,
  DateRangeViewModeHelper,
  DateRangePreset,
} from '../../core/models/DateRangeModel';
import { MenuIcons } from '../../core/icons/MenuIcons';
import './DateRangePicker.css';

export interface DateRangePickerProps {
  value?: DateRange;
  defaultValue?: DateRange;
  onChange?: (range: DateRange) => void;
  viewMode?: DateRangeViewMode;
  onViewModeChange?: (mode: DateRangeViewMode) => void;
  presets?: DateRangePreset[];
  disabled?: boolean;
  className?: string;
  placeholder?: string;
}

export const DateRangePicker: React.FC<DateRangePickerProps> = ({
  value,
  defaultValue,
  onChange,
  viewMode: controlledViewMode,
  onViewModeChange,
  presets,
  disabled = false,
  className = '',
  placeholder = 'Select date range...',
}) => {
  const [internalRange, setInternalRange] = useState<DateRange>(() => {
    if (defaultValue) return defaultValue;
    const now = new Date();
    const start = new Date(now.getFullYear(), now.getMonth(), 1);
    const end = new Date(now.getFullYear(), now.getMonth() + 1, 0);
    return { start, end };
  });

  const activeRange = value ?? internalRange;

  const [internalMode, setInternalMode] = useState<DateRangeViewMode>('Day');
  const activeMode = controlledViewMode ?? internalMode;

  const [isOpen, setIsOpen] = useState(false);
  const containerRef = useRef<HTMLDivElement>(null);

  // Temporary selection state while popup is open
  const [tempStart, setTempStart] = useState<Date>(activeRange.start);
  const [tempEnd, setTempEnd] = useState<Date | null>(activeRange.end);
  const [hoverDate, setHoverDate] = useState<Date | null>(null);

  // Current navigation view year/month
  const [navYear, setNavYear] = useState(activeRange.start.getFullYear());
  const [navMonth, setNavMonth] = useState(activeRange.start.getMonth());

  const effectivePresets = useMemo(
    () => presets ?? DateRangeViewModeHelper.getStandardPresets(),
    [presets]
  );

  // Handle clicking outside to close
  useEffect(() => {
    const handleClickOutside = (e: MouseEvent) => {
      if (containerRef.current && !containerRef.current.contains(e.target as Node)) {
        setIsOpen(false);
      }
    };
    if (isOpen) {
      document.addEventListener('mousedown', handleClickOutside);
    }
    return () => {
      document.removeEventListener('mousedown', handleClickOutside);
    };
  }, [isOpen]);

  const handleOpen = () => {
    if (disabled) return;
    setTempStart(activeRange.start);
    setTempEnd(activeRange.end);
    setNavYear(activeRange.start.getFullYear());
    setNavMonth(activeRange.start.getMonth());
    setIsOpen(true);
  };

  const handleApply = () => {
    if (tempStart && tempEnd) {
      const normalized = DateRangeViewModeHelper.normalizeRange(tempStart, tempEnd, activeMode);
      if (!value) {
        setInternalRange(normalized);
      }
      onChange?.(normalized);
    }
    setIsOpen(false);
  };

  const handleModeChange = (newMode: DateRangeViewMode) => {
    onViewModeChange?.(newMode);
    setInternalMode(newMode);
  };

  const handlePresetClick = (preset: DateRangePreset) => {
    const range = preset.getRange();
    const normalized = DateRangeViewModeHelper.normalizeRange(range.start, range.end, activeMode);
    if (!value) {
      setInternalRange(normalized);
    }
    onChange?.(normalized);
    setIsOpen(false);
  };

  // Day Calendar math
  const daysInMonth = useMemo(() => {
    const totalDays = new Date(navYear, navMonth + 1, 0).getDate();
    const firstDayOfWeek = new Date(navYear, navMonth, 1).getDay(); // 0 = Sun
    return { totalDays, firstDayOfWeek };
  }, [navYear, navMonth]);

  const handleDayClick = (day: number) => {
    const clicked = new Date(navYear, navMonth, day);
    if (!tempEnd || (tempStart && tempEnd)) {
      setTempStart(clicked);
      setTempEnd(null);
    } else {
      if (clicked < tempStart) {
        setTempStart(clicked);
        setTempEnd(tempStart);
      } else {
        setTempEnd(clicked);
      }
    }
  };

  const isDayInRange = (day: number) => {
    const current = new Date(navYear, navMonth, day).getTime();
    const start = tempStart ? tempStart.getTime() : 0;
    const end = (tempEnd || hoverDate) ? (tempEnd || hoverDate)!.getTime() : 0;
    const min = Math.min(start, end);
    const max = Math.max(start, end);
    return current >= min && current <= max;
  };

  const isDayStart = (day: number) => {
    if (!tempStart) return false;
    const current = new Date(navYear, navMonth, day);
    return (
      current.getFullYear() === tempStart.getFullYear() &&
      current.getMonth() === tempStart.getMonth() &&
      current.getDate() === tempStart.getDate()
    );
  };

  const isDayEnd = (day: number) => {
    if (!tempEnd) return false;
    const current = new Date(navYear, navMonth, day);
    return (
      current.getFullYear() === tempEnd.getFullYear() &&
      current.getMonth() === tempEnd.getMonth() &&
      current.getDate() === tempEnd.getDate()
    );
  };

  const monthNames = [
    'Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun',
    'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'
  ];

  return (
    <div className={`zero-daterange-picker-container ${className}`} ref={containerRef}>
      <button
        type="button"
        className={`zero-daterange-trigger ${isOpen ? 'is-active' : ''}`}
        onClick={handleOpen}
        disabled={disabled}
      >
        <span className="trigger-icon">{MenuIcons.View}</span>
        <span>
          {activeRange
            ? DateRangeViewModeHelper.formatRange(activeRange.start, activeRange.end, activeMode)
            : placeholder}
        </span>
        <span className="trigger-icon">{MenuIcons.ArrowDown}</span>
      </button>

      {isOpen && (
        <div className="zero-daterange-popover">
          {/* Preset Buttons */}
          <div className="zero-daterange-presets">
            {effectivePresets.map((preset) => (
              <button
                key={preset.id}
                type="button"
                className="zero-daterange-preset-btn"
                onClick={() => handlePresetClick(preset)}
              >
                {preset.label}
              </button>
            ))}
          </div>

          {/* Main Picker Body */}
          <div className="zero-daterange-main">
            {/* Header with Mode Switching and Month/Year Nav */}
            <div className="zero-daterange-header">
              <button
                type="button"
                className="zero-daterange-nav-btn"
                onClick={() => {
                  if (activeMode === 'Day') {
                    if (navMonth === 0) {
                      setNavMonth(11);
                      setNavYear((y) => y - 1);
                    } else {
                      setNavMonth((m) => m - 1);
                    }
                  } else {
                    setNavYear((y) => y - 1);
                  }
                }}
              >
                ◀
              </button>

              <span className="zero-daterange-title">
                {activeMode === 'Day' && `${monthNames[navMonth]} ${navYear}`}
                {activeMode === 'Month' && `${navYear}`}
                {activeMode === 'Year' && `${navYear - 5} - ${navYear + 6}`}
              </span>

              <button
                type="button"
                className="zero-daterange-nav-btn"
                onClick={() => {
                  if (activeMode === 'Day') {
                    if (navMonth === 11) {
                      setNavMonth(0);
                      setNavYear((y) => y + 1);
                    } else {
                      setNavMonth((m) => m + 1);
                    }
                  } else {
                    setNavYear((y) => y + 1);
                  }
                }}
              >
                ▶
              </button>

              <div className="zero-daterange-mode-pills">
                {(['Day', 'Month', 'Year'] as DateRangeViewMode[]).map((m) => (
                  <button
                    key={m}
                    type="button"
                    className={`zero-daterange-mode-pill ${activeMode === m ? 'is-active' : ''}`}
                    onClick={() => handleModeChange(m)}
                  >
                    {m}
                  </button>
                ))}
              </div>
            </div>

            {/* Day Calendar Grid */}
            {activeMode === 'Day' && (
              <div className="zero-daterange-grid">
                {['Su', 'Mo', 'Tu', 'We', 'Th', 'Fr', 'Sa'].map((d) => (
                  <div key={d} className="zero-daterange-weekday">
                    {d}
                  </div>
                ))}
                {Array.from({ length: daysInMonth.firstDayOfWeek }).map((_, i) => (
                  <div key={`pad-${i}`} className="zero-daterange-cell empty" />
                ))}
                {Array.from({ length: daysInMonth.totalDays }).map((_, i) => {
                  const day = i + 1;
                  const isStart = isDayStart(day);
                  const isEnd = isDayEnd(day);
                  const inRange = isDayInRange(day);

                  return (
                    <div
                      key={`day-${day}`}
                      className={`zero-daterange-cell ${isStart ? 'is-range-start' : ''} ${
                        isEnd ? 'is-range-end' : ''
                      } ${inRange ? 'is-in-range' : ''}`}
                      onClick={() => handleDayClick(day)}
                      onMouseEnter={() => {
                        if (tempStart && !tempEnd) {
                          setHoverDate(new Date(navYear, navMonth, day));
                        }
                      }}
                    >
                      {day}
                    </div>
                  );
                })}
              </div>
            )}

            {/* Month Calendar Grid */}
            {activeMode === 'Month' && (
              <div className="zero-daterange-grid-12">
                {monthNames.map((name, idx) => {
                  const isSelStart =
                    tempStart.getFullYear() === navYear && tempStart.getMonth() === idx;
                  const isSelEnd =
                    tempEnd && tempEnd.getFullYear() === navYear && tempEnd.getMonth() === idx;
                  return (
                    <div
                      key={name}
                      className={`zero-daterange-cell-12 ${
                        isSelStart || isSelEnd ? 'is-selected' : ''
                      }`}
                      onClick={() => {
                        const clicked = new Date(navYear, idx, 1);
                        if (!tempEnd || (tempStart && tempEnd)) {
                          setTempStart(clicked);
                          setTempEnd(null);
                        } else {
                          setTempEnd(new Date(navYear, idx + 1, 0));
                        }
                      }}
                    >
                      {name}
                    </div>
                  );
                })}
              </div>
            )}

            {/* Year Calendar Grid */}
            {activeMode === 'Year' && (
              <div className="zero-daterange-grid-12">
                {Array.from({ length: 12 }).map((_, i) => {
                  const y = navYear - 5 + i;
                  const isSelStart = tempStart.getFullYear() === y;
                  const isSelEnd = tempEnd && tempEnd.getFullYear() === y;
                  return (
                    <div
                      key={y}
                      className={`zero-daterange-cell-12 ${
                        isSelStart || isSelEnd ? 'is-selected' : ''
                      }`}
                      onClick={() => {
                        const clicked = new Date(y, 0, 1);
                        if (!tempEnd || (tempStart && tempEnd)) {
                          setTempStart(clicked);
                          setTempEnd(null);
                        } else {
                          setTempEnd(new Date(y, 11, 31));
                        }
                      }}
                    >
                      {y}
                    </div>
                  );
                })}
              </div>
            )}

            {/* Footer */}
            <div className="zero-daterange-footer">
              <span className="zero-daterange-selected-preview">
                {tempStart
                  ? DateRangeViewModeHelper.formatRange(
                      tempStart,
                      tempEnd || tempStart,
                      activeMode
                    )
                  : 'Select range'}
              </span>
              <div className="zero-daterange-actions">
                <button
                  type="button"
                  className="zero-daterange-action-btn btn-cancel"
                  onClick={() => setIsOpen(false)}
                >
                  Cancel
                </button>
                <button
                  type="button"
                  className="zero-daterange-action-btn btn-apply"
                  onClick={handleApply}
                >
                  Apply
                </button>
              </div>
            </div>
          </div>
        </div>
      )}
    </div>
  );
};
