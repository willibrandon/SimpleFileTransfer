import { createContext, useContext, useEffect, useState, useRef } from 'react';

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

  // Check server status via API
  const checkServerStatus = async () => {
    try {
      const response = await fetch('/api/server/status');
      if (response.ok) {
        const data = await response.json();
        console.log('Server status from API:', data);
        setServerStatus({ 
          isRunning: data.isRunning, 
          port: data.port || 0 
        });
        return data.isRunning;
      }
    } catch (error) {
      console.error('Error checking server status:', error);
    }
    return false;
  };

  // Function to create a new WebSocket connection
  const createWebSocketConnection = () => {
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
  };

  // Connect to the WebSocket server
  useEffect(() => {
    isUnmounting.current = false;
    
    // Check server status immediately on mount
    const initialStatusCheck = async () => {
      await checkServerStatus();
      hasCheckedServerStatus.current = true;
    };
    initialStatusCheck();
    
    // Create WebSocket connection
    createWebSocketConnection();
    
    // Clean up on unmount
    return () => {
      isUnmounting.current = true;
      
      if (reconnectTimeoutId.current) {
        clearTimeout(reconnectTimeoutId.current);
      }
      
      if (socket) {
        // Use a clean close code to prevent reconnection attempts
        socket.close(1000, "Component unmounting");
      }
    };
  }, []);
  
  // Process file data to ensure it's valid
  const processFileData = (file) => {
    if (!file) {
      console.log('processFileData received null file');
      return null;
    }
    
    console.log('Raw file data:', file);
    
    // Handle different property name formats (camelCase vs PascalCase)
    const rawFileName = file.fileName || file.FileName || '';
    const rawFilePath = file.filePath || file.FilePath || '';
    const rawDirectory = file.directory || file.Directory || '';
    const rawSize = file.size || file.Size || 0;
    const rawSender = file.sender || file.Sender || 'Unknown';
    const rawReceivedDate = file.receivedDate || file.ReceivedDate || new Date().toISOString();
    
    // Generate an ID if one doesn't exist
    const id = file.id || file.Id || `file-${Date.now()}-${Math.random().toString(36).substring(2, 9)}`;
    
    // Extract directory from file path if not provided
    let directory = rawDirectory;
    if (!directory && rawFilePath) {
      const lastSlashIndex = Math.max(
        rawFilePath.lastIndexOf('\\'),
        rawFilePath.lastIndexOf('/')
      );
      if (lastSlashIndex > 0) {
        directory = rawFilePath.substring(0, lastSlashIndex);
      }
    }
    
    return {
      id: id,
      fileName: rawFileName,
      filePath: rawFilePath,
      directory: directory,
      size: typeof rawSize === 'number' ? rawSize : parseInt(rawSize, 10) || 0,
      sender: rawSender,
      receivedDate: rawReceivedDate
    };
  };
  
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
        updateTransferHistory(event.data);
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
        // Update existing transfer
        const updated = [...prev];
        updated[index] = { ...updated[index], ...transfer };
        return updated;
      } else {
        // Add new transfer
        return [...prev, transfer];
      }
    });
  };
  
  // Send a message to the WebSocket server
  const sendMessage = (message) => {
    if (socket && connected) {
      socket.send(JSON.stringify(message));
    } else {
      console.error('Cannot send message: WebSocket not connected');
    }
  };
  
  // The value to provide to consumers
  const value = {
    connected,
    events,
    serverStatus,
    receivedFiles,
    transferHistory,
    queueStatus,
    sendMessage
  };
  
  return (
    <WebSocketContext.Provider value={value}>
      {children}
    </WebSocketContext.Provider>
  );
} 