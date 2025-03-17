import { useState, useEffect } from 'react'

export function ReceivedFiles({ files = [], isLoading = false }) {
  const [downloadStatus, setDownloadStatus] = useState({});
  const [validFiles, setValidFiles] = useState([]);
  
  // Process files to ensure they have valid data
  useEffect(() => {
    console.log('ReceivedFiles component received files:', files);
    
    if (!Array.isArray(files)) {
      console.error('ReceivedFiles: files prop is not an array', files);
      setValidFiles([]);
      return;
    }
    
    // Filter out invalid files
    const filtered = files.filter(file => 
      file && 
      file.id && 
      file.fileName && 
      file.size > 0
    );
    
    console.log('ReceivedFiles filtered valid files:', filtered);
    setValidFiles(filtered);
  }, [files]);

  const formatFileSize = (bytes) => {
    if (bytes === undefined || bytes === null || bytes === 0) return '0 Bytes'
    
    const k = 1024
    const sizes = ['Bytes', 'KB', 'MB', 'GB', 'TB']
    const i = Math.floor(Math.log(bytes) / Math.log(k))
    
    return parseFloat((bytes / Math.pow(k, i)).toFixed(2)) + ' ' + sizes[i]
  }
  
  const formatDate = (dateString) => {
    if (!dateString) return 'Unknown'
    
    try {
      const date = new Date(dateString)
      if (isNaN(date.getTime())) return 'Invalid Date'
      return date.toLocaleString()
    } catch (error) {
      console.error('Error formatting date:', error)
      return 'Invalid Date'
    }
  }
  
  const openFile = async (fileId) => {
    if (!fileId) {
      console.error('Cannot open file: Missing file ID')
      return
    }
    
    // Use the correct API endpoint for downloading files
    const downloadUrl = `/api/server/files/${fileId}/download`
    
    try {
      // Set download status to loading
      setDownloadStatus(prev => ({ ...prev, [fileId]: 'loading' }))
      
      // Create a hidden anchor element to trigger the download
      const link = document.createElement('a')
      link.href = downloadUrl
      link.target = '_blank'
      link.rel = 'noopener noreferrer'
      
      // Append to the document and click
      document.body.appendChild(link)
      link.click()
      
      // Clean up
      document.body.removeChild(link)
      
      // Set download status to success
      setDownloadStatus(prev => ({ ...prev, [fileId]: 'success' }))
      
      // Clear status after 3 seconds
      setTimeout(() => {
        setDownloadStatus(prev => {
          const newStatus = { ...prev }
          delete newStatus[fileId]
          return newStatus
        })
      }, 3000)
    } catch (error) {
      console.error(`Error downloading file: ${error.message}`)
      
      // Set download status to error
      setDownloadStatus(prev => ({ ...prev, [fileId]: 'error' }))
      
      // Clear error status after 5 seconds
      setTimeout(() => {
        setDownloadStatus(prev => {
          const newStatus = { ...prev }
          delete newStatus[fileId]
          return newStatus
        })
      }, 5000)
    }
  }
  
  const openFolder = async (directory) => {
    if (!directory) {
      console.error('Cannot open folder: Missing directory path')
      return
    }
    
    try {
      // Use the API to open the folder instead of trying to use file:// protocol
      const response = await fetch('/api/server/open-folder', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json'
        },
        body: JSON.stringify({ path: directory })
      })
      
      if (!response.ok) {
        const data = await response.json()
        throw new Error(data.error || 'Failed to open folder')
      }
      
      // Success - no need to do anything else
    } catch (error) {
      console.error('Error opening folder:', error)
      alert('Could not open folder: ' + error.message)
    }
  }
  
  // Helper function to get button text based on download status
  const getButtonText = (fileId) => {
    const status = downloadStatus[fileId]
    if (!status) return 'Open'
    if (status === 'loading') return 'Opening...'
    if (status === 'success') return 'Opened!'
    if (status === 'error') return 'Failed'
    return 'Open'
  }
  
  // Helper function to get button class based on download status
  const getButtonClass = (fileId) => {
    const status = downloadStatus[fileId]
    if (!status) return 'open-button'
    if (status === 'loading') return 'open-button loading'
    if (status === 'success') return 'open-button success'
    if (status === 'error') return 'open-button error'
    return 'open-button'
  }
  
  // CSS styles for button states
  const styles = {
    loading: {
      backgroundColor: '#f0ad4e'
    },
    success: {
      backgroundColor: '#5cb85c'
    },
    error: {
      backgroundColor: '#d9534f'
    }
  };
  
  // Helper function to get button style based on download status
  const getButtonStyle = (fileId) => {
    const status = downloadStatus[fileId];
    if (status && styles[status]) {
      return styles[status];
    }
    return {};
  };
  
  // Render loading state
  if (isLoading && (!validFiles || validFiles.length === 0)) {
    return (
      <div className="received-files">
        <h2>Received Files</h2>
        <div className="loading">Loading files...</div>
      </div>
    );
  }
  
  return (
    <div className="received-files">
      <h2>Received Files</h2>
      
      <style>
        {`
          .files-list {
            list-style: none;
            padding: 0;
            margin: 0;
          }
          
          .file-item {
            background-color: var(--bg-highlight);
            margin-bottom: 0.5rem;
            border-radius: 4px;
            padding: 0.75rem;
            border: 1px solid var(--border-color);
          }
          
          .file-item:hover {
            background-color: var(--light-bg);
          }
          
          .file-item-header {
            display: flex;
            justify-content: space-between;
            margin-bottom: 0.5rem;
          }
          
          .file-name {
            font-weight: bold;
            font-size: 1rem;
            color: var(--text-color);
          }
          
          .file-size {
            color: var(--dim);
            font-size: 0.9rem;
          }
          
          .file-item-details {
            display: grid;
            grid-template-columns: repeat(2, 1fr);
            gap: 0.5rem;
            font-size: 0.9rem;
            color: var(--text-color);
            margin-bottom: 0.75rem;
          }
          
          .detail-label {
            color: var(--dim);
          }
          
          .file-item-actions {
            display: flex;
            justify-content: flex-end;
            gap: 0.5rem;
          }
          
          .open-button, .open-folder-button {
            padding: 0.3rem 0.6rem;
            font-size: 0.8rem;
            min-width: 60px;
            border-radius: 4px;
            border: none;
            cursor: pointer;
            color: white;
          }
          
          .open-button {
            background-color: var(--primary-color);
          }
          
          .open-folder-button {
            background-color: var(--dim);
          }
          
          .no-files {
            text-align: center;
            padding: 2rem;
            color: var(--dim);
            background-color: var(--bg-highlight);
            border-radius: 4px;
            margin-top: 1rem;
          }
        `}
      </style>
      
      {!validFiles || validFiles.length === 0 ? (
        <div className="no-files">No files received yet</div>
      ) : (
        <ul className="files-list">
          {validFiles.map((file) => (
            <li key={file.id} className="file-item">
              <div className="file-item-header">
                <span className="file-name">{file.fileName}</span>
                <span className="file-size">{formatFileSize(file.size)}</span>
              </div>
              
              <div className="file-item-details">
                <div>
                  <span className="detail-label">Sender: </span>
                  {file.sender || 'Unknown'}
                </div>
                
                <div>
                  <span className="detail-label">Received: </span>
                  {formatDate(file.receivedDate)}
                </div>
              </div>
              
              <div className="file-item-actions">
                <button 
                  className={getButtonClass(file.id)}
                  style={getButtonStyle(file.id)}
                  onClick={() => openFile(file.id)}
                  disabled={downloadStatus[file.id] === 'loading'}
                >
                  {getButtonText(file.id)}
                </button>
                <button 
                  className="open-folder-button"
                  onClick={() => openFolder(file.directory)}
                  disabled={!file.directory}
                >
                  Folder
                </button>
              </div>
            </li>
          ))}
        </ul>
      )}
    </div>
  )
} 