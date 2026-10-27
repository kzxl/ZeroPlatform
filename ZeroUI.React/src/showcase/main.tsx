import React from 'react';
import ReactDOM from 'react-dom/client';
import { ThemeProvider } from '../core/theme/ThemeProvider';
import { App } from './App';

ReactDOM.createRoot(document.getElementById('root')!).render(
  <React.StrictMode>
    <ThemeProvider initialMode="dark">
      <App />
    </ThemeProvider>
  </React.StrictMode>
);
