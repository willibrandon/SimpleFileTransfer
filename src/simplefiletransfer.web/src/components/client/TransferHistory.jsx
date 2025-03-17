import { useState, useEffect } from 'react';
import { clientApi } from '../../api/apiService';

export function TransferHistory({ transfers = [], isLoading = false, error = '', refreshHistory }) {
  const [history, setHistory] = useState([]);
  const [localError, setLocalError] = useState('');
  const [statusMessage, setStatusMessage] = useState('');
  const [statusType, setStatusType] = useState(''); // 'success', 'error'

  // Process transfers prop
  useEffect(() => {
    if (Array.isArray(transfers) && transfers.length > 0) {
      // First, deduplicate transfers by filename and timestamp
      // If the same file appears multiple times, prioritize the one that was transferred
      const deduplicatedTransfers = [];
      const fileMap = new Map();
      
      // First pass: collect all entries by filename
      transfers.forEach(transfer => {
        const fileName = transfer.fileName || transfer.FileName || '';
        if (!fileMap.has(fileName)) {
          fileMap.set(fileName, []);
        }
        fileMap.get(fileName).push(transfer);
      });
      
      // Second pass: for each filename, pick the entry that was most likely transferred
      fileMap.forEach((entries, fileName) => {
        // Sort entries by completion status, then by start time (newest first)
        entries.sort((a, b) => {
          const aStatus = String(a.status || a.Status || '').toLowerCase();
          const bStatus = String(b.status || b.Status || '').toLowerCase();
          
          // Completed entries come first
          if (aStatus === 'completed' && bStatus !== 'completed') return -1;
          if (aStatus !== 'completed' && bStatus === 'completed') return 1;
          
          // Then entries with end times
          const aEndTime = a.endTime || a.EndTime;
          const bEndTime = b.endTime || b.EndTime;
          if (aEndTime && !bEndTime) return -1;
          if (!aEndTime && bEndTime) return 1;
          
          // Then sort by start time (newest first)
          const aStartTime = a.startTime || a.StartTime || '';
          const bStartTime = b.startTime || b.StartTime || '';
          return bStartTime.localeCompare(aStartTime);
        });
        
        // Add all entries to the deduplicated list
        // This ensures we keep both transferred and queued versions
        entries.forEach(entry => {
          deduplicatedTransfers.push(entry);
        });
      });
      
      // Process all transfers and mark them as transferred or queued
      const processedTransfers = deduplicatedTransfers.map(transfer => {
        const status = String(transfer.status || transfer.Status || '').toLowerCase();
        const startTime = transfer.startTime || transfer.StartTime;
        const endTime = transfer.endTime || transfer.EndTime;
        
        // Determine if this was actually transferred or just queued
        let wasTransferred = false;
        
        // Completed transfers
        if (status === 'completed') {
          wasTransferred = true;
        }
        
        // Failed transfers (they were attempted)
        else if (status === 'failed') {
          wasTransferred = true;
        }
        
        // Transfers with both start and end times
        else if (startTime && endTime) {
          wasTransferred = true;
        }
        
        // In-progress transfers or transfers with a start time but no end time
        else if (status === 'inprogress' || startTime) {
          wasTransferred = true;
        }
        
        // Return the transfer with the wasTransferred flag
        return {
          ...transfer,
          wasTransferred
        };
      });
      
      setHistory(processedTransfers);
    }
  }, [transfers]);

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

  const getStatusClass = (transfer) => {
    if (!transfer) return '';
    
    // If it's just queued, use a specific class
    if (!transfer.wasTransferred) {
      return 'queued';
    }
    
    const status = transfer.status || transfer.Status;
    if (!status) return '';
    
    // Convert to string to ensure toLowerCase works
    const statusStr = String(status);
    const statusLower = statusStr.toLowerCase();
    
    // Handle status values
    if (statusLower === 'completed') return 'completed-status';
    if (statusLower === 'failed') return 'failed';
    if (statusLower === 'inprogress') return 'processing';
    if (statusLower === 'queued') return 'queued';
    
    return '';
  };

  const getStatusText = (transfer) => {
    // If it's just queued, show as Queued
    if (!transfer.wasTransferred) {
      return 'Queued';
    }
    
    const status = transfer.status || transfer.Status;
    const startTime = transfer.startTime || transfer.StartTime;
    const endTime = transfer.endTime || transfer.EndTime;
    
    if (!status) {
      // If it has a start time but no end time and no explicit status, it was likely transferred
      if (startTime && !endTime) {
        return 'Completed';
      }
      return 'Queued';
    }
    
    // Convert to string to ensure consistent handling
    const statusStr = String(status);
    const statusLower = statusStr.toLowerCase();
    
    // Handle status values
    if (statusLower === 'completed') return 'Completed';
    if (statusLower === 'failed') return 'Failed';
    if (statusLower === 'inprogress') return 'In Progress';
    if (statusLower === 'queued') return 'Queued';
    if (statusLower === 'cancelled') return 'Cancelled';
    
    // Return the status with first letter capitalized
    return statusStr.charAt(0).toUpperCase() + statusStr.slice(1);
  };

  // Get a tooltip for the status
  const getStatusTooltip = (transfer) => {
    if (!transfer.wasTransferred) {
      return 'File was queued but not yet transferred';
    }
    
    const status = transfer.status || transfer.Status;
    const startTime = transfer.startTime || transfer.StartTime;
    const endTime = transfer.endTime || transfer.EndTime;
    
    if (!status && startTime && !endTime) {
      return 'Transfer was completed but end time not recorded';
    }
    
    if (status === '2' || String(status).toLowerCase().includes('complet')) {
      return 'Transfer completed successfully';
    }
    
    if (status === '3' || String(status).toLowerCase().includes('fail')) {
      return transfer.error || transfer.Error || 'Transfer failed';
    }
    
    if (status === '1' || String(status).toLowerCase().includes('progress')) {
      return 'Transfer is in progress';
    }
    
    return 'Unknown status';
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
      } else {
        setTimeout(() => {
          handleRefresh();
        }, 500);
      }
    } catch (error) {
      showStatus(`Failed to retry transfer: ${error.message}`, 'error');
    }
  };

  // Manually trigger a history refresh
  const handleRefresh = async () => {
    try {
      if (refreshHistory) {
        refreshHistory();
        return;
      }
      
      setLocalError('');
      const response = await clientApi.getHistory();
      
      let transferItems = [];
      
      if (response && Array.isArray(response.items)) {
        transferItems = response.items;
      } else if (response && Array.isArray(response)) {
        transferItems = response;
      } else if (response && response.transfers && Array.isArray(response.transfers)) {
        transferItems = response.transfers;
      } else if (response && response.history && Array.isArray(response.history)) {
        transferItems = response.history;
      } else {
        setLocalError('Unexpected response format from server');
        return;
      }
      
      // First, deduplicate transfers by filename and timestamp
      // If the same file appears multiple times, prioritize the one that was transferred
      const deduplicatedTransfers = [];
      const fileMap = new Map();
      
      // First pass: collect all entries by filename
      transferItems.forEach(transfer => {
        const fileName = transfer.fileName || transfer.FileName || '';
        if (!fileMap.has(fileName)) {
          fileMap.set(fileName, []);
        }
        fileMap.get(fileName).push(transfer);
      });
      
      // Second pass: for each filename, pick the entry that was most likely transferred
      fileMap.forEach((entries, fileName) => {
        // Sort entries by completion status, then by start time (newest first)
        entries.sort((a, b) => {
          const aStatus = String(a.status || a.Status || '').toLowerCase();
          const bStatus = String(b.status || b.Status || '').toLowerCase();
          
          // Completed entries come first
          if (aStatus === 'completed' && bStatus !== 'completed') return -1;
          if (aStatus !== 'completed' && bStatus === 'completed') return 1;
          
          // Then entries with end times
          const aEndTime = a.endTime || a.EndTime;
          const bEndTime = b.endTime || b.EndTime;
          if (aEndTime && !bEndTime) return -1;
          if (!aEndTime && bEndTime) return 1;
          
          // Then sort by start time (newest first)
          const aStartTime = a.startTime || a.StartTime || '';
          const bStartTime = b.startTime || b.StartTime || '';
          return bStartTime.localeCompare(aStartTime);
        });
        
        // Add all entries to the deduplicated list
        // This ensures we keep both transferred and queued versions
        entries.forEach(entry => {
          deduplicatedTransfers.push(entry);
        });
      });
      
      // Process all transfers and mark them as transferred or queued
      const processedTransfers = deduplicatedTransfers.map(transfer => {
        const status = String(transfer.status || transfer.Status || '').toLowerCase();
        const startTime = transfer.startTime || transfer.StartTime;
        const endTime = transfer.endTime || transfer.EndTime;
        
        // Determine if this was actually transferred or just queued
        let wasTransferred = false;
        
        // Completed transfers
        if (status === 'completed') {
          wasTransferred = true;
        }
        
        // Failed transfers (they were attempted)
        else if (status === 'failed') {
          wasTransferred = true;
        }
        
        // Transfers with both start and end times
        else if (startTime && endTime) {
          wasTransferred = true;
        }
        
        // In-progress transfers or transfers with a start time but no end time
        // For resume.docx at 10:08:05 PM, this should mark it as transferred
        else if (status === 'inprogress' || startTime) {
          wasTransferred = true;
        }
        
        // Return the transfer with the wasTransferred flag
        return {
          ...transfer,
          wasTransferred
        };
      });
      
      setHistory(processedTransfers);
    } catch (err) {
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
            fontSize: '0.8rem'
          }}
        >
          Refresh
        </button>
      </h2>
      
      {localError && <div className="error-message">{localError}</div>}
      {statusMessage && <div className={`status ${statusType}`}>{statusMessage}</div>}
      
      <style>
        {`
          .error-info {
            margin-left: 8px;
            cursor: help;
            font-weight: bold;
          }
          .history-table tr.completed-status {
            background-color: transparent;
          }
          .history-table tr.queued {
            opacity: 0.7;
            background-color: var(--bg-highlight);
          }
          .history-table tr.queued td {
            font-style: italic;
          }
        `}
      </style>
      
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
              <tr key={transfer.id || transfer.Id} className={getStatusClass(transfer)}>
                <td>{transfer.fileName || transfer.FileName}</td>
                <td>{formatFileSize(transfer.size || transfer.Size)}</td>
                <td>{(transfer.host || transfer.Host || transfer.targetHost || transfer.TargetHost || 'Unknown')}:{transfer.port || transfer.Port || 0}</td>
                <td title={getStatusTooltip(transfer)}>{getStatusText(transfer)}</td>
                <td>{formatDate(transfer.startTime || transfer.StartTime)}</td>
                <td>{formatDate(transfer.endTime || transfer.EndTime)}</td>
                <td>
                  {(String(transfer.status || transfer.Status).toLowerCase() === 'failed' || 
                    (!transfer.wasTransferred && !(transfer.startTime || transfer.StartTime))) && (
                    <button 
                      className="retry-button"
                      onClick={() => handleRetry(transfer)}
                    >
                      {transfer.wasTransferred ? 'Retry' : 'Transfer'}
                    </button>
                  )}
                  {(String(transfer.status || transfer.Status).toLowerCase() === 'failed') && 
                    (transfer.error || transfer.Error) && (
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