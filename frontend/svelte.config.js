import adapter from '@sveltejs/adapter-static';
import { vitePreprocess } from '@sveltejs/vite-plugin-svelte';
import path from 'path';
import { fileURLToPath } from 'url';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

const config = {
  preprocess: vitePreprocess(),
  kit: {
    adapter: adapter({
      fallback: 'index.html', // для SPA-роутинга
      pages: path.resolve(__dirname, '../frontBuild'),
      assets: path.resolve(__dirname, '../frontBuild'),
    }),
    prerender: {
      entries: ['*']
    }
  }
};

export default config;