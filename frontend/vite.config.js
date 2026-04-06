import { sveltekit } from '@sveltejs/kit/vite';
import { defineConfig } from 'vite';

export default defineConfig({
	plugins: [sveltekit()],
	server: {
		host: '0.0.0.0',
		port: 7270,
		proxy: {
			'/api': {
				target: 'http://192.168.1.132:27272',
				changeOrigin: true
			}
		}
	}
});
