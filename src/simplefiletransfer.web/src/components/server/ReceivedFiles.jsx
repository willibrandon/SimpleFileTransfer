import { useState } from 'react'

export function ReceivedFiles({ files = [] }) {
  const formatFileSize = (bytes) => {
    if (bytes === 0) return '0 Bytes'
    
    const k = 1024
    const sizes = ['Bytes', 'KB', 'MB', 'GB', 'TB']
    const i = Math.floor(Math.log(bytes) / Math.log(k))
    
    return parseFloat((bytes / Math.pow(k, i)).toFixed(2)) + ' ' + sizes[i]
  }
  
  const formatDate = (dateString) => {
    const date = new Date(dateString)
    return date.toLocaleString()
  }
  
  return (
    <div className="received-files">
      <h2>Received Files</h2>
      
      {files.length === 0 ? (
        <div className="no-files">No files received yet</div>
      ) : (
        <table className="files-table">
          <thead>
            <tr>
              <th>File Name</th>
              <th>Size</th>
              <th>Sender</th>
              <th>Received</th>
              <th>Actions</th>
            </tr>
          </thead>
          <tbody>
            {files.map((file) => (
              <tr key={file.id}>
                <td>{file.fileName}</td>
                <td>{formatFileSize(file.size)}</td>
                <td>{file.sender}</td>
                <td>{formatDate(file.receivedDate)}</td>
                <td>
                  <button 
                    className="open-button"
                    onClick={() => window.open(`/api/server/files/${file.id}/download`)}
                  >
                    Open
                  </button>
                  <button 
                    className="open-folder-button"
                    onClick={() => window.open(`file://${file.directory}`)}
                  >
                    Folder
                  </button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </div>
  )
} 