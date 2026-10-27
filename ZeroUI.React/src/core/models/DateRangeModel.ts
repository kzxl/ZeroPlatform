/**
 * Specifies the calendar resolution and selection granularity for DateRangePicker.
 * 100% compatible with ZeroUI.Core.Input.Date.DateRangeViewMode
 */
export type DateRangeViewMode = 'Day' | 'Month' | 'Year';

export interface DateRange {
  start: Date;
  end: Date;
}

export interface DateRangePreset {
  id: string;
  label: string;
  getRange: () => DateRange;
}

/**
 * Headless helper utilities for calculating and clamping date boundaries based on DateRangeViewMode.
 */
export class DateRangeViewModeHelper {
  /**
   * Clamps start and end dates according to the specified DateRangeViewMode.
   * Month: clamps start to 1st of month (00:00:00) and end to last day of month (23:59:59).
   * Year: clamps start to Jan 1 and end to Dec 31.
   * Day: clamps hours to 00:00:00 and 23:59:59.
   */
  public static normalizeRange(start: Date, end: Date, mode: DateRangeViewMode): DateRange {
    const s = new Date(Math.min(start.getTime(), end.getTime()));
    const e = new Date(Math.max(start.getTime(), end.getTime()));

    switch (mode) {
      case 'Month': {
        const firstDay = new Date(s.getFullYear(), s.getMonth(), 1, 0, 0, 0, 0);
        const lastDay = new Date(e.getFullYear(), e.getMonth() + 1, 0, 23, 59, 59, 999);
        return { start: firstDay, end: lastDay };
      }
      case 'Year': {
        const firstDay = new Date(s.getFullYear(), 0, 1, 0, 0, 0, 0);
        const lastDay = new Date(e.getFullYear(), 11, 31, 23, 59, 59, 999);
        return { start: firstDay, end: lastDay };
      }
      case 'Day':
      default: {
        const startDay = new Date(s.getFullYear(), s.getMonth(), s.getDate(), 0, 0, 0, 0);
        const endDay = new Date(e.getFullYear(), e.getMonth(), e.getDate(), 23, 59, 59, 999);
        return { start: startDay, end: endDay };
      }
    }
  }

  public static getDefaultFormat(mode: DateRangeViewMode): string {
    switch (mode) {
      case 'Month':
        return 'yyyy-MM';
      case 'Year':
        return 'yyyy';
      case 'Day':
      default:
        return 'yyyy-MM-dd';
    }
  }

  public static formatDate(date: Date, mode: DateRangeViewMode): string {
    const year = date.getFullYear();
    const month = String(date.getMonth() + 1).padStart(2, '0');
    const day = String(date.getDate()).padStart(2, '0');

    switch (mode) {
      case 'Month':
        return `${year}-${month}`;
      case 'Year':
        return `${year}`;
      case 'Day':
      default:
        return `${year}-${month}-${day}`;
    }
  }

  public static formatRange(start: Date, end: Date, mode: DateRangeViewMode, separator = ' - '): string {
    return `${this.formatDate(start, mode)}${separator}${this.formatDate(end, mode)}`;
  }

  /**
   * Generates standard enterprise date presets.
   */
  public static getStandardPresets(): DateRangePreset[] {
    const now = new Date();
    return [
      {
        id: 'today',
        label: 'Today',
        getRange: () => {
          const today = new Date();
          return { start: today, end: today };
        },
      },
      {
        id: 'yesterday',
        label: 'Yesterday',
        getRange: () => {
          const d = new Date();
          d.setDate(d.getDate() - 1);
          return { start: d, end: d };
        },
      },
      {
        id: 'last7days',
        label: 'Last 7 Days',
        getRange: () => {
          const end = new Date();
          const start = new Date();
          start.setDate(end.getDate() - 6);
          return { start, end };
        },
      },
      {
        id: 'last30days',
        label: 'Last 30 Days',
        getRange: () => {
          const end = new Date();
          const start = new Date();
          start.setDate(end.getDate() - 29);
          return { start, end };
        },
      },
      {
        id: 'thisMonth',
        label: 'This Month',
        getRange: () => {
          const start = new Date(now.getFullYear(), now.getMonth(), 1);
          const end = new Date(now.getFullYear(), now.getMonth() + 1, 0);
          return { start, end };
        },
      },
      {
        id: 'lastMonth',
        label: 'Last Month',
        getRange: () => {
          const start = new Date(now.getFullYear(), now.getMonth() - 1, 1);
          const end = new Date(now.getFullYear(), now.getMonth(), 0);
          return { start, end };
        },
      },
      {
        id: 'ytd',
        label: 'Year to Date',
        getRange: () => {
          const start = new Date(now.getFullYear(), 0, 1);
          const end = new Date();
          return { start, end };
        },
      },
    ];
  }
}
