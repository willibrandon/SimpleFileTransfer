import { useState } from 'react'
import './App.css'
import { ThemeIndicator } from './ThemeIndicator'

function App() {
  const [sourceFile, setSourceFile] = useState('')
  const [destinationFile, setDestinationFile] = useState('')
  const [isCompressed, setIsCompressed] = useState(false)
  const [isEncrypted, setIsEncrypted] = useState(false)
  const [password, setPassword] = useState('')
  const [transferStatus, setTransferStatus] = useState('')
  const [isTransferring, setIsTransferring] = useState(false)

  const handleTransfer = async () => {
    if (!sourceFile || !destinationFile) {
      setTransferStatus('Please provide both source and destination paths')
      return
    }

    if (isEncrypted && !password) {
      setTransferStatus('Password is required for encryption')
      return
    }

    setIsTransferring(true)
    setTransferStatus('Transferring file...')

    try {
      // This would be replaced with actual API call to backend
      const response = await fetch('/api/transfer', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify({
          sourcePath: sourceFile,
          destinationPath: destinationFile,
          compress: isCompressed,
          encrypt: isEncrypted,
          password: isEncrypted ? password : undefined
        }),
      })

      const data = await response.json()
      
      if (response.ok) {
        setTransferStatus(`Transfer completed successfully! ${data.message || ''}`)
      } else {
        setTransferStatus(`Error: ${data.error || 'Unknown error occurred'}`)
      }
    } catch (error) {
      setTransferStatus(`Error: ${error.message || 'Failed to connect to server'}`)
    } finally {
      setIsTransferring(false)
    }
  }

  return (
    <div className="app-container">
      <header>
        <h1>Simple File Transfer</h1>
        <p>Transfer files with optional compression and encryption</p>
      </header>

      <main>
        <div className="form-group">
          <label htmlFor="sourceFile">Source File Path:</label>
          <input
            type="text"
            id="sourceFile"
            value={sourceFile}
            onChange={(e) => setSourceFile(e.target.value)}
            placeholder="C:\path\to\source\file.txt"
            disabled={isTransferring}
          />
        </div>

        <div className="form-group">
          <label htmlFor="destinationFile">Destination File Path:</label>
          <input
            type="text"
            id="destinationFile"
            value={destinationFile}
            onChange={(e) => setDestinationFile(e.target.value)}
            placeholder="C:\path\to\destination\file.txt"
            disabled={isTransferring}
          />
        </div>

        <div className="options">
          <div className="checkbox-group">
            <input
              type="checkbox"
              id="compress"
              checked={isCompressed}
              onChange={() => setIsCompressed(!isCompressed)}
              disabled={isTransferring}
            />
            <label htmlFor="compress">Compress</label>
          </div>

          <div className="checkbox-group">
            <input
              type="checkbox"
              id="encrypt"
              checked={isEncrypted}
              onChange={() => setIsEncrypted(!isEncrypted)}
              disabled={isTransferring}
            />
            <label htmlFor="encrypt">Encrypt</label>
          </div>
        </div>

        {isEncrypted && (
          <div className="form-group">
            <label htmlFor="password">Password:</label>
            <input
              type="password"
              id="password"
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              disabled={isTransferring}
            />
          </div>
        )}

        <button 
          className="transfer-button" 
          onClick={handleTransfer}
          disabled={isTransferring}
        >
          {isTransferring ? 'Transferring...' : 'Transfer File'}
        </button>

        {transferStatus && (
          <div className={`status ${transferStatus.includes('Error') ? 'error' : transferStatus.includes('completed') ? 'success' : ''}`}>
            {transferStatus}
          </div>
        )}
      </main>

      <footer>
        <p>SimpleFileTransfer &copy; {new Date().getFullYear()}</p>
      </footer>
      
      <ThemeIndicator />
    </div>
  )
}

export default App
