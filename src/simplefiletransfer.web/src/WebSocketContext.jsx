import { createContext, useContext, useEffect, useState } from 'react';

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

  // Connect to the WebSocket server
  useEffect(() => {
    // Determine the WebSocket URL based on the current location
    const protocol = window.location.protocol === 'https:' ? 'wss:' : 'ws:';
    const wsUrl = `${protocol}//${window.location.host}/ws`;
    
    // Create a new WebSocket connection
    const newSocket = new WebSocket(wsUrl);
    
    // Set up event handlers
    newSocket.onopen = () => {
      console.log('WebSocket connected');
      setConnected(true);
    };
    
    newSocket.onclose = () => {
      console.log('WebSocket disconnected');
      setConnected(false);
      
      // Try to reconnect after a delay
      setTimeout(() => {
        setSocket(null);
      }, 5000);
    };
    
    newSocket.onerror = (error) => {
      console.error('WebSocket error:', error);
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
    
    // Clean up on unmount
    return () => {
      if (newSocket) {
        newSocket.close();
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
        
      case 'file_received':
        setReceivedFiles(prev => [...prev, event.data]);
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