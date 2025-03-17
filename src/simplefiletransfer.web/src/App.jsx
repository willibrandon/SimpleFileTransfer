import { useState, useEffect } from 'react'
import './App.css'
import { ThemeIndicator } from './ThemeIndicator'
import { ModeSelector } from './components/ModeSelector'
import { ServerView } from './components/server/ServerView'
import { ClientView } from './components/client/ClientView'

function App() {
  const [mode, setMode] = useState('client')
  
  // Load the last selected mode from localStorage
  useEffect(() => {
    const savedMode = localStorage.getItem('selectedMode')
    if (savedMode) {
      setMode(savedMode)
    }
  }, [])
  
  // Save the selected mode to localStorage
  const handleModeChange = (newMode) => {
    setMode(newMode)
    localStorage.setItem('selectedMode', newMode)
  }

  return (
    <div className="app-container">
      <header>
        <h1>Simple File Transfer</h1>
        <p>Transfer files with optional compression and encryption</p>
        <ModeSelector onModeChange={handleModeChange} />
      </header>

      <main>
        {mode === 'server' ? <ServerView /> : <ClientView />}
      </main>

      <footer>
        <p>SimpleFileTransfer &copy; {new Date().getFullYear()}</p>
      </footer>
      
      <ThemeIndicator />
    </div>
  )
}

export default App
