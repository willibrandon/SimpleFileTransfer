import { useState, useEffect, useRef } from 'react';
import { clientApi } from '../../api/apiService';
import { Pagination } from '../common/Pagination';

export function TransferHistory({ transfers = [], isLoading = false, error = '', refreshHistory }) {
  const [history, setHistory] = useState([]);
  const [localError, setLocalError] = useState('');
  const [statusMessage, setStatusMessage] = useState('');
  const [statusType, setStatusType] = useState(''); // 'success', 'error'
  const [currentPage, setCurrentPage] = useState(1);
  const [itemsPerPage] = useState(5);
  const prevTransfersLengthRef = useRef(0);

  // Process transfers prop
  useEffect(() => {
    if (Array.isArray(transfers) && transfers.length > 0) {
      // Sort transfers by startTime in descending order (newest first)
      const sortedTransfers = [...transfers].sort((a, b) => {
        const dateA = new Date(a.startTime || a.StartTime || 0);
        const dateB = new Date(b.startTime || b.StartTime || 0);
        return dateB - dateA; // Descending order
      });
      
      // Only reset to first page when the number of transfers changes significantly
      // This prevents resetting pagination when just refreshing the same data
      const newTransfersLength = sortedTransfers.length;
      if (Math.abs(newTransfersLength - prevTransfersLengthRef.current) > 1) {
        setCurrentPage(1);
      }
      prevTransfersLengthRef.current = newTransfersLength;
      
      // Set history state with sorted transfers
      setHistory(sortedTransfers);
    }
  }, [transfers]);

  // Get current page transfers
  const indexOfLastItem = currentPage * itemsPerPage;
  const indexOfFirstItem = indexOfLastItem - itemsPerPage;
  const currentItems = history.slice(indexOfFirstItem, indexOfLastItem);
  const totalPages = Math.ceil(history.length / itemsPerPage);

  // Change page
  const handlePageChange = (pageNumber) => {
    setCurrentPage(pageNumber);
  };

  // Handle error prop
  useEffect(() => {
    if (error) {
      setLocalError(error);
    }
  }, [error]);

  const showStatus = (message, type) => {
    setStatusMessage(message);
    setStatusType(type);
    
    // Clear status after 5 seconds
    setTimeout(() => {
      setStatusMessage('');
      setStatusType('');
    }, 5000);
  };

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

  const handleRetry = async (transfer) => {
    if (!transfer || !transfer.fileName) {
      return;
    }
    
    try {
      // Create a new transfer request
      const retryRequest = {
        fileName: transfer.fileName || transfer.FileName,
        host: transfer.host || transfer.Host || transfer.targetHost || transfer.TargetHost,
        port: transfer.port || transfer.Port,
        useCompression: transfer.useCompression || transfer.UseCompression,
        useEncryption: transfer.useEncryption || transfer.UseEncryption,
        password: transfer.password || transfer.Password
      };
      
      // Queue the transfer
      await clientApi.queueTransfer(retryRequest);
      
      // Show success message
      showStatus(`File "${retryRequest.fileName}" has been added to the queue`, 'success');
      
      // Automatically refresh the transfer history
      if (refreshHistory) {
        setTimeout(() => {
          refreshHistory();
        }, 500); // Small delay to allow the server to process the request
      }
    } catch (error) {
      showStatus(`Failed to retry transfer: ${error.message}`, 'error');
    }
  };

  // Manually trigger a history refresh
  const handleRefresh = () => {
    if (refreshHistory) {
      refreshHistory();
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
      <div className="history-header">
        <h2>Transfer History</h2>
        <button 
          onClick={handleRefresh} 
          className="refresh-button"
        >
          Refresh
        </button>
      </div>
      
      {localError && <div className="error-message">{localError}</div>}
      {statusMessage && <div className={`status ${statusType}`}>{statusMessage}</div>}
      
      <style>
        {`
          .history-header {
            display: flex;
            justify-content: space-between;
            align-items: center;
            margin-bottom: 1rem;
          }
          
          .refresh-button {
            padding: 0.3rem 0.6rem;
            font-size: 0.8rem;
          }
          
          .history-list {
            list-style: none;
            padding: 0;
            margin: 0;
          }
          
          .history-item {
            background-color: var(--bg-highlight);
            margin-bottom: 0.5rem;
            border-radius: 4px;
            padding: 0.75rem;
            border: 1px solid var(--border-color);
          }
          
          .history-item:hover {
            background-color: var(--light-bg);
          }
          
          .history-item-header {
            display: flex;
            justify-content: space-between;
            margin-bottom: 0.5rem;
          }
          
          .file-name {
            font-weight: bold;
            font-size: 1rem;
            color: var(--text);
          }
          
          .file-size {
            color: var(--dim);
            font-size: 0.9rem;
          }
          
          .history-item-details {
            display: grid;
            grid-template-columns: repeat(2, 1fr);
            gap: 0.5rem;
            font-size: 0.9rem;
            color: var(--text);
            margin-bottom: 0.75rem;
          }
          
          .detail-label {
            color: var(--dim);
          }
          
          .status-completed {
            color: var(--success-color);
          }
          
          .status-failed {
            color: var(--error-color);
          }
          
          .status-inprogress {
            color: var(--primary-color);
          }
          
          .history-item-actions {
            margin-top: 0.75rem;
            display: flex;
            justify-content: flex-end;
          }
          
          .retry-button {
            padding: 0.3rem 0.6rem;
            font-size: 0.8rem;
            min-width: 60px;
            background-color: var(--primary-color);
            color: var(--bg);
            border: none;
            border-radius: 4px;
            cursor: pointer;
          }
          
          .no-history {
            text-align: center;
            padding: 2rem;
            color: var(--dim);
            background-color: var(--bg-highlight);
            border-radius: 4px;
            margin-top: 1rem;
          }
          
          .history-count {
            color: var(--dim);
            font-size: 0.8rem;
            margin-bottom: 1rem;
            text-align: right;
          }
        `}
      </style>
      
      {history.length === 0 ? (
        <div className="no-history">
          No transfer history
          {isLoading ? ' (loading...)' : ''}
        </div>
      ) : (
        <>
          <div className="history-count">
            Showing {indexOfFirstItem + 1}-{Math.min(indexOfLastItem, history.length)} of {history.length} transfers
          </div>
          
          <ul className="history-list">
            {currentItems.map((transfer) => {
              const fileName = transfer.fileName || transfer.FileName;
              const size = formatFileSize(transfer.size || transfer.Size);
              const destination = `${(transfer.host || transfer.Host || transfer.targetHost || transfer.TargetHost || 'Unknown')}:${transfer.port || transfer.Port || 0}`;
              const status = transfer.status || transfer.Status || '';
              const statusLower = String(status).toLowerCase();
              const startTime = formatDate(transfer.startTime || transfer.StartTime);
              const endTime = formatDate(transfer.endTime || transfer.EndTime);
              
              let statusClass = '';
              if (statusLower.includes('complet')) statusClass = 'status-completed';
              else if (statusLower.includes('fail')) statusClass = 'status-failed';
              else if (statusLower.includes('progress')) statusClass = 'status-inprogress';
              
              return (
                <li key={transfer.id || transfer.Id} className="history-item">
                  <div className="history-item-header">
                    <span className="file-name">{fileName}</span>
                    <span className="file-size">{size}</span>
                  </div>
                  
                  <div className="history-item-details">
                    <div>
                      <span className="detail-label">Destination: </span>
                      {destination}
                    </div>
                    
                    <div>
                      <span className="detail-label">Status: </span>
                      <span className={statusClass}>
                        {statusLower.includes('complet') ? 'Completed' : 
                         statusLower.includes('fail') ? 'Failed' :
                         statusLower.includes('progress') ? 'In Progress' : 
                         status || 'Unknown'}
                      </span>
                    </div>
                    
                    <div>
                      <span className="detail-label">Started: </span>
                      {startTime}
                    </div>
                    
                    <div>
                      <span className="detail-label">Completed: </span>
                      {endTime}
                    </div>
                  </div>
                  
                  {statusLower.includes('fail') && (
                    <div className="history-item-actions">
                      <button 
                        className="retry-button"
                        onClick={() => handleRetry(transfer)}
                      >
                        Retry
                      </button>
                    </div>
                  )}
                </li>
              );
            })}
          </ul>
          
          <Pagination 
            currentPage={currentPage}
            totalPages={totalPages}
            onPageChange={handlePageChange}
          />
        </>
      )}
    </div>
  );
} 