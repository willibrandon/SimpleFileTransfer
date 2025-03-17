import { useState, useEffect } from 'react';
import { serverApi } from '../../api/apiService';

export function ServerControlPanel() {
  const [isRunning, setIsRunning] = useState(false);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState('');
  const [config, setConfig] = useState({
    port: 9876,
    downloadsDirectory: '',
    password: '',
    useEncryption: false
  });

  // Fetch server status and config on component mount
  useEffect(() => {
    const fetchServerData = async () => {
      try {
        setIsLoading(true);
        const statusResponse = await serverApi.getStatus();
        setIsRunning(statusResponse.isRunning);
        
        const configResponse = await serverApi.getConfig();
        setConfig(configResponse);
        
        setError('');
      } catch (err) {
        setError('Failed to fetch server data. The server API might not be available.');
        console.error(err);
      } finally {
        setIsLoading(false);
      }
    };

    fetchServerData();
  }, []);

  const handleStartServer = async () => {
    try {
      setIsLoading(true);
      await serverApi.start(config);
      setIsRunning(true);
      setError('');
    } catch (err) {
      setError('Failed to start server');
      console.error(err);
    } finally {
      setIsLoading(false);
    }
  };

  const handleStopServer = async () => {
    try {
      setIsLoading(true);
      await serverApi.stop();
      setIsRunning(false);
      setError('');
    } catch (err) {
      setError('Failed to stop server');
      console.error(err);
    } finally {
      setIsLoading(false);
    }
  };

  const handleConfigChange = (e) => {
    const { name, value, type, checked } = e.target;
    setConfig({
      ...config,
      [name]: type === 'checkbox' ? checked : value
    });
  };

  const handleSaveConfig = async () => {
    try {
      setIsLoading(true);
      await serverApi.updateConfig(config);
      setError('');
    } catch (err) {
      setError('Failed to update server configuration');
      console.error(err);
    } finally {
      setIsLoading(false);
    }
  };

  if (isLoading && !error) {
    return <div className="loading">Loading server status...</div>;
  }

  return (
    <div className="server-control-panel">
      <h2>Server Control Panel</h2>
      
      {error && <div className="error-message">{error}</div>}
      
      <div className="server-status">
        <div className={`status-indicator ${isRunning ? 'active' : 'inactive'}`}>
          {isRunning ? 'Running' : 'Stopped'}
        </div>
        
        <button 
          className={`control-button ${isRunning ? 'stop' : 'start'}`}
          onClick={isRunning ? handleStopServer : handleStartServer}
          disabled={isLoading}
        >
          {isRunning ? 'Stop Server' : 'Start Server'}
        </button>
      </div>
      
      <div className="server-config">
        <h3>Server Configuration</h3>
        
        <div className="form-group">
          <label htmlFor="port">Port:</label>
          <input
            type="number"
            id="port"
            name="port"
            value={config.port}
            onChange={handleConfigChange}
            disabled={isRunning}
          />
        </div>
        
        <div className="form-group">
          <label htmlFor="downloadsDirectory">Downloads Directory:</label>
          <input
            type="text"
            id="downloadsDirectory"
            name="downloadsDirectory"
            value={config.downloadsDirectory}
            onChange={handleConfigChange}
            disabled={isRunning}
            placeholder="C:\path\to\downloads"
          />
        </div>
        
        <div className="checkbox-group">
          <input
            type="checkbox"
            id="useEncryption"
            name="useEncryption"
            checked={config.useEncryption}
            onChange={handleConfigChange}
            disabled={isRunning}
          />
          <label htmlFor="useEncryption">Require Encryption</label>
        </div>
        
        {config.useEncryption && (
          <div className="form-group">
            <label htmlFor="password">Password:</label>
            <input
              type="password"
              id="password"
              name="password"
              value={config.password}
              onChange={handleConfigChange}
              disabled={isRunning}
            />
          </div>
        )}
        
        <button 
          className="save-button"
          onClick={handleSaveConfig}
          disabled={isRunning || isLoading}
        >
          Save Configuration
        </button>
      </div>
    </div>
  );
} 