export function initErrorLogger() {
    if (typeof window === 'undefined') return;
    window.addEventListener('error', (e) => {
        console.error('[FrontendError]', e.message, e.filename, e.lineno);
    });
    window.addEventListener('unhandledrejection', (e) => {
        console.error('[UnhandledRejection]', e.reason);
    });
}
