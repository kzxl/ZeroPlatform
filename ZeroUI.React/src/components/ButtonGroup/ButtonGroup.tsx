import React, { useState, useRef, useEffect } from 'react';
import {
  ButtonGroupItem,
  ButtonGroupSelectionMode,
  ButtonGroupSizeMode,
} from '../../core/models/ButtonGroupModel';
import './ButtonGroup.css';

export interface ButtonGroupProps {
  items: ButtonGroupItem[];
  selectionMode?: ButtonGroupSelectionMode;
  sizeMode?: ButtonGroupSizeMode;
  className?: string;
  style?: React.CSSProperties;
  onItemClick?: (item: ButtonGroupItem, index: number) => void;
  onSelectionChange?: (selectedItems: ButtonGroupItem[]) => void;
}

export const ButtonGroup: React.FC<ButtonGroupProps> = ({
  items,
  selectionMode = 'none',
  sizeMode = 'auto-fit',
  className = '',
  style,
  onItemClick,
  onSelectionChange,
}) => {
  const [itemList, setItemList] = useState<ButtonGroupItem[]>(items);
  const [openDropdownId, setOpenDropdownId] = useState<string | null>(null);
  const dropdownRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    setItemList(items);
  }, [items]);

  // Click outside to dismiss dropdown
  useEffect(() => {
    const handleClickOutside = (event: MouseEvent) => {
      if (dropdownRef.current && !dropdownRef.current.contains(event.target as Node)) {
        setOpenDropdownId(null);
      }
    };
    if (openDropdownId) {
      document.addEventListener('mousedown', handleClickOutside);
    }
    return () => {
      document.removeEventListener('mousedown', handleClickOutside);
    };
  }, [openDropdownId]);

  const normalizedSelectionMode = String(selectionMode).toLowerCase().replace('_', '-');
  const normalizedSizeMode = String(sizeMode).toLowerCase().replace('_', '-');

  const handleItemClick = (item: ButtonGroupItem, index: number) => {
    if (item.isEnabled === false || item.type === 'separator') return;

    const executeAction = () => {
      item.onClick?.();
      item.action?.(item);
      onItemClick?.(item, index);
    };

    if (item.type === 'dropdown') {
      setOpenDropdownId((prev) => (prev === item.id ? null : item.id));
      executeAction();
      return;
    }

    if (item.type === 'toggle' || normalizedSelectionMode !== 'none') {
      let updated: ButtonGroupItem[] = [];
      if (normalizedSelectionMode === 'single-select') {
        updated = itemList.map((itm) =>
          itm.id === item.id
            ? { ...itm, isChecked: true, isSelected: true }
            : { ...itm, isChecked: false, isSelected: false }
        );
      } else if (normalizedSelectionMode === 'multi-select') {
        updated = itemList.map((itm) => {
          if (itm.id === item.id) {
            const val = !(itm.isChecked || itm.isSelected);
            return { ...itm, isChecked: val, isSelected: val };
          }
          return itm;
        });
      } else {
        // Independent toggle
        updated = itemList.map((itm) => {
          if (itm.id === item.id) {
            const val = !(itm.isChecked || itm.isSelected);
            return { ...itm, isChecked: val, isSelected: val };
          }
          return itm;
        });
      }
      setItemList(updated);
      onSelectionChange?.(updated.filter((i) => i.isChecked || i.isSelected));
    }

    executeAction();
  };

  const containerClasses = [
    'zero-button-group',
    normalizedSizeMode === 'equal-width' ? 'zero-button-group--equal-width' : '',
    normalizedSizeMode === 'fill' ? 'zero-button-group--fill' : '',
    className,
  ]
    .filter(Boolean)
    .join(' ');

  return (
    <div className={containerClasses} style={style} ref={dropdownRef}>
      {itemList.map((item, index) => {
        if (item.isVisible === false) return null;

        if (item.type === 'separator') {
          return <div key={item.id || index} className="zero-button-group__separator" />;
        }

        const isChecked = Boolean(item.isChecked || item.isSelected);
        const text = item.text ?? item.label;
        const icon = item.iconGlyph ?? item.icon;
        const badgeColor = item.badgeColorHex ?? item.badgeColor;

        const itemClasses = [
          'zero-button-group__item',
          isChecked ? 'zero-button-group__item--checked' : '',
          item.style ? `zero-button-group__item--${item.style}` : '',
        ]
          .filter(Boolean)
          .join(' ');

        const itemCustomStyle: React.CSSProperties = {
          minWidth: item.minWidth ? `${item.minWidth}px` : undefined,
          paddingLeft: item.customPaddingHorizontal ? `${item.customPaddingHorizontal}px` : undefined,
          paddingRight: item.customPaddingHorizontal ? `${item.customPaddingHorizontal}px` : undefined,
          backgroundColor: !isChecked ? item.customBackColorHex : undefined,
          color: !isChecked ? item.customForeColorHex : undefined,
        };

        const hasDropdown = item.type === 'dropdown' && item.dropDownItems && item.dropDownItems.length > 0;
        const isDropdownOpen = openDropdownId === item.id;

        return (
          <div key={item.id} className="zero-button-group__dropdown-wrapper">
            <button
              type="button"
              className={itemClasses}
              style={itemCustomStyle}
              title={item.tooltip}
              disabled={item.isEnabled === false}
              onClick={() => handleItemClick(item, index)}
            >
              {icon && <span>{icon}</span>}
              {text && <span>{text}</span>}
              {item.type === 'dropdown' && <span style={{ fontSize: '11px' }}>▾</span>}

              {/* Badge Dot */}
              {item.showBadgeDot && (
                <span
                  className="zero-button-group__dot"
                  style={{ backgroundColor: badgeColor }}
                />
              )}

              {/* Badge Pill */}
              {item.badgeText && (
                <span
                  className="zero-button-group__badge"
                  style={{ backgroundColor: badgeColor }}
                >
                  {item.badgeText}
                </span>
              )}
            </button>

            {/* Dropdown Menu Popup */}
            {hasDropdown && isDropdownOpen && (
              <div className="zero-button-group__dropdown-menu">
                {item.dropDownItems!.map((sub) => (
                  <button
                    key={sub.id}
                    type="button"
                    className={`zero-button-group__dropdown-item ${
                      sub.isDanger || sub.danger ? 'zero-button-group__dropdown-item--danger' : ''
                    }`}
                    onClick={() => {
                      sub.onClick?.();
                      setOpenDropdownId(null);
                    }}
                  >
                    {sub.icon && <span>{sub.icon}</span>}
                    <span>{sub.text ?? sub.label}</span>
                  </button>
                ))}
              </div>
            )}
          </div>
        );
      })}
    </div>
  );
};
