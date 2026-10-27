import React from 'react';

/**
 * Headless model and utilities for high-density enterprise DataGrid.
 */

export type SortDirection = 'asc' | 'desc' | null;

export interface SortState {
  columnKey: string;
  direction: 'asc' | 'desc';
}

export interface ColumnDef<T> {
  key: string;
  title: string;
  width?: number | string;
  sortable?: boolean;
  align?: 'left' | 'center' | 'right';
  render?: (val: any, row: T, index: number) => React.ReactNode;
}

export class DataGridEngine {
  /**
   * Sorts array of records based on sort state with type safety.
   */
  public static sortRows<T extends Record<string, any>>(
    rows: T[],
    sortState: SortState | null
  ): T[] {
    if (!sortState || !sortState.direction) return rows;

    const { columnKey, direction } = sortState;
    const modifier = direction === 'asc' ? 1 : -1;

    return [...rows].sort((a, b) => {
      const valA = a[columnKey];
      const valB = b[columnKey];

      if (valA === valB) return 0;
      if (valA === null || valA === undefined) return 1;
      if (valB === null || valB === undefined) return -1;

      if (typeof valA === 'number' && typeof valB === 'number') {
        return (valA - valB) * modifier;
      }

      if (valA instanceof Date && valB instanceof Date) {
        return (valA.getTime() - valB.getTime()) * modifier;
      }

      return String(valA).localeCompare(String(valB)) * modifier;
    });
  }

  /**
   * Global search across all string/number columns.
   */
  public static filterRows<T extends Record<string, any>>(
    rows: T[],
    searchTerm: string,
    columns: ColumnDef<T>[]
  ): T[] {
    const q = searchTerm.trim().toLowerCase();
    if (!q) return rows;

    const searchableKeys = columns.map((c) => c.key);

    return rows.filter((row) =>
      searchableKeys.some((key) => {
        const val = row[key];
        if (val === null || val === undefined) return false;
        return String(val).toLowerCase().includes(q);
      })
    );
  }

  /**
   * Client-side pagination.
   */
  public static paginateRows<T>(rows: T[], page: number, pageSize: number): T[] {
    const start = Math.max(0, (page - 1) * pageSize);
    return rows.slice(start, start + pageSize);
  }
}
