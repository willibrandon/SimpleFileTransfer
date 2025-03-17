import { createContext, useContext, useEffect, useState, useRef, useCallback } from 'react';

// Create a context for the WebSocket
const WebSocketContext = createContext(null);

// Custom hook to use the WebSocket context
export const useWebSocket = () => useContext(WebSocketContext);

// WebSocket provider component
export function WebSocketProvider({ children }) {
  const [socket, setSocket] = useState(null);
  const [connected, setConnected] = useState(false);
  const [events, setEvents] = useState([]);
  const [serverStatus, setServerStatus] = useState({ isRunning: false, port: 0 });
  const [receivedFiles, setReceivedFiles] = useState([]);
  const [transferHistory, setTransferHistory] = useState([]);
  const [queueStatus, setQueueStatus] = useState({ isProcessing: false, count: 0 });
  
  // Refs for reconnection logic
  const reconnectAttempts = useRef(0);
  const maxReconnectAttempts = useRef(10);
  const reconnectTimeoutId = useRef(null);
  const isUnmounting = useRef(false);
  const hasCheckedServerStatus = useRef(false);
  const lastServerStatus = useRef({ isRunning: false, port: 0 });

  // Check server status via API - memoized to prevent unnecessary re-renders
  const checkServerStatus = useCallback(async () => {
    try {
      const response = await fetch('/api/server/status');
      if (response.ok) {
        const data = await response.json();
        console.log('Server status from API:', data);
        
        // Only update state if the status has changed
        if (lastServerStatus.current.isRunning !== data.isRunning || 
            lastServerStatus.current.port !== data.port) {
          lastServerStatus.current = { 
            isRunning: data.isRunning, 
            port: data.port || 0 
          };
          setServerStatus(lastServerStatus.current);
        }
        return data.isRunning;
      }
    } catch (error) {
      console.error('Error checking server status:', error);
    }
    return false;
  }, []);

  // Function to create a new WebSocket connection - memoized
  const createWebSocketConnection = useCallback(() => {
    if (isUnmounting.current) return;
    
    // Clear any existing reconnection timeout
    if (reconnectTimeoutId.current) {
      clearTimeout(reconnectTimeoutId.current);
      reconnectTimeoutId.current = null;
    }
    
    // Determine the WebSocket URL based on the current location
    const protocol = window.location.protocol === 'https:' ? 'wss:' : 'ws:';
    const wsUrl = `${protocol}//${window.location.host}/ws`;
    
    // Create a new WebSocket connection
    const newSocket = new WebSocket(wsUrl);
    
    // Set up event handlers
    newSocket.onopen = () => {
      // Reset reconnection attempts on successful connection
      reconnectAttempts.current = 0;
      setConnected(true);
      console.log('WebSocket connected');
      
      // Check server status if we haven't already
      if (!hasCheckedServerStatus.current) {
        checkServerStatus();
        hasCheckedServerStatus.current = true;
      }
      
      // Request initial data
      setTimeout(() => {
        if (newSocket.readyState === WebSocket.OPEN) {
          newSocket.send(JSON.stringify({ type: 'get_server_status' }));
          newSocket.send(JSON.stringify({ type: 'get_received_files' }));
        }
      }, 500);
    };
    
    newSocket.onclose = (event) => {
      setConnected(false);
      
      // Don't log normal closures
      if (event.code !== 1000) {
        console.log('WebSocket disconnected, will attempt to reconnect...');
      }
      
      // Attempt to reconnect with exponential backoff
      if (!isUnmounting.current && reconnectAttempts.current < maxReconnectAttempts.current) {
        const delay = Math.min(1000 * Math.pow(1.5, reconnectAttempts.current), 30000);
        reconnectAttempts.current++;
        
        reconnectTimeoutId.current = setTimeout(() => {
          createWebSocketConnection();
        }, delay);
      }
    };
    
    newSocket.onerror = () => {
      // Suppress error logging - we'll handle reconnection in onclose
      // Errors are expected during development or when the server is starting up
    };
    
    newSocket.onmessage = (message) => {
      try {
        const event = JSON.parse(message.data);
        console.log('WebSocket event:', event);
        
        // Add the event to the events list
        setEvents(prev => [...prev, event]);
        
        // Handle specific event types
        handleEvent(event);
      } catch (error) {
        console.error('Error parsing WebSocket message:', error);
      }
    };
    
    // Save the socket to state
    setSocket(newSocket);
  }, []);

  // Connect to the WebSocket server
  useEffect(() => {
    if (!socket && !isUnmounting.current) {
      createWebSocketConnection();
    }
    
    // Check server status on initial load
    if (!hasCheckedServerStatus.current) {
      hasCheckedServerStatus.current = true;
      checkServerStatus();
    }
    
    return () => {
      isUnmounting.current = true;
      
      // Clean up the socket
      if (socket) {
        socket.close();
      }
      
      // Clear any reconnection timeout
      if (reconnectTimeoutId.current) {
        clearTimeout(reconnectTimeoutId.current);
      }
    };
  }, [socket, createWebSocketConnection, checkServerStatus]);
  
  // Process file data to ensure it's valid
  const processFileData = useCallback((fileData) => {
    if (!fileData) return null;
    
    // Ensure we have the required fields - check both lowercase and uppercase property names
    const id = fileData.id || fileData.Id;
    const fileName = fileData.fileName || fileData.FileName;
    const size = fileData.size || fileData.Size;
    
    if (!id || !fileName || !size) {
      console.error('Invalid file data received:', fileData);
      return null;
    }
    
    return {
      id: id,
      fileName: fileName,
      filePath: fileData.filePath || fileData.FilePath || '',
      directory: fileData.directory || fileData.Directory || '',
      size: size,
      receivedDate: fileData.receivedDate || fileData.ReceivedDate || new Date().toISOString(),
      sender: fileData.sender || fileData.Sender || 'Unknown'
    };
  }, []);

  // Handle WebSocket messages
  const handleMessage = useCallback((event) => {
    try {
      const message = JSON.parse(event.data);
      console.log('WebSocket message received:', message);
      
      if (message.type === 'server_status') {
        // Only update if the status has changed
        if (lastServerStatus.current.isRunning !== message.data.isRunning || 
            lastServerStatus.current.port !== message.data.port) {
          lastServerStatus.current = { 
            isRunning: message.data.isRunning, 
            port: message.data.port || 0 
          };
          setServerStatus(lastServerStatus.current);
        }
      } else if (message.type === 'server_started') {
        lastServerStatus.current = { isRunning: true, port: message.data.port || 0 };
        setServerStatus(lastServerStatus.current);
      } else if (message.type === 'server_stopped') {
        lastServerStatus.current = { isRunning: false, port: 0 };
        setServerStatus(lastServerStatus.current);
      } else if (message.type === 'received_files') {
        // Process the received files list
        console.log('Received files list:', message.data);
        
        if (Array.isArray(message.data)) {
          const validFiles = message.data
            .map(processFileData)
            .filter(file => file !== null);
          
          setReceivedFiles(validFiles);
        }
      } else if (message.type === 'file_received') {
        // Process the received file
        const fileData = processFileData(message.data);
        
        if (fileData) {
          // Check if this file is already in the list (avoid duplicates)
          setReceivedFiles(prevFiles => {
            const existingFile = prevFiles.find(f => f.id === fileData.id);
            if (existingFile) {
              return prevFiles;
            }
            return [...prevFiles, fileData];
          });
        }
      } else if (message.type === 'transfer_started' || 
                message.type === 'transfer_progress' || 
                message.type === 'transfer_completed' || 
                message.type === 'transfer_failed') {
        // Update transfer history
        setTransferHistory(prev => {
          const updatedHistory = [...prev];
          const existingIndex = updatedHistory.findIndex(t => 
            t.id === message.data.id || 
            (t.fileName === message.data.fileName && t.targetHost === message.data.targetHost)
          );
          
          if (existingIndex >= 0) {
            updatedHistory[existingIndex] = {
              ...updatedHistory[existingIndex],
              ...message.data,
              status: message.type.replace('transfer_', ''),
              lastUpdated: new Date().toISOString()
            };
          } else {
            updatedHistory.push({
              ...message.data,
              id: message.data.id || `transfer-${Date.now()}`,
              status: message.type.replace('transfer_', ''),
              lastUpdated: new Date().toISOString()
            });
          }
          
          return updatedHistory;
        });
      } else if (message.type === 'queue_status') {
        // Update queue status
        setQueueStatus({
          isProcessing: message.data.isProcessing,
          count: message.data.count || 0
        });
      }
      
      // Add to events history
      setEvents(prev => [...prev, message]);
    } catch (error) {
      console.error('Error handling WebSocket message:', error);
    }
  }, [processFileData]);

  // Handle WebSocket events
  const handleEvent = (event) => {
    switch (event.type) {
      case 'server_status':
        console.log('Received server_status event:', event.data);
        // Handle different property name formats (camelCase vs PascalCase)
        const isRunning = event.data.isRunning !== undefined ? event.data.isRunning : 
                          event.data.IsRunning !== undefined ? event.data.IsRunning : false;
        const port = event.data.port || event.data.Port || 0;
        setServerStatus({ isRunning, port });
        break;
        
      case 'server_started':
        console.log('Received server_started event:', event.data);
        setServerStatus({ isRunning: true, port: event.data.port || event.data.Port || 0 });
        break;
        
      case 'server_stopped':
        console.log('Received server_stopped event:', event.data);
        setServerStatus({ isRunning: false, port: 0 });
        break;
        
      case 'received_files':
        // Handle initial list of received files
        console.log('Received files event with data:', event.data);
        
        if (Array.isArray(event.data)) {
          // Filter out invalid files and process the data
          const validFiles = event.data
            .map(file => {
              console.log('Processing file:', file);
              // Process the file data first to ensure all fields are populated
              const processed = processFileData(file);
              console.log('Processed file data:', processed);
              return processed;
            })
            .filter(file => {
              if (!file) {
                console.log('Filtering out null file');
                return false;
              }
              if (!file.fileName) {
                console.log('Filtering out file with no fileName:', file);
                return false;
              }
              if (file.size <= 0) {
                console.log('Filtering out file with invalid size:', file);
                return false;
              }
              return true;
            });
            
          console.log('Received files list after processing:', validFiles);
          
          // Always update the state with the received files, even if empty
          setReceivedFiles(validFiles);
        } else {
          console.warn('Received files event with non-array data:', event.data);
        }
        break;
        
      case 'file_received':
        // Process the file data
        const fileData = processFileData(event.data);
        
        if (fileData && fileData.fileName && fileData.size > 0) {
          console.log('Received new file:', fileData);
          
          // Check if this file already exists in the list by comparing filePath
          setReceivedFiles(prev => {
            const exists = prev.some(f => 
              f.filePath === fileData.filePath && 
              f.size === fileData.size
            );
            
            if (exists) {
              return prev; // File already exists, don't add it again
            }
            return [...prev, fileData];
          });
        }
        break;
        
      case 'transfer_started':
      case 'transfer_completed':
      case 'transfer_failed':
        // Ensure we preserve the speedLimit property
        const transferData = {
          ...event.data,
          // Make sure speedLimit is preserved in both camelCase and PascalCase
          speedLimit: event.data.speedLimit || event.data.SpeedLimit,
          SpeedLimit: event.data.speedLimit || event.data.SpeedLimit
        };
        updateTransferHistory(transferData);
        break;
        
      case 'transfer_queued':
        setQueueStatus(prev => ({ ...prev, count: prev.count + 1 }));
        updateTransferHistory({
          ...event.data,
          status: 'Queued',
          startTime: event.data.queuedAt
        });
        break;
        
      case 'queue_started':
        setQueueStatus(prev => ({ ...prev, isProcessing: true }));
        break;
        
      case 'queue_stopped':
        setQueueStatus(prev => ({ ...prev, isProcessing: false }));
        break;
        
      case 'queue_cleared':
        setQueueStatus({ isProcessing: false, count: 0 });
        // Update all queued transfers to cancelled
        setTransferHistory(prev => 
          prev.map(item => 
            item.status === 'Queued' 
              ? { ...item, status: 'Cancelled', endTime: new Date().toISOString() } 
              : item
          )
        );
        break;
        
      default:
        // Unhandled event type
        break;
    }
  };
  
  // Update the transfer history with a new or updated transfer
  const updateTransferHistory = (transfer) => {
    setTransferHistory(prev => {
      // Check if this transfer is already in the history
      const index = prev.findIndex(item => item.id === transfer.id);
      
      if (index >= 0) {
        // Update existing transfer but preserve important properties that might not be in the update
        const existingTransfer = prev[index];
        const updated = [...prev];
        
        // Preserve speedLimit if it exists in the original but not in the update
        const speedLimit = transfer.speedLimit || transfer.SpeedLimit || existingTransfer.speedLimit || existingTransfer.SpeedLimit;
        
        updated[index] = { 
          ...existingTransfer, 
          ...transfer,
          // Explicitly preserve these properties if they're not in the update
          speedLimit: speedLimit,
          SpeedLimit: speedLimit // Include both casing variants for compatibility
        };
        
        return updated;
      } else {
        // Add new transfer
        return [...prev, transfer];
      }
    });
  };
  
  // Send a message to the WebSocket server
  const sendMessage = useCallback((type, data) => {
    if (socket && socket.readyState === WebSocket.OPEN) {
      const message = JSON.stringify({ type, data });
      socket.send(message);
      return true;
    }
    return false;
  }, [socket]);
  
  // Provide the WebSocket context
  const contextValue = {
    connected,
    serverStatus,
    receivedFiles,
    transferHistory,
    queueStatus,
    sendMessage
  };

  return (
    <WebSocketContext.Provider value={contextValue}>
      {children}
    </WebSocketContext.Provider>
  );
} 