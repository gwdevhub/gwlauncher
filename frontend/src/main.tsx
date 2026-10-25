// The page's entry point. The host embeds the built page; the page's views are components under App.

import { createRoot } from 'react-dom/client';
import './app.css';
import { App } from './components/App';
import { call } from './bridge';

function reportFailure(error: unknown) {
    call('frontendError', `The launcher interface failed: ${String(error)}`).catch(() => undefined);
}

window.addEventListener('error', event => reportFailure(event.error ?? event.message));
window.addEventListener('unhandledrejection', event => reportFailure(event.reason));

createRoot(document.getElementById('view')!).render(<App />);
