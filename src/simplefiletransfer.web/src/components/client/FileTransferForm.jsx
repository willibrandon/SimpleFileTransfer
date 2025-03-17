import { useState } from 'react';
import { clientApi } from '../../api/apiService';

export function FileTransferForm() {
  const [sourceFile, setSourceFile] = useState('');
  const [destinationFile, setDestinationFile] = useState('');
  const [isCompressed, setIsCompressed] = useState(false);
  const [isEncrypted, setIsEncrypted] = useState(false);
  const [password, setPassword] = useState('');
  const [transferStatus, setTransferStatus] = useState('');
  const [isTransferring, setIsTransferring] = useState(false);
  const [addToQueue, setAddToQueue] = useState(false);

  const handleTransfer = async () => {
    if (!sourceFile || !destinationFile) {
      setTransferStatus('Please provide both source and destination paths');
      return;
    }

    if (isEncrypted && !password) {
      setTransferStatus('Password is required for encryption');
      return;
    }

    setIsTransferring(true);
    setTransferStatus('Preparing transfer...');

    const transferRequest = {
      sourcePath: sourceFile,
      destinationPath: destinationFile,
      compress: isCompressed,
      encrypt: isEncrypted,
      password: isEncrypted ? password : undefined
    };

    try {
      let response;
      
      if (addToQueue) {
        setTransferStatus('Adding to queue...');
        response = await clientApi.queueTransfer(transferRequest);
        setTransferStatus(`Added to queue successfully! ${response.message || ''}`);
      } else {
        setTransferStatus('Transferring file...');
        response = await clientApi.sendFile(transferRequest);
        setTransferStatus(`Transfer completed successfully! ${response.message || ''}`);
      }
    } catch (error) {
      setTransferStatus(`Error: ${error.message || 'Failed to connect to server'}`);
    } finally {
      setIsTransferring(false);
    }
  };

  return (
    <div className="file-transfer-form">
      <h2>Send File</h2>
      
      <div className="form-group">
        <label htmlFor="sourceFile">Source File Path:</label>
        <input
          type="text"
          id="sourceFile"
          value={sourceFile}
          onChange={(e) => setSourceFile(e.target.value)}
          placeholder="C:\path\to\source\file.txt"
          disabled={isTransferring}
        />
      </div>

      <div className="form-group">
        <label htmlFor="destinationFile">Destination File Path:</label>
        <input
          type="text"
          id="destinationFile"
          value={destinationFile}
          onChange={(e) => setDestinationFile(e.target.value)}
          placeholder="C:\path\to\destination\file.txt"
          disabled={isTransferring}
        />
      </div>

      <div className="options">
        <div className="checkbox-group">
          <input
            type="checkbox"
            id="compress"
            checked={isCompressed}
            onChange={() => setIsCompressed(!isCompressed)}
            disabled={isTransferring}
          />
          <label htmlFor="compress">Compress</label>
        </div>

        <div className="checkbox-group">
          <input
            type="checkbox"
            id="encrypt"
            checked={isEncrypted}
            onChange={() => setIsEncrypted(!isEncrypted)}
            disabled={isTransferring}
          />
          <label htmlFor="encrypt">Encrypt</label>
        </div>
        
        <div className="checkbox-group">
          <input
            type="checkbox"
            id="addToQueue"
            checked={addToQueue}
            onChange={() => setAddToQueue(!addToQueue)}
            disabled={isTransferring}
          />
          <label htmlFor="addToQueue">Add to Queue</label>
        </div>
      </div>

      {isEncrypted && (
        <div className="form-group">
          <label htmlFor="password">Password:</label>
          <input
            type="password"
            id="password"
            value={password}
            onChange={(e) => setPassword(e.target.value)}
            disabled={isTransferring}
          />
        </div>
      )}

      <button 
        className="transfer-button" 
        onClick={handleTransfer}
        disabled={isTransferring}
      >
        {isTransferring ? 'Processing...' : (addToQueue ? 'Add to Queue' : 'Transfer File')}
      </button>

      {transferStatus && (
        <div className={`status ${transferStatus.includes('Error') ? 'error' : transferStatus.includes('successfully') ? 'success' : ''}`}>
          {transferStatus}
        </div>
      )}
    </div>
  );
} 