import { createContext, useContext, useEffect, useState, useRef } from 'react';

// Create a context for the WebSocket
const WebSocketContext = createContext(null);

// Custom hook to use the WebSocket context
export const useWebSocket = () => useContext(WebSocketContext);

// Silent logger function that does nothing in production
const logger = {
  log: process.env.NODE_ENV === 'development' ? console.log : () => {},
  error: process.env.NODE_ENV === 'development' ? console.error : () => {}
};

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
      logger.log('WebSocket connected');
    };
    
    newSocket.onclose = (event) => {
      setConnected(false);
      
      // Don't log normal closures
      if (event.code !== 1000) {
        logger.log('WebSocket disconnected, will attempt to reconnect...');
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
        logger.log('WebSocket event:', event);
        
        // Add the event to the events list
        setEvents(prev => [...prev, event]);
        
        // Handle specific event types
        handleEvent(event);
      } catch (error) {
        logger.error('Error parsing WebSocket message:', error);
      }
    };
    
    // Save the socket to state
    setSocket(newSocket);
  };

  // Connect to the WebSocket server
  useEffect(() => {
    isUnmounting.current = false;
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
  
  // Handle WebSocket events
  const handleEvent = (event) => {
    switch (event.type) {
      case 'server_status':
        setServerStatus(event.data);
        break;
        
      case 'server_started':
        setServerStatus({ isRunning: true, port: event.data.port });
        break;
        
      case 'server_stopped':
        setServerStatus({ isRunning: false, port: 0 });
        break;
        
      case 'received_files':
        // Handle initial list of received files
        if (Array.isArray(event.data)) {
          // Use the server's file IDs directly
          const formattedFiles = event.data.map(file => ({
            id: file.id,
            fileName: file.fileName || 'Unknown',
            size: file.size || 0,
            sender: file.sender || 'Unknown',
            receivedDate: file.receivedDate || new Date().toISOString(),
            directory: file.directory || null
          }));
          setReceivedFiles(formattedFiles);
        }
        break;
        
      case 'file_received':
        // Use the server's file ID directly
        const fileData = {
          id: event.data.id,
          fileName: event.data.fileName || 'Unknown',
          size: event.data.size || 0,
          sender: event.data.sender || 'Unknown',
          receivedDate: event.data.receivedDate || new Date().toISOString(),
          directory: event.data.directory || null
        };
        setReceivedFiles(prev => [...prev, fileData]);
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
      logger.error('Cannot send message: WebSocket not connected');
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