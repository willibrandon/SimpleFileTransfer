import { useState, useEffect } from 'react';
import { clientApi } from '../../api/apiService';

export function TransferHistory({ transfers = [] }) {
  const [history, setHistory] = useState([]);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState('');

  // Fetch transfer history on component mount
  useEffect(() => {
    const fetchHistory = async () => {
      try {
        setIsLoading(true);
        const response = await clientApi.getHistory();
        setHistory(response.items || []);
        setError('');
      } catch (err) {
        setError('Failed to fetch transfer history');
        console.error(err);
      } finally {
        setIsLoading(false);
      }
    };

    fetchHistory();
  }, []);

  const formatFileSize = (bytes) => {
    if (bytes === 0) return '0 Bytes';
    
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
    switch (status) {
      case 'Completed':
        return 'success';
      case 'Failed':
      case 'Cancelled':
        return 'failed';
      case 'InProgress':
        return 'processing';
      default:
        return '';
    }
  };

  const getStatusText = (status) => {
    switch (status) {
      case 'InProgress':
        return 'In Progress';
      default:
        return status;
    }
  };

  if (isLoading && !error) {
    return <div className="loading">Loading transfer history...</div>;
  }

  return (
    <div className="transfer-history">
      <h2>Transfer History</h2>
      
      {error && <div className="error-message">{error}</div>}
      
      {history.length === 0 ? (
        <div className="no-history">No transfer history</div>
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
              <tr key={transfer.id} className={getStatusClass(transfer.status)}>
                <td>{transfer.fileName}</td>
                <td>{formatFileSize(transfer.size)}</td>
                <td>{transfer.host}:{transfer.port}</td>
                <td>{getStatusText(transfer.status)}</td>
                <td>{formatDate(transfer.startTime)}</td>
                <td>{formatDate(transfer.endTime)}</td>
                <td>
                  {transfer.status === 'Failed' && (
                    <button className="retry-button">
                      Retry
                    </button>
                  )}
                  {transfer.status === 'Failed' && transfer.error && (
                    <span 
                      className="error-info" 
                      title={transfer.error}
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