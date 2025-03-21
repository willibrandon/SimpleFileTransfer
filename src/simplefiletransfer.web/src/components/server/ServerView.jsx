import { useState, useEffect } from 'react'
import { ServerControlPanel } from './ServerControlPanel'
import { ReceivedFiles } from './ReceivedFiles'
import { useWebSocket } from '../../WebSocketContext'
import { serverApi } from '../../api/apiService'

export function ServerView() {
  const { serverStatus, receivedFiles: wsReceivedFiles } = useWebSocket()
  const [receivedFiles, setReceivedFiles] = useState([])
  const [isLoading, setIsLoading] = useState(false)
  
  // Fetch files from the server when it's running
  useEffect(() => {
    const fetchFiles = async () => {
      if (!serverStatus.isRunning) {
        setReceivedFiles([]);
        return;
      }
      
      try {
        setIsLoading(true);
        const response = await serverApi.getFiles();
        let files = [];
        
        if (response && Array.isArray(response.files)) {
          files = response.files;
        } else if (response && Array.isArray(response)) {
          files = response;
        } else {
          console.warn('Unexpected response format from server files API:', response);
          files = [];
        }
        
        // Sort files by receivedDate in descending order (newest first)
        const sortedFiles = [...files].sort((a, b) => {
          const dateA = new Date(a.receivedDate || 0);
          const dateB = new Date(b.receivedDate || 0);
          return dateB - dateA; // Descending order
        });
        
        setReceivedFiles(sortedFiles);
      } catch (error) {
        console.error('Error fetching files:', error);
      } finally {
        setIsLoading(false);
      }
    };
    
    fetchFiles();
    
    // Set up a refresh interval when the server is running
    let interval = null;
    if (serverStatus.isRunning) {
      interval = setInterval(fetchFiles, 10000); // Refresh every 10 seconds
    }
    
    return () => {
      if (interval) clearInterval(interval);
    };
  }, [serverStatus.isRunning]);
  
  // Merge files from WebSocket and API
  useEffect(() => {
    if (wsReceivedFiles && wsReceivedFiles.length > 0) {
      setReceivedFiles(prevFiles => {
        // Create a map of existing files by ID
        const fileMap = new Map(prevFiles.map(file => [file.id, file]));
        
        // Add or update files from WebSocket
        wsReceivedFiles.forEach(file => {
          if (file && file.id) {
            fileMap.set(file.id, file);
          }
        });
        
        // Convert map to array and sort by receivedDate
        const mergedFiles = Array.from(fileMap.values());
        return mergedFiles.sort((a, b) => {
          const dateA = new Date(a.receivedDate || 0);
          const dateB = new Date(b.receivedDate || 0);
          return dateB - dateA; // Descending order
        });
      });
    }
  }, [wsReceivedFiles]);
  
  // Log received files for debugging
  useEffect(() => {
    console.log('ServerView receivedFiles:', receivedFiles);
  }, [receivedFiles]);
  
  return (
    <div className="server-view">
      <h1>Server Mode</h1>
      <p className="description">
        Receive files from other devices. Start the server and share your IP address with others.
      </p>
      
      <div className="server-container">
        <ServerControlPanel 
          isRunning={serverStatus.isRunning} 
          port={serverStatus.port} 
        />
        <ReceivedFiles files={receivedFiles} isLoading={isLoading} />
      </div>
    </div>
  )
} 