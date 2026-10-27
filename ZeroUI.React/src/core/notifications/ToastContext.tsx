import React, { createContext, useContext, useState, useCallback, useMemo } from 'react';

export type ToastType = 'success' | 'warning' | 'error' | 'info';

export interface ToastItem {
  id: string;
  type: ToastType;
  title: string;
  message?: string;
  duration?: number;
}

interface ToastContextValue {
  toasts: ToastItem[];
  addToast: (toast: Omit<ToastItem, 'id'>) => string;
  removeToast: (id: string) => void;
  toast: {
    success: (title: string, message?: string, duration?: number) => string;
    warning: (title: string, message?: string, duration?: number) => string;
    error: (title: string, message?: string, duration?: number) => string;
    info: (title: string, message?: string, duration?: number) => string;
  };
}

const ToastContext = createContext<ToastContextValue | null>(null);

export const useToast = (): ToastContextValue => {
  const ctx = useContext(ToastContext);
  if (!ctx) {
    throw new Error('useToast must be used within a ToastProvider');
  }
  return ctx;
};

export interface ToastProviderProps {
  children: React.ReactNode;
}

export const ToastProvider: React.FC<ToastProviderProps> = ({ children }) => {
  const [toasts, setToasts] = useState<ToastItem[]>([]);

  const removeToast = useCallback((id: string) => {
    setToasts((prev) => prev.filter((t) => t.id !== id));
  }, []);

  const addToast = useCallback(
    (toastData: Omit<ToastItem, 'id'>) => {
      const id = `toast_${Date.now()}_${Math.random().toString(36).substring(2, 6)}`;
      const duration = toastData.duration ?? 3500;

      const newToast: ToastItem = { ...toastData, id, duration };
      setToasts((prev) => [...prev, newToast]);

      if (duration > 0) {
        setTimeout(() => {
          removeToast(id);
        }, duration);
      }

      return id;
    },
    [removeToast]
  );

  const toastHelpers = useMemo(
    () => ({
      success: (title: string, message?: string, duration?: number) =>
        addToast({ type: 'success', title, message, duration }),
      warning: (title: string, message?: string, duration?: number) =>
        addToast({ type: 'warning', title, message, duration }),
      error: (title: string, message?: string, duration?: number) =>
        addToast({ type: 'error', title, message, duration }),
      info: (title: string, message?: string, duration?: number) =>
        addToast({ type: 'info', title, message, duration }),
    }),
    [addToast]
  );

  const value = useMemo(
    () => ({
      toasts,
      addToast,
      removeToast,
      toast: toastHelpers,
    }),
    [toasts, addToast, removeToast, toastHelpers]
  );

  return <ToastContext.Provider value={value}>{children}</ToastContext.Provider>;
};
