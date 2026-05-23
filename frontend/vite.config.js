import { sveltekit } from '@sveltejs/kit/vite';
import { defineConfig } from 'vite';

export default defineConfig({
	plugins: [sveltekit()],
	server: {
		host: '0.0.0.0',
		port: 5173,
		proxy: {
			'/api': {
				target: 'http://127.0.0.1:27272',
				changeOrigin: true
			},
			'/ws': {
				target: 'ws://127.0.0.1:62727',
				ws: true
			}
		}
	}
});

