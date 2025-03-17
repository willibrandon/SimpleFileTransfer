import { useState, useEffect } from 'react'
import { ServerControlPanel } from './ServerControlPanel'
import { ReceivedFiles } from './ReceivedFiles'
import { useWebSocket } from '../../WebSocketContext'

export function ServerView() {
  const { serverStatus, receivedFiles } = useWebSocket()
  
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
        <ReceivedFiles files={receivedFiles} />
      </div>
    </div>
  )
} 