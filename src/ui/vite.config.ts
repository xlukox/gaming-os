import react from '@vitejs/plugin-react';
import { defineConfig } from 'vitest/config';

export default defineConfig({
  // Relative paths: the host serves the built files from a local folder, not from a web root.
  base: './',
  plugins: [react()],
});
