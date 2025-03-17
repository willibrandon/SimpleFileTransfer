import { useState, useEffect } from 'react';
import { clientApi } from '../../api/apiService';

export function TransferHistory() {
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
    const date = new Date(dateString);
    return date.toLocaleString();
  };

  if (isLoading && !error) {
    return <div className="loading">Loading transfer history...</div>;
  }

  return (
    <div className="transfer-history">
      <h2>Transfer History</h2>
      
      {error && <div className="error-message">{error}</div>}
      
      {history.length === 0 ? (
        <p className="no-history">No transfer history available.</p>
      ) : (
        <table className="history-table">
          <thead>
            <tr>
              <th>Date</th>
              <th>Type</th>
              <th>Source</th>
              <th>Destination</th>
              <th>Size</th>
              <th>Status</th>
              <th>Options</th>
            </tr>
          </thead>
          <tbody>
            {history.map((item) => (
              <tr key={item.id} className={item.status.toLowerCase()}>
                <td>{formatDate(item.date)}</td>
                <td>{item.type}</td>
                <td>{item.source}</td>
                <td>{item.destination}</td>
                <td>{formatFileSize(item.size)}</td>
                <td>{item.status}</td>
                <td>
                  {item.status === 'Failed' && (
                    <button 
                      className="retry-button"
                      onClick={() => {
                        // Implement retry functionality
                        alert('Retry functionality not implemented yet');
                      }}
                    >
                      Retry
                    </button>
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