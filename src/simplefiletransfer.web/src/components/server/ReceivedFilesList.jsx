import { useState, useEffect } from 'react';
import { serverApi } from '../../api/apiService';

export function ReceivedFilesList() {
  const [files, setFiles] = useState([]);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState('');

  // Fetch received files on component mount
  useEffect(() => {
    const fetchFiles = async () => {
      try {
        setIsLoading(true);
        const response = await serverApi.getFiles();
        setFiles(response.files || []);
        setError('');
      } catch (err) {
        setError('Failed to fetch received files');
        console.error(err);
      } finally {
        setIsLoading(false);
      }
    };

    fetchFiles();
    
    // Set up polling to refresh the file list every 5 seconds
    const interval = setInterval(fetchFiles, 5000);
    
    // Clean up interval on component unmount
    return () => clearInterval(interval);
  }, []);

  const formatFileSize = (bytes) => {
    if (bytes === 0) return '0 Bytes';
    
    const k = 1024;
    const sizes = ['Bytes', 'KB', 'MB', 'GB', 'TB'];
    const i = Math.floor(Math.log(bytes) / Math.log(k));
    
    return parseFloat((bytes / Math.pow(k, i)).toFixed(2)) + ' ' + sizes[i];
  };

  const formatDate = (dateString) => {
    const date = new Date(dateString);
    return date.toLocaleString();
  };

  if (isLoading && !error) {
    return <div className="loading">Loading received files...</div>;
  }

  return (
    <div className="received-files">
      <h2>Received Files</h2>
      
      {error && <div className="error-message">{error}</div>}
      
      {files.length === 0 ? (
        <p className="no-files">No files have been received yet.</p>
      ) : (
        <table className="files-table">
          <thead>
            <tr>
              <th>Filename</th>
              <th>Size</th>
              <th>Received</th>
              <th>Sender</th>
              <th>Options</th>
            </tr>
          </thead>
          <tbody>
            {files.map((file) => (
              <tr key={file.id}>
                <td>{file.fileName}</td>
                <td>{formatFileSize(file.size)}</td>
                <td>{formatDate(file.receivedDate)}</td>
                <td>{file.sender}</td>
                <td>
                  <button 
                    className="open-button"
                    onClick={() => window.open(`file://${file.filePath}`)}
                  >
                    Open
                  </button>
                  <button 
                    className="open-folder-button"
                    onClick={() => window.open(`file://${file.directory}`)}
                  >
                    Open Folder
                  </button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </div>
  );
} 