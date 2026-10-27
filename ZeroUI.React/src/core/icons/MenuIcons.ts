/**
 * Standardized icon glyphs for menus, toolbars, buttons, and action strips across ZeroUI.
 * Provides consistent, crisp visual symbols optimized for standard typography and emoji rendering.
 */
export const MenuIcons = {
  // --- CRUD & Actions ---
  Add: '➕',
  Plus: '➕',
  Edit: '✏',
  Rename: '✏',
  Delete: '🗑',
  Trash: '🗑',
  Save: '💾',
  Refresh: '🔄',
  Search: '🔍',
  Filter: '🔍',
  Clear: '✖',
  Close: '✖',

  // --- Clipboard & History ---
  Copy: '📋',
  Cut: '✂',
  Paste: '📄',
  Duplicate: '📑',
  Undo: '↩',
  Redo: '↪',

  // --- File & Data Transfer ---
  Document: '📄',
  Folder: '📁',
  Export: '📤',
  Import: '📥',
  Print: '🖨',
  Download: '⬇',
  Upload: '⬆',

  // --- Flow, Canvas & Industrial ---
  Frame: '📦',
  Swimlane: '📦',
  Flow: '📐',
  FitToContent: '🎯',
  AutoLayout: '📐',
  Workflow: '🔀',
  Connect: '🔗',
  Link: '🔗',
  Unlink: '⛓',
  Shape: '🔷',
  Palette: '🎨',
  Play: '▶',
  Stop: '⏹',
  Data: '📊',

  // --- View & Status ---
  ZoomIn: '🔍',
  ZoomOut: '🔎',
  ZoomFit: '🎯',
  Fullscreen: '⛶',
  Preview: '👁',
  View: '👁',
  Settings: '⚙',
  Info: 'ℹ',
  Warning: '⚠',
  Success: '✔',
  Lock: '🔒',
  Unlock: '🔓',

  // --- Directional & Navigation ---
  ArrowRight: '➜',
  ArrowLeft: '←',
  ArrowUp: '↑',
  ArrowDown: '↓',
  DropDown: '▾',

  /**
   * Formats menu item header text by prepending the specified icon glyph with standard spacing.
   */
  format(icon?: string | null, text?: string): string {
    if (!icon) return text || '';
    return `${icon} ${text || ''}`.trim();
  },
} as const;

export type MenuIconType = keyof typeof MenuIcons;
