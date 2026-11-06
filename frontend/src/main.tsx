// The page's entry point. The host embeds the built page; the page's views are components under App.

import { createRoot } from 'react-dom/client';
import './app.css';
import { App } from './components/App';
import { reportFrontendError } from './bridge';

window.addEventListener('error', event => reportFrontendError(event.error ?? event.message));
window.addEventListener('unhandledrejection', event => reportFrontendError(event.reason));

createRoot(document.getElementById('view')!).render(<App />);
