// The page's entry point. The host embeds the built page; the page's views are components under App.

import { createRoot } from 'react-dom/client';
import './app.css';
import { App } from './components/App';

createRoot(document.getElementById('view')!).render(<App />);
