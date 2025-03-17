import { useState, useEffect, useRef } from 'react';
import { serverApi } from '../../api/apiService';
import { Pagination } from '../common/Pagination';

export function ReceivedFilesList() {
  const [files, setFiles] = useState([]);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState('');
  const [currentPage, setCurrentPage] = useState(1);
  const [itemsPerPage] = useState(5);
  const prevFilesLengthRef = useRef(0);

  // Fetch received files on component mount
  useEffect(() => {
    const fetchFiles = async () => {
      try {
        setIsLoading(true);
        const response = await serverApi.getFiles();
        
        // Sort files by receivedDate in descending order (newest first)
        const sortedFiles = [...(response.files || [])].sort((a, b) => {
          const dateA = new Date(a.receivedDate || 0);
          const dateB = new Date(b.receivedDate || 0);
          return dateB - dateA; // Descending order
        });
        
        // Only reset to first page when the number of files changes significantly
        // This prevents resetting pagination when just refreshing the same data
        const newFilesLength = sortedFiles.length;
        if (Math.abs(newFilesLength - prevFilesLengthRef.current) > 1) {
          setCurrentPage(1);
        }
        prevFilesLengthRef.current = newFilesLength;
        
        setFiles(sortedFiles);
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

  // Get current page files
  const indexOfLastFile = currentPage * itemsPerPage;
  const indexOfFirstFile = indexOfLastFile - itemsPerPage;
  const currentFiles = files.slice(indexOfFirstFile, indexOfLastFile);
  const totalPages = Math.ceil(files.length / itemsPerPage);

  // Change page
  const handlePageChange = (pageNumber) => {
    setCurrentPage(pageNumber);
  };

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
        <>
          <div className="files-count" style={{ color: 'var(--dim)', fontSize: '0.8rem', marginBottom: '1rem', textAlign: 'right' }}>
            Showing {indexOfFirstFile + 1}-{Math.min(indexOfLastFile, files.length)} of {files.length} files
          </div>
          
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
              {currentFiles.map((file) => (
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
          
          <Pagination 
            currentPage={currentPage}
            totalPages={totalPages}
            onPageChange={handlePageChange}
          />
        </>
      )}
    </div>
  );
} 