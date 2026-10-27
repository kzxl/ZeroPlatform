import React, { useEffect, useRef } from 'react';
import './ContextMenu.css';

export interface ContextMenuItem {
  id: string;
  text?: string;
  label?: string;
  icon?: string;
  shortcut?: string;
  isDanger?: boolean;
  danger?: boolean;
  isSeparator?: boolean;
  disabled?: boolean;
  onClick?: () => void;
}

export interface ContextMenuProps {
  isOpen: boolean;
  x: number;
  y: number;
  items: ContextMenuItem[];
  onClose: () => void;
}

export const ContextMenu: React.FC<ContextMenuProps> = ({
  isOpen,
  x,
  y,
  items,
  onClose,
}) => {
  const menuRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    const handleOutsideClick = (e: MouseEvent) => {
      if (menuRef.current && !menuRef.current.contains(e.target as Node)) {
        onClose();
      }
    };

    const handleKeyDown = (e: KeyboardEvent) => {
      if (e.key === 'Escape') {
        onClose();
      }
    };

    if (isOpen) {
      document.addEventListener('mousedown', handleOutsideClick);
      document.addEventListener('keydown', handleKeyDown);
    }

    return () => {
      document.removeEventListener('mousedown', handleOutsideClick);
      document.removeEventListener('keydown', handleKeyDown);
    };
  }, [isOpen, onClose]);

  // Adjust coordinates to not clip beyond viewport
  const adjustedStyle: React.CSSProperties = {
    top: `${Math.min(y, window.innerHeight - 240)}px`,
    left: `${Math.min(x, window.innerWidth - 220)}px`,
  };

  if (!isOpen) return null;

  return (
    <div className="zero-context-menu" style={adjustedStyle} ref={menuRef}>
      {items.map((item, index) => {
        if (item.isSeparator) {
          return <div key={item.id || index} className="zero-context-menu__separator" />;
        }

        const isDanger = item.isDanger || item.danger;
        const text = item.text ?? item.label;

        const itemClasses = [
          'zero-context-menu__item',
          isDanger ? 'zero-context-menu__item--danger' : '',
        ]
          .filter(Boolean)
          .join(' ');

        return (
          <button
            key={item.id}
            type="button"
            className={itemClasses}
            disabled={item.disabled}
            onClick={() => {
              item.onClick?.();
              onClose();
            }}
          >
            <span className="zero-context-menu__item-left">
              {item.icon && <span className="zero-context-menu__item-icon">{item.icon}</span>}
              <span>{text}</span>
            </span>
            {item.shortcut && (
              <span className="zero-context-menu__item-shortcut">{item.shortcut}</span>
            )}
          </button>
        );
      })}
    </div>
  );
};
