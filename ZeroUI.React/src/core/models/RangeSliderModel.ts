/**
 * Headless calculations for dual thumb range slider.
 */

export interface RangeValue {
  min: number;
  max: number;
}

export interface RangeSliderProps {
  min: number;
  max: number;
  step?: number;
  value: RangeValue;
  onChange: (value: RangeValue) => void;
  formatLabel?: (value: number) => string;
  disabled?: boolean;
  className?: string;
}

export class RangeSliderEngine {
  public static clamp(val: number, min: number, max: number): number {
    return Math.min(Math.max(val, min), max);
  }

  public static snapToStep(val: number, step: number, min: number): number {
    const stepsFromMin = Math.round((val - min) / step);
    return min + stepsFromMin * step;
  }

  public static toPercent(val: number, min: number, max: number): number {
    if (max <= min) return 0;
    return ((val - min) / (max - min)) * 100;
  }

  public static fromPercent(percent: number, min: number, max: number): number {
    return min + (percent / 100) * (max - min);
  }
}
