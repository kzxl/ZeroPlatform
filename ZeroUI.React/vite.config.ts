import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';
import { resolve } from 'path';

// https://vitejs.dev/config/
export default defineConfig(({ mode }) => {
  if (mode === 'lib') {
    return {
      plugins: [react()],
      build: {
        emptyOutDir: false,
        lib: {
          entry: resolve(__dirname, 'src/index.ts'),
          name: 'ZeroUIReact',
          fileName: (format) => `index.${format}.js`,
        },
        rollupOptions: {
          external: ['react', 'react-dom'],
          output: {
            globals: {
              react: 'React',
              'react-dom': 'ReactDOM',
            },
          },
        },
      },
    };
  }

  return {
    plugins: [react()],
    build: {
      emptyOutDir: false,
    },
    server: {
      port: 3000,
      open: false,
    },
  };
});
