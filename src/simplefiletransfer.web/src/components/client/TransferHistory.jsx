import { useState, useEffect } from 'react';
import { clientApi } from '../../api/apiService';

export function TransferHistory({ transfers = [], isLoading = false, error = '' }) {
  const [history, setHistory] = useState([]);
  const [localError, setLocalError] = useState('');

  // Process transfers prop
  useEffect(() => {
    console.log('TransferHistory received transfers:', transfers);
    if (Array.isArray(transfers) && transfers.length > 0) {
      console.log('Setting history state with transfers:', transfers);
      setHistory(transfers);
    } else {
      console.log('Transfers array is empty or invalid:', transfers);
    }
  }, [transfers]);

  // Handle error prop
  useEffect(() => {
    if (error) {
      setLocalError(error);
    }
  }, [error]);

  const formatFileSize = (bytes) => {
    if (!bytes || bytes === 0) return '0 Bytes';
    
    const k = 1024;
    const sizes = ['Bytes', 'KB', 'MB', 'GB', 'TB'];
    const i = Math.floor(Math.log(bytes) / Math.log(k));
    
    return parseFloat((bytes / Math.pow(k, i)).toFixed(2)) + ' ' + sizes[i];
  };

  const formatDate = (dateString) => {
    if (!dateString) return '-';
    const date = new Date(dateString);
    return date.toLocaleString();
  };

  const getStatusClass = (status) => {
    if (!status) return '';
    
    // Convert to string to ensure toLowerCase works
    const statusStr = String(status);
    const statusLower = statusStr.toLowerCase();
    
    if (statusLower.includes('complet')) return 'success';
    if (statusLower.includes('fail') || statusLower.includes('cancel')) return 'failed';
    if (statusLower.includes('progress') || statusLower.includes('start')) return 'processing';
    return '';
  };

  const getStatusText = (status) => {
    if (!status) return 'Unknown';
    
    // Convert to string to ensure consistent handling
    const statusStr = String(status);
    
    if (statusStr === 'InProgress' || statusStr.toLowerCase() === 'inprogress') return 'In Progress';
    if (statusStr === 'started' || statusStr.toLowerCase() === 'started') return 'In Progress';
    if (statusStr === 'completed' || statusStr.toLowerCase() === 'completed') return 'Completed';
    if (statusStr === 'failed' || statusStr.toLowerCase() === 'failed') return 'Failed';
    
    // Return the status with first letter capitalized
    return statusStr.charAt(0).toUpperCase() + statusStr.slice(1);
  };
  
  const handleRetry = async (transfer) => {
    if (!transfer || !transfer.fileName) {
      console.error('Cannot retry: Invalid transfer data');
      return;
    }
    
    try {
      // Create a new transfer request
      const retryRequest = {
        fileName: transfer.fileName,
        host: transfer.host || transfer.targetHost,
        port: transfer.port,
        useCompression: transfer.useCompression,
        useEncryption: transfer.useEncryption,
        password: transfer.password
      };
      
      console.log('Retrying transfer with request:', retryRequest);
      
      // Queue the transfer
      await clientApi.queueTransfer(retryRequest);
      
      // Show success message
      alert(`File "${transfer.fileName}" has been added to the queue`);
    } catch (error) {
      console.error('Error retrying transfer:', error);
      alert(`Failed to retry transfer: ${error.message}`);
    }
  };

  // Manually trigger a history refresh
  const handleRefresh = async () => {
    try {
      setLocalError('');
      const response = await clientApi.getHistory();
      console.log('Manual refresh response:', response);
      
      if (response && Array.isArray(response.items)) {
        setHistory(response.items);
      } else if (response && Array.isArray(response)) {
        setHistory(response);
      } else if (response && response.transfers && Array.isArray(response.transfers)) {
        setHistory(response.transfers);
      } else if (response && response.history && Array.isArray(response.history)) {
        setHistory(response.history);
      } else {
        console.warn('Unexpected response format from manual refresh:', response);
        setLocalError('Unexpected response format from server');
      }
    } catch (err) {
      console.error('Error in manual refresh:', err);
      setLocalError(`Failed to refresh: ${err.message}`);
    }
  };

  // Render loading state
  if (isLoading && history.length === 0) {
    return (
      <div className="transfer-history">
        <h2>Transfer History</h2>
        <div className="loading">Loading transfer history...</div>
      </div>
    );
  }

  return (
    <div className="transfer-history">
      <h2>
        Transfer History
        <button 
          onClick={handleRefresh} 
          style={{ 
            marginLeft: '10px', 
            padding: '0.3rem 0.6rem',
            fontSize: '0.8rem',
            backgroundColor: 'var(--primary-color)'
          }}
        >
          Refresh
        </button>
      </h2>
      
      {localError && <div className="error-message">{localError}</div>}
      
      {history.length === 0 ? (
        <div className="no-history">
          No transfer history
          {isLoading ? ' (loading...)' : ''}
        </div>
      ) : (
        <table className="history-table">
          <thead>
            <tr>
              <th>File Name</th>
              <th>Size</th>
              <th>Destination</th>
              <th>Status</th>
              <th>Started</th>
              <th>Completed</th>
              <th>Options</th>
            </tr>
          </thead>
          <tbody>
            {history.map((transfer) => (
              <tr key={transfer.id || transfer.Id} className={getStatusClass(transfer.status || transfer.Status)}>
                <td>{transfer.fileName || transfer.FileName}</td>
                <td>{formatFileSize(transfer.size || transfer.Size)}</td>
                <td>{(transfer.host || transfer.Host || transfer.targetHost || transfer.TargetHost || 'Unknown')}:{transfer.port || transfer.Port || 0}</td>
                <td>{getStatusText(transfer.status || transfer.Status)}</td>
                <td>{formatDate(transfer.startTime || transfer.StartTime)}</td>
                <td>{formatDate(transfer.endTime || transfer.EndTime)}</td>
                <td>
                  {(String(transfer.status || transfer.Status).toLowerCase() === 'failed') && (
                    <button 
                      className="retry-button"
                      onClick={() => handleRetry(transfer)}
                    >
                      Retry
                    </button>
                  )}
                  {(String(transfer.status || transfer.Status).toLowerCase() === 'failed') && (transfer.error || transfer.Error) && (
                    <span 
                      className="error-info" 
                      title={transfer.error || transfer.Error}
                    >
                      ⓘ
                    </span>
                  )}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </div>
  );
} 