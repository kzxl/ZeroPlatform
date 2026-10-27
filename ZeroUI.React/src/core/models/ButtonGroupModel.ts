export type ButtonGroupItemType = 'push' | 'toggle' | 'dropdown' | 'separator';
export type ButtonGroupItemStyle = 'primary' | 'secondary' | 'success' | 'danger' | 'ghost';

export type ButtonGroupSelectionMode =
  | 'none'
  | 'single-select'
  | 'multi-select'
  | 'SingleSelect'
  | 'MultiSelect';

export type ButtonGroupSizeMode =
  | 'auto-fit'
  | 'equal-width'
  | 'fill'
  | 'AutoFit'
  | 'EqualWidth'
  | 'Fill';

export interface ButtonGroupItem {
  id: string;
  text?: string;
  label?: string;
  iconGlyph?: string;
  icon?: string;
  tooltip?: string;
  type?: ButtonGroupItemType;
  style?: ButtonGroupItemStyle;
  isChecked?: boolean;
  isSelected?: boolean;
  isEnabled?: boolean;
  isVisible?: boolean;
  badgeText?: string;
  showBadgeDot?: boolean;
  badgeColor?: string;
  badgeColorHex?: string;
  minWidth?: number;
  customPaddingHorizontal?: number;
  customBackColorHex?: string;
  customForeColorHex?: string;
  dropDownItems?: Array<{
    id: string;
    text?: string;
    label?: string;
    icon?: string;
    isDanger?: boolean;
    danger?: boolean;
    onClick?: () => void;
  }>;
  onClick?: () => void;
  action?: (item: ButtonGroupItem) => void;
  tag?: unknown;
}
