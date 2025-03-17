import { useState } from 'react'
import { useWebSocket } from '../../WebSocketContext'

export function ServerControlPanel({ isRunning, port }) {
  const [config, setConfig] = useState({
    port: port || 9876,
    downloadsDirectory: '',
    useEncryption: false,
    password: ''
  })
  
  const { connected } = useWebSocket()
  
  // Handle starting the server
  const handleStartServer = async () => {
    try {
      const response = await fetch('/api/server/start', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json'
        },
        body: JSON.stringify(config)
      })
      
      if (!response.ok) {
        const error = await response.json()
        throw new Error(error.error || 'Failed to start server')
      }
    } catch (error) {
      console.error('Error starting server:', error)
      alert(`Error starting server: ${error.message}`)
    }
  }
  
  // Handle stopping the server
  const handleStopServer = async () => {
    try {
      const response = await fetch('/api/server/stop', {
        method: 'POST'
      })
      
      if (!response.ok) {
        const error = await response.json()
        throw new Error(error.error || 'Failed to stop server')
      }
    } catch (error) {
      console.error('Error stopping server:', error)
      alert(`Error stopping server: ${error.message}`)
    }
  }
  
  // Handle saving server configuration
  const handleSaveConfig = async () => {
    try {
      const response = await fetch('/api/server/config', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json'
        },
        body: JSON.stringify(config)
      })
      
      if (!response.ok) {
        const error = await response.json()
        throw new Error(error.error || 'Failed to save configuration')
      }
      
      alert('Configuration saved successfully')
    } catch (error) {
      console.error('Error saving configuration:', error)
      alert(`Error saving configuration: ${error.message}`)
    }
  }
  
  // Handle input changes
  const handleChange = (e) => {
    const { name, value, type, checked } = e.target
    setConfig(prev => ({
      ...prev,
      [name]: type === 'checkbox' ? checked : value
    }))
  }
  
  return (
    <div className="server-control-panel">
      <h2>Server Control</h2>
      
      <div className="server-status">
        <div>
          <span>Status: </span>
          <span className={`status-indicator ${isRunning ? 'active' : 'inactive'}`}>
            {isRunning ? 'Running' : 'Stopped'}
          </span>
        </div>
        
        {isRunning ? (
          <button 
            className="control-button stop"
            onClick={handleStopServer}
          >
            Stop Server
          </button>
        ) : (
          <button 
            className="control-button start"
            onClick={handleStartServer}
          >
            Start Server
          </button>
        )}
      </div>
      
      {isRunning && (
        <div className="server-info">
          <p>Server is running on port {port}</p>
          <p>Share your IP address with others to receive files</p>
        </div>
      )}
      
      <h3>Server Configuration</h3>
      <div className="form-group">
        <label htmlFor="port">Port</label>
        <input
          type="number"
          id="port"
          name="port"
          value={config.port}
          onChange={handleChange}
          disabled={isRunning}
        />
      </div>
      
      <div className="form-group">
        <label htmlFor="downloadsDirectory">Downloads Directory</label>
        <input
          type="text"
          id="downloadsDirectory"
          name="downloadsDirectory"
          value={config.downloadsDirectory}
          onChange={handleChange}
          placeholder="Default: ./downloads (in application directory)"
          disabled={isRunning}
        />
      </div>
      
      <div className="form-group">
        <div className="checkbox-group">
          <input
            type="checkbox"
            id="useEncryption"
            name="useEncryption"
            checked={config.useEncryption}
            onChange={handleChange}
            disabled={isRunning}
          />
          <label htmlFor="useEncryption">Require Encryption</label>
        </div>
      </div>
      
      {config.useEncryption && (
        <div className="form-group">
          <label htmlFor="password">Password</label>
          <input
            type="password"
            id="password"
            name="password"
            value={config.password}
            onChange={handleChange}
            disabled={isRunning}
          />
        </div>
      )}
      
      <button 
        className="save-button"
        onClick={handleSaveConfig}
        disabled={isRunning}
      >
        Save Configuration
      </button>
    </div>
  )
} 