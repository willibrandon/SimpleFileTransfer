import { FileTransferForm } from './FileTransferForm';
import { QueueManager } from './QueueManager';
import { TransferHistory } from './TransferHistory';

export function ClientView() {
  return (
    <div className="client-view">
      <h1>File Transfer Client</h1>
      <p className="description">
        Send files to a remote server with optional compression and encryption.
      </p>
      
      <div className="client-container">
        <div className="client-main">
          <FileTransferForm />
          <QueueManager />
        </div>
        
        <div className="client-history">
          <TransferHistory />
        </div>
      </div>
    </div>
  );
} 