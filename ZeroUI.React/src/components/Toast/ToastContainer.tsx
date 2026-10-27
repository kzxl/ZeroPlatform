import React from 'react';
import { useToast, ToastType } from '../../core/notifications/ToastContext';
import { MenuIcons } from '../../core/icons/MenuIcons';
import './Toast.css';

const getToastIcon = (type: ToastType) => {
  switch (type) {
    case 'success':
      return '✅';
    case 'warning':
      return '⚠️';
    case 'error':
      return '❌';
    case 'info':
    default:
      return 'ℹ️';
  }
};

export const ToastContainer: React.FC = () => {
  const { toasts, removeToast } = useToast();

  if (toasts.length === 0) return null;

  return (
    <div className="zero-toast-container">
      {toasts.map((t) => (
        <div key={t.id} className={`zero-toast-card toast-${t.type}`}>
          <span className="zero-toast-icon">{getToastIcon(t.type)}</span>
          <div className="zero-toast-content">
            <span className="zero-toast-title">{t.title}</span>
            {t.message && <span className="zero-toast-message">{t.message}</span>}
          </div>
          <button
            type="button"
            className="zero-toast-close"
            onClick={() => removeToast(t.id)}
          >
            {MenuIcons.Close}
          </button>
        </div>
      ))}
    </div>
  );
};
