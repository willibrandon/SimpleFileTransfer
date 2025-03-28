import { useState, useEffect } from 'react'
import { useWebSocket } from '../../WebSocketContext'

// Define styles as a constant
const styles = {
  statusMessage: {
    padding: '10px',
    borderRadius: '4px',
    marginBottom: '15px',
    textAlign: 'center'
  },
  error: {
    backgroundColor: 'rgba(231, 76, 60, 0.1)',
    color: '#e74c3c',
    border: '1px solid rgba(231, 76, 60, 0.3)'
  },
  success: {
    backgroundColor: 'rgba(46, 204, 113, 0.1)',
    color: '#2ecc71',
    border: '1px solid rgba(46, 204, 113, 0.3)'
  },
  info: {
    backgroundColor: 'rgba(52, 152, 219, 0.1)',
    color: '#3498db',
    border: '1px solid rgba(52, 152, 219, 0.3)'
  }
};

export function ServerControlPanel({ isRunning, port }) {
  const [config, setConfig] = useState({
    port: port || 9876,
    downloadsDirectory: '',
    useEncryption: false,
    password: ''
  })
  const [statusMessage, setStatusMessage] = useState({ type: '', message: '' })
  const [localIsRunning, setLocalIsRunning] = useState(isRunning)
  const [isInitialized, setIsInitialized] = useState(false)
  const [currentPort, setCurrentPort] = useState(port || 9876)
  const [ipAddress, setIpAddress] = useState('')
  
  const { connected, serverStatus } = useWebSocket()
  
  // Update local state when props change
  useEffect(() => {
    setLocalIsRunning(isRunning);
  }, [isRunning]);
  
  // Update port from serverStatus
  useEffect(() => {
    if (serverStatus && serverStatus.port) {
      setCurrentPort(serverStatus.port);
    }
  }, [serverStatus]);
  
  // Get IP address when server is running
  useEffect(() => {
    const getIpAddress = async () => {
      try {
        // Try to get IP from the server API first
        const response = await fetch('/api/server/ip');
        if (response.ok) {
          const data = await response.json();
          if (data.ip) {
            setIpAddress(data.ip);
            return;
          }
        }
        
        // Fallback to a public service that returns the client's IP
        const publicIpResponse = await fetch('https://api.ipify.org?format=json');
        if (publicIpResponse.ok) {
          const data = await publicIpResponse.json();
          setIpAddress(data.ip);
        }
      } catch (error) {
        console.warn('Could not get IP address:', error);
        setIpAddress('Could not determine IP address');
      }
    };
    
    if (localIsRunning) {
      getIpAddress();
    } else {
      setIpAddress('');
    }
  }, [localIsRunning]);
  
  // Check server status on initial load
  useEffect(() => {
    const checkInitialStatus = async () => {
      // First check if we already have the status from WebSocket
      if (serverStatus && serverStatus.isRunning !== undefined) {
        setLocalIsRunning(serverStatus.isRunning);
        if (serverStatus.port) {
          setCurrentPort(serverStatus.port);
        }
        setIsInitialized(true);
        return;
      }
      
      // If not, check via API
      try {
        const response = await fetch('/api/server/status');
        if (response.ok) {
          const data = await response.json();
          setLocalIsRunning(data.isRunning);
          if (data.port) {
            setCurrentPort(data.port);
          }
          setIsInitialized(true);
          return data.isRunning;
        }
      } catch (error) {
        console.error('Error checking server status:', error);
      }
      
      setIsInitialized(true);
      return false;
    };
    
    if (!isInitialized) {
      checkInitialStatus();
    }
  }, [serverStatus, isInitialized]);
  
  // Update from WebSocket server status
  useEffect(() => {
    if (serverStatus && serverStatus.isRunning !== undefined && isInitialized) {
      setLocalIsRunning(serverStatus.isRunning);
    }
  }, [serverStatus, isInitialized]);
  
  // Check server status before starting
  const getServerStatus = async () => {
    try {
      const response = await fetch('/api/server/status');
      if (response.ok) {
        const data = await response.json();
        setLocalIsRunning(data.isRunning);
        return data.isRunning;
      }
      return false;
    } catch (error) {
      console.error('Error checking server status:', error);
      return false;
    }
  };
  
  // Clear status message after a delay
  const clearStatusMessage = () => {
    setTimeout(() => {
      setStatusMessage({ type: '', message: '' });
    }, 3000);
  };
  
  // Handle starting the server
  const handleStartServer = async () => {
    // Clear any previous status messages
    setStatusMessage({ type: '', message: '' });
    
    // Check if server is already running
    const serverRunning = await getServerStatus();
    if (serverRunning) {
      return;
    }
    
    try {
      const response = await fetch('/api/server/start', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json'
        },
        body: JSON.stringify(config)
      })
      
      if (!response.ok) {
        const errorData = await response.json()
        
        // Handle "Server is already running" error gracefully
        if (errorData.error === "Server is already running") {
          setLocalIsRunning(true);
          return;
        }
        
        throw new Error(errorData.error || 'Failed to start server')
      }
      
      // Update local state
      setLocalIsRunning(true);
    } catch (error) {
      // Show error message without console logging
      setStatusMessage({ 
        type: 'error', 
        message: `Error: ${error.message}` 
      });
      clearStatusMessage();
    }
  }
  
  // Handle stopping the server
  const handleStopServer = async () => {
    // Clear any previous status messages
    setStatusMessage({ type: '', message: '' });
    
    try {
      const response = await fetch('/api/server/stop', {
        method: 'POST'
      })
      
      if (!response.ok) {
        const errorData = await response.json()
        throw new Error(errorData.error || 'Failed to stop server')
      }
      
      // Parse the response
      const data = await response.json();
      
      // Update local state based on the response
      setLocalIsRunning(data.isRunning === true);
    } catch (error) {
      // Show error message without console logging
      setStatusMessage({ 
        type: 'error', 
        message: `Error: ${error.message}` 
      });
      clearStatusMessage();
    }
  }
  
  // Handle saving server configuration
  const handleSaveConfig = async () => {
    // Clear any previous status messages
    setStatusMessage({ type: '', message: '' });
    
    try {
      const response = await fetch('/api/server/config', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json'
        },
        body: JSON.stringify(config)
      })
      
      if (!response.ok) {
        const errorData = await response.json()
        throw new Error(errorData.error || 'Failed to save configuration')
      }
    } catch (error) {
      // Show error message without console logging
      setStatusMessage({ 
        type: 'error', 
        message: `Error: ${error.message}` 
      });
      clearStatusMessage();
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
  
  // Get combined style for status message
  const getStatusMessageStyle = (type) => {
    return {
      ...styles.statusMessage,
      ...(type === 'error' ? styles.error : 
         type === 'success' ? styles.success : 
         type === 'info' ? styles.info : {})
    };
  };
  
  // Use the local state for rendering
  const displayIsRunning = localIsRunning;
  
  return (
    <div className="server-control-panel">
      <h2>Server Control</h2>
      
      {/* Server status indicator */}
      <div className="connection-indicator">
        <div className={`indicator-dot ${displayIsRunning ? 'connected' : 'disconnected'}`}></div>
        <span>{displayIsRunning ? 'Running' : 'Stopped'}</span>
      </div>
      
      <div className="server-status">
        {displayIsRunning ? (
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
      
      {displayIsRunning && (
        <div className="server-info">
          {ipAddress ? (
            <p className="server-address">Server is running at <strong>{ipAddress}:{currentPort}</strong></p>
          ) : (
            <p>Server is running on port {currentPort}</p>
          )}
          <p>Share this address with others to receive files</p>
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
          disabled={displayIsRunning}
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
          disabled={displayIsRunning}
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
            disabled={displayIsRunning}
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
            disabled={displayIsRunning}
          />
        </div>
      )}
      
      <button 
        className="save-button"
        onClick={handleSaveConfig}
        disabled={displayIsRunning}
      >
        Save Configuration
      </button>
      
      {/* Only show error messages */}
      {statusMessage.type === 'error' && statusMessage.message && (
        <div className="toast-container">
          <div className={`toast-message error visible`}>
            {statusMessage.message}
          </div>
        </div>
      )}
    </div>
  )
} 