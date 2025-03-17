import { useState, useEffect } from 'react';
import { clientApi } from '../../api/apiService';

export function QueueManager() {
  const [queue, setQueue] = useState([]);
  const [isProcessing, setIsProcessing] = useState(false);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState('');

  // Fetch queue status on component mount
  useEffect(() => {
    const fetchQueue = async () => {
      try {
        setIsLoading(true);
        const response = await clientApi.getQueue();
        setQueue(response.items || []);
        setIsProcessing(response.isProcessing || false);
        setError('');
      } catch (err) {
        setError('Failed to fetch queue status');
        console.error(err);
      } finally {
        setIsLoading(false);
      }
    };

    fetchQueue();
    
    // Set up polling to refresh the queue status every 3 seconds
    const interval = setInterval(fetchQueue, 3000);
    
    // Clean up interval on component unmount
    return () => clearInterval(interval);
  }, []);

  const handleStartQueue = async () => {
    try {
      setIsLoading(true);
      await clientApi.startQueue();
      setIsProcessing(true);
      setError('');
    } catch (err) {
      setError('Failed to start queue processing');
      console.error(err);
    } finally {
      setIsLoading(false);
    }
  };

  const handleStopQueue = async () => {
    try {
      setIsLoading(true);
      await clientApi.stopQueue();
      setIsProcessing(false);
      setError('');
    } catch (err) {
      setError('Failed to stop queue processing');
      console.error(err);
    } finally {
      setIsLoading(false);
    }
  };

  const handleClearQueue = async () => {
    try {
      setIsLoading(true);
      await clientApi.clearQueue();
      setQueue([]);
      setError('');
    } catch (err) {
      setError('Failed to clear queue');
      console.error(err);
    } finally {
      setIsLoading(false);
    }
  };

  if (isLoading && !error && queue.length === 0) {
    return <div className="loading">Loading queue status...</div>;
  }

  return (
    <div className="queue-manager">
      <h2>Transfer Queue</h2>
      
      {error && <div className="error-message">{error}</div>}
      
      <div className="queue-controls">
        <div className="queue-status">
          Status: <span className={isProcessing ? 'active' : 'inactive'}>
            {isProcessing ? 'Processing' : 'Idle'}
          </span>
        </div>
        
        <div className="queue-buttons">
          {!isProcessing ? (
            <button 
              className="start-button"
              onClick={handleStartQueue}
              disabled={isLoading || queue.length === 0}
            >
              Start Queue
            </button>
          ) : (
            <button 
              className="stop-button"
              onClick={handleStopQueue}
              disabled={isLoading}
            >
              Stop Queue
            </button>
          )}
          
          <button 
            className="clear-button"
            onClick={handleClearQueue}
            disabled={isLoading || queue.length === 0}
          >
            Clear Queue
          </button>
        </div>
      </div>
      
      {queue.length === 0 ? (
        <p className="no-items">No items in the queue.</p>
      ) : (
        <div className="queue-items">
          <h3>Queue Items ({queue.length})</h3>
          
          <table className="queue-table">
            <thead>
              <tr>
                <th>Type</th>
                <th>Source</th>
                <th>Destination</th>
                <th>Options</th>
                <th>Status</th>
              </tr>
            </thead>
            <tbody>
              {queue.map((item) => (
                <tr key={item.id} className={item.status === 'Processing' ? 'processing' : ''}>
                  <td>{item.type}</td>
                  <td>{item.source}</td>
                  <td>{item.destination}</td>
                  <td>
                    {item.compress && <span className="option">Compressed</span>}
                    {item.encrypt && <span className="option">Encrypted</span>}
                  </td>
                  <td>{item.status}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
} 