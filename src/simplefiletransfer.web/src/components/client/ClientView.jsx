import { useState } from 'react'
import { FileTransferForm } from './FileTransferForm'
import { QueueManager } from './QueueManager'
import { TransferHistory } from './TransferHistory'
import { useWebSocket } from '../../WebSocketContext'

export function ClientView() {
  const { transferHistory, queueStatus } = useWebSocket()
  
  return (
    <div className="client-view">
      <h1>Client Mode</h1>
      <p className="description">
        Send files to other devices. Enter the server IP address and select files to transfer.
      </p>
      
      <div className="client-container">
        <div className="client-main">
          <FileTransferForm />
          <QueueManager 
            isProcessing={queueStatus.isProcessing}
            count={queueStatus.count}
          />
        </div>
        <div className="client-history">
          <TransferHistory transfers={transferHistory} />
        </div>
      </div>
    </div>
  )
} 