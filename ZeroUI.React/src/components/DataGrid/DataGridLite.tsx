import { useState, useMemo } from 'react';
import {
  ColumnDef,
  SortState,
  DataGridEngine,
} from '../../core/models/DataGridModel';
import { MenuIcons } from '../../core/icons/MenuIcons';
import './DataGridLite.css';

export interface DataGridLiteProps<T extends Record<string, any>> {
  data: T[];
  columns: ColumnDef<T>[];
  rowKey: keyof T | ((row: T) => string);
  pageSize?: number;
  selectable?: boolean;
  selectedKeys?: string[];
  onSelectionChange?: (selectedKeys: string[]) => void;
  className?: string;
}

export function DataGridLite<T extends Record<string, any>>({
  data,
  columns,
  rowKey,
  pageSize = 10,
  selectable = true,
  selectedKeys,
  onSelectionChange,
  className = '',
}: DataGridLiteProps<T>) {
  const [searchTerm, setSearchTerm] = useState<string>('');
  const [sortState, setSortState] = useState<SortState | null>(null);
  const [currentPage, setCurrentPage] = useState<number>(1);
  const [internalSelectedKeys, setInternalSelectedKeys] = useState<string[]>([]);

  const activeSelectedKeys = selectedKeys ?? internalSelectedKeys;

  const getRowId = (row: T, idx: number): string => {
    if (typeof rowKey === 'function') return rowKey(row);
    return row[rowKey] !== undefined ? String(row[rowKey]) : String(idx);
  };

  // 1. Filter
  const filteredData = useMemo(() => {
    return DataGridEngine.filterRows(data, searchTerm, columns);
  }, [data, searchTerm, columns]);

  // 2. Sort
  const sortedData = useMemo(() => {
    return DataGridEngine.sortRows(filteredData, sortState);
  }, [filteredData, sortState]);

  // 3. Paginate
  const totalPages = Math.max(1, Math.ceil(sortedData.length / pageSize));
  const paginatedData = useMemo(() => {
    return DataGridEngine.paginateRows(sortedData, currentPage, pageSize);
  }, [sortedData, currentPage, pageSize]);

  // Sorting Handler
  const handleSort = (columnKey: string) => {
    setSortState((prev) => {
      if (!prev || prev.columnKey !== columnKey) {
        return { columnKey, direction: 'asc' };
      }
      if (prev.direction === 'asc') {
        return { columnKey, direction: 'desc' };
      }
      return null;
    });
  };

  // Selection Handlers
  const handleSelectAll = (checked: boolean) => {
    if (checked) {
      const allIds = sortedData.map((row, i) => getRowId(row, i));
      if (!selectedKeys) setInternalSelectedKeys(allIds);
      onSelectionChange?.(allIds);
    } else {
      if (!selectedKeys) setInternalSelectedKeys([]);
      onSelectionChange?.([]);
    }
  };

  const handleSelectRow = (id: string, checked: boolean) => {
    const next = checked
      ? [...activeSelectedKeys, id]
      : activeSelectedKeys.filter((k) => k !== id);

    if (!selectedKeys) setInternalSelectedKeys(next);
    onSelectionChange?.(next);
  };

  const isAllSelected =
    paginatedData.length > 0 &&
    paginatedData.every((row, i) => activeSelectedKeys.includes(getRowId(row, i)));

  return (
    <div className={`zero-datagrid-container ${className}`}>
      {/* Toolbar */}
      <div className="zero-datagrid-toolbar">
        <div className="zero-datagrid-search-wrap">
          <span className="zero-datagrid-search-icon">🔍</span>
          <input
            type="text"
            className="zero-datagrid-search-input"
            placeholder="Search records..."
            value={searchTerm}
            onChange={(e) => {
              setSearchTerm(e.target.value);
              setCurrentPage(1);
            }}
          />
        </div>

        {selectable && activeSelectedKeys.length > 0 && (
          <div className="zero-datagrid-selection-info">
            {activeSelectedKeys.length} of {data.length} selected
          </div>
        )}
      </div>

      {/* Table */}
      <div className="zero-datagrid-table-wrap">
        <table className="zero-datagrid-table">
          <thead>
            <tr>
              {selectable && (
                <th style={{ width: 40, textAlign: 'center' }}>
                  <input
                    type="checkbox"
                    className="zero-datagrid-checkbox"
                    checked={isAllSelected}
                    onChange={(e) => handleSelectAll(e.target.checked)}
                  />
                </th>
              )}
              {columns.map((col) => {
                const isSorted = sortState?.columnKey === col.key;
                const sortDir = isSorted ? sortState.direction : null;

                return (
                  <th
                    key={col.key}
                    style={{ width: col.width, textAlign: col.align || 'left' }}
                    className={col.sortable ? 'is-sortable' : ''}
                    onClick={() => col.sortable && handleSort(col.key)}
                  >
                    {col.title}
                    {col.sortable && (
                      <span className="zero-datagrid-sort-indicator">
                        {sortDir === 'asc' ? ' ▲' : sortDir === 'desc' ? ' ▼' : ' ⇅'}
                      </span>
                    )}
                  </th>
                );
              })}
            </tr>
          </thead>
          <tbody>
            {paginatedData.length === 0 ? (
              <tr>
                <td colSpan={columns.length + (selectable ? 1 : 0)} className="zero-datagrid-empty">
                  No records found matching criteria
                </td>
              </tr>
            ) : (
              paginatedData.map((row, idx) => {
                const id = getRowId(row, idx);
                const isSelected = activeSelectedKeys.includes(id);

                return (
                  <tr key={id} className={isSelected ? 'is-selected' : ''}>
                    {selectable && (
                      <td style={{ textAlign: 'center' }}>
                        <input
                          type="checkbox"
                          className="zero-datagrid-checkbox"
                          checked={isSelected}
                          onChange={(e) => handleSelectRow(id, e.target.checked)}
                        />
                      </td>
                    )}
                    {columns.map((col) => (
                      <td key={col.key} style={{ textAlign: col.align || 'left' }}>
                        {col.render
                          ? col.render(row[col.key], row, idx)
                          : String(row[col.key] ?? '')}
                      </td>
                    ))}
                  </tr>
                );
              })
            )}
          </tbody>
        </table>
      </div>

      {/* Pagination Footer */}
      <div className="zero-datagrid-footer">
        <div>
          Showing {sortedData.length === 0 ? 0 : (currentPage - 1) * pageSize + 1} to{' '}
          {Math.min(currentPage * pageSize, sortedData.length)} of {sortedData.length} entries
        </div>

        <div className="zero-datagrid-pagination-pages">
          <button
            type="button"
            className="zero-datagrid-page-btn"
            disabled={currentPage <= 1}
            onClick={() => setCurrentPage((p) => Math.max(1, p - 1))}
          >
            {MenuIcons.ArrowLeft}
          </button>

          {Array.from({ length: totalPages }).map((_, i) => {
            const page = i + 1;
            return (
              <button
                key={page}
                type="button"
                className={`zero-datagrid-page-btn ${page === currentPage ? 'is-active' : ''}`}
                onClick={() => setCurrentPage(page)}
              >
                {page}
              </button>
            );
          })}

          <button
            type="button"
            className="zero-datagrid-page-btn"
            disabled={currentPage >= totalPages}
            onClick={() => setCurrentPage((p) => Math.min(totalPages, p + 1))}
          >
            {MenuIcons.ArrowRight}
          </button>
        </div>
      </div>
    </div>
  );
}
