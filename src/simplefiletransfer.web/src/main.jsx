import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import './index.css'
import './themes.css'
import App from './App.jsx'
import { ThemeProvider } from './ThemeContext'
import { WebSocketProvider } from './WebSocketContext'

createRoot(document.getElementById('root')).render(
  <StrictMode>
    <ThemeProvider>
      <WebSocketProvider>
        <App />
      </WebSocketProvider>
    </ThemeProvider>
  </StrictMode>,
)
