import { useState, useEffect } from 'react';
import { clientApi } from '../../api/apiService';
import { useWebSocket } from '../../WebSocketContext'

export function QueueManager({ isProcessing: initialIsProcessing, count, onProcessingChange }) {
  const { connected } = useWebSocket()
  const [queue, setQueue] = useState([]);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState('');
  const [isProcessing, setIsProcessing] = useState(initialIsProcessing);

  // Update parent component when processing state changes
  useEffect(() => {
    if (onProcessingChange && isProcessing !== initialIsProcessing) {
      onProcessingChange(isProcessing);
    }
  }, [isProcessing, initialIsProcessing, onProcessingChange]);

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

  // Handle starting the queue
  const handleStartQueue = async () => {
    try {
      const response = await fetch('/api/client/queue/start', {
        method: 'POST'
      })
      
      if (!response.ok) {
        const error = await response.json()
        throw new Error(error.error || 'Failed to start queue')
      }
    } catch (error) {
      console.error('Error starting queue:', error)
      alert(`Error starting queue: ${error.message}`)
    }
  }
  
  // Handle stopping the queue
  const handleStopQueue = async () => {
    try {
      const response = await fetch('/api/client/queue/stop', {
        method: 'POST'
      })
      
      if (!response.ok) {
        const error = await response.json()
        throw new Error(error.error || 'Failed to stop queue')
      }
    } catch (error) {
      console.error('Error stopping queue:', error)
      alert(`Error stopping queue: ${error.message}`)
    }
  }
  
  // Handle clearing the queue
  const handleClearQueue = async () => {
    if (!confirm('Are you sure you want to clear the queue? This will cancel all pending transfers.')) {
      return
    }
    
    try {
      const response = await fetch('/api/client/queue/clear', {
        method: 'POST'
      })
      
      if (!response.ok) {
        const error = await response.json()
        throw new Error(error.error || 'Failed to clear queue')
      }
    } catch (error) {
      console.error('Error clearing queue:', error)
      alert(`Error clearing queue: ${error.message}`)
    }
  }

  if (isLoading && !error && queue.length === 0) {
    return <div className="loading">Loading queue status...</div>;
  }

  return (
    <div className="queue-manager">
      <h2>Queue Manager</h2>
      
      <div className="queue-status">
        <div>
          <span>Status: </span>
          <span className={`status-indicator ${isProcessing ? 'active' : 'inactive'}`}>
            {isProcessing ? 'Processing' : 'Idle'}
          </span>
        </div>
        
        <div>
          <span>Items in queue: </span>
          <span>{count}</span>
        </div>
      </div>
      
      <div className="queue-controls">
        <div className="queue-buttons">
          {isProcessing ? (
            <button 
              className="stop-button"
              onClick={handleStopQueue}
              disabled={!connected || count === 0}
            >
              Stop Queue
            </button>
          ) : (
            <button 
              className="start-button"
              onClick={handleStartQueue}
              disabled={!connected || count === 0}
            >
              Start Queue
            </button>
          )}
          
          <button 
            className="clear-button"
            onClick={handleClearQueue}
            disabled={!connected || count === 0}
          >
            Clear Queue
          </button>
        </div>
      </div>
      
      <p className="queue-info">
        {count === 0 ? (
          'The queue is empty. Add files to the queue to transfer them in sequence.'
        ) : isProcessing ? (
          `Processing ${count} item(s) in the queue.`
        ) : (
          `${count} item(s) in the queue. Click "Start Queue" to begin processing.`
        )}
      </p>
      
      {error && <div className="error-message">{error}</div>}
      
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