import { useState, useEffect, useCallback } from 'react'
import { FileTransferForm } from './FileTransferForm'
import { QueueManager } from './QueueManager'
import { TransferHistory } from './TransferHistory'
import { useWebSocket } from '../../WebSocketContext'

export function ClientView() {
  const { transferHistory: wsTransferHistory, queueStatus } = useWebSocket()
  const [transferHistory, setTransferHistory] = useState([])
  const [isLoading, setIsLoading] = useState(false)
  const [isProcessing, setIsProcessing] = useState(queueStatus.isProcessing)
  const [error, setError] = useState('')
  
  // Create a fetchHistory function that can be called from child components
  const fetchHistory = useCallback(async () => {
    try {
      setIsLoading(true);
      setError('');
      
      const response = await fetch('/api/client/history');
      
      if (!response.ok) {
        throw new Error(`API returned ${response.status}: ${response.statusText}`);
      }
      
      const data = await response.json();
      let historyItems = [];
      
      if (data && Array.isArray(data.items)) {
        historyItems = data.items;
      } else if (data && Array.isArray(data)) {
        historyItems = data;
      } else if (data && data.transfers && Array.isArray(data.transfers)) {
        historyItems = data.transfers;
      } else if (data && data.history && Array.isArray(data.history)) {
        historyItems = data.history;
      } else {
        setError('No transfer history available');
        setTransferHistory([]);
        return;
      }
      
      // Process history items to ensure consistent property names
      const processedItems = historyItems.map(item => {
        // Ensure speedLimit is preserved in both camelCase and PascalCase
        const speedLimit = item.speedLimit || item.SpeedLimit;
        
        return {
          ...item,
          // Normalize property names to ensure consistency
          id: item.id || item.Id,
          fileName: item.fileName || item.FileName,
          host: item.host || item.Host || item.targetHost || item.TargetHost,
          port: item.port || item.Port,
          size: item.size || item.Size,
          startTime: item.startTime || item.StartTime,
          endTime: item.endTime || item.EndTime,
          status: item.status || item.Status,
          // Ensure speedLimit is preserved
          speedLimit: speedLimit,
          SpeedLimit: speedLimit
        };
      });
      
      // Sort history by startTime in descending order (newest first)
      const sortedHistory = [...processedItems].sort((a, b) => {
        const dateA = new Date(a.startTime || 0);
        const dateB = new Date(b.startTime || 0);
        return dateB - dateA; // Descending order
      });
      
      setTransferHistory(sortedHistory);
    } catch (error) {
      console.error('Error fetching transfer history:', error);
      setError(`Failed to load history: ${error.message}`);
    } finally {
      setIsLoading(false);
    }
  }, []);
  
  // Fetch transfer history directly from API
  useEffect(() => {
    fetchHistory();
    
    // Set up a refresh interval
    const interval = setInterval(fetchHistory, 30000); // Refresh every 30 seconds
    
    return () => {
      clearInterval(interval);
    };
  }, [fetchHistory]);
  
  // Merge transfer history from WebSocket
  useEffect(() => {
    if (wsTransferHistory && wsTransferHistory.length > 0) {
      setTransferHistory(prevHistory => {
        // Create a map of existing transfers by ID
        const historyMap = new Map(prevHistory.map(item => [item.id || item.Id, item]));
        
        // Add or update transfers from WebSocket
        wsTransferHistory.forEach(item => {
          if (item && (item.id || item.Id)) {
            const id = item.id || item.Id;
            const existingItem = historyMap.get(id);
            
            if (existingItem) {
              // Preserve important properties like speedLimit when updating
              const speedLimit = item.speedLimit || item.SpeedLimit || 
                                existingItem.speedLimit || existingItem.SpeedLimit;
              
              historyMap.set(id, {
                ...existingItem,
                ...item,
                // Explicitly preserve these properties
                speedLimit: speedLimit,
                SpeedLimit: speedLimit
              });
            } else {
              historyMap.set(id, item);
            }
          }
        });
        
        // Convert map to array and sort by startTime
        const mergedHistory = Array.from(historyMap.values());
        return mergedHistory.sort((a, b) => {
          const dateA = new Date(a.startTime || a.StartTime || 0);
          const dateB = new Date(b.startTime || b.StartTime || 0);
          return dateB - dateA; // Descending order
        });
      });
    }
  }, [wsTransferHistory]);
  
  // Handle processing state changes from QueueManager
  const handleProcessingChange = (newState) => {
    setIsProcessing(newState);
  };
  
  return (
    <div className="client-view">
      <h1>Client Mode</h1>
      <p className="description">
        Send files to other devices. Enter the server IP address and select files to transfer.
      </p>
      
      <div className="client-container">
        <div className="client-main">
          <FileTransferForm onTransferComplete={fetchHistory} />
          <QueueManager 
            isProcessing={queueStatus.isProcessing}
            count={queueStatus.count}
            onProcessingChange={handleProcessingChange}
            onQueueOperation={fetchHistory}
          />
        </div>
        <div className="client-history">
          <TransferHistory 
            transfers={transferHistory} 
            isLoading={isLoading} 
            error={error}
            refreshHistory={fetchHistory}
          />
        </div>
      </div>
    </div>
  )
} 