import { useState } from 'react'
import { useWebSocket } from '../../WebSocketContext'

export function FileTransferForm() {
  const [formData, setFormData] = useState({
    host: '',
    port: 9876,
    file: null,
    useCompression: false,
    useEncryption: false,
    password: '',
    resumeEnabled: true,
    addToQueue: false
  })
  
  const [isSubmitting, setIsSubmitting] = useState(false)
  const { connected } = useWebSocket()
  
  const handleChange = (e) => {
    const { name, value, type, checked, files } = e.target
    
    if (type === 'file') {
      setFormData(prev => ({
        ...prev,
        file: files[0] || null
      }))
    } else if (type === 'checkbox') {
      setFormData(prev => ({
        ...prev,
        [name]: checked
      }))
    } else {
      setFormData(prev => ({
        ...prev,
        [name]: value
      }))
    }
  }
  
  const handleSubmit = async (e) => {
    e.preventDefault()
    
    if (!formData.host) {
      alert('Please enter a host address')
      return
    }
    
    if (!formData.file) {
      alert('Please select a file to transfer')
      return
    }
    
    if (formData.useEncryption && !formData.password) {
      alert('Please enter a password for encryption')
      return
    }
    
    try {
      setIsSubmitting(true)
      
      // Create form data for the API request
      const apiFormData = new FormData()
      apiFormData.append('file', formData.file)
      apiFormData.append('host', formData.host)
      apiFormData.append('port', formData.port)
      apiFormData.append('useCompression', formData.useCompression)
      apiFormData.append('useEncryption', formData.useEncryption)
      
      if (formData.useEncryption) {
        apiFormData.append('password', formData.password)
      }
      
      apiFormData.append('resumeEnabled', formData.resumeEnabled)
      
      // Determine the endpoint based on whether to add to queue
      const endpoint = formData.addToQueue ? '/api/client/queue' : '/api/client/send'
      
      // Send the request
      const response = await fetch(endpoint, {
        method: 'POST',
        body: apiFormData
      })
      
      if (!response.ok) {
        const error = await response.json()
        throw new Error(error.error || 'Failed to transfer file')
      }
      
      // Reset the file input
      setFormData(prev => ({
        ...prev,
        file: null
      }))
      
      // Reset the file input element
      const fileInput = document.getElementById('file')
      if (fileInput) {
        fileInput.value = ''
      }
      
      // Show success message
      alert(formData.addToQueue ? 'File added to queue' : 'File transfer started')
    } catch (error) {
      console.error('Error transferring file:', error)
      alert(`Error: ${error.message}`)
    } finally {
      setIsSubmitting(false)
    }
  }
  
  return (
    <div className="file-transfer-form">
      <h2>Send File</h2>
      
      <form onSubmit={handleSubmit}>
        <div className="form-group">
          <label htmlFor="host">Server Address</label>
          <input
            type="text"
            id="host"
            name="host"
            value={formData.host}
            onChange={handleChange}
            placeholder="IP address or hostname"
            required
            disabled={isSubmitting}
          />
        </div>
        
        <div className="form-group">
          <label htmlFor="port">Port</label>
          <input
            type="number"
            id="port"
            name="port"
            value={formData.port}
            onChange={handleChange}
            min="1"
            max="65535"
            required
            disabled={isSubmitting}
          />
        </div>
        
        <div className="form-group">
          <label htmlFor="file">File to Send</label>
          <input
            type="file"
            id="file"
            name="file"
            onChange={handleChange}
            required
            disabled={isSubmitting}
          />
        </div>
        
        <div className="options">
          <div className="checkbox-group">
            <input
              type="checkbox"
              id="useCompression"
              name="useCompression"
              checked={formData.useCompression}
              onChange={handleChange}
              disabled={isSubmitting}
            />
            <label htmlFor="useCompression">Use Compression</label>
          </div>
          
          <div className="checkbox-group">
            <input
              type="checkbox"
              id="useEncryption"
              name="useEncryption"
              checked={formData.useEncryption}
              onChange={handleChange}
              disabled={isSubmitting}
            />
            <label htmlFor="useEncryption">Use Encryption</label>
          </div>
          
          <div className="checkbox-group">
            <input
              type="checkbox"
              id="resumeEnabled"
              name="resumeEnabled"
              checked={formData.resumeEnabled}
              onChange={handleChange}
              disabled={isSubmitting}
            />
            <label htmlFor="resumeEnabled">Enable Resume</label>
          </div>
          
          <div className="checkbox-group">
            <input
              type="checkbox"
              id="addToQueue"
              name="addToQueue"
              checked={formData.addToQueue}
              onChange={handleChange}
              disabled={isSubmitting}
            />
            <label htmlFor="addToQueue">Add to Queue</label>
          </div>
        </div>
        
        {formData.useEncryption && (
          <div className="form-group">
            <label htmlFor="password">Password</label>
            <input
              type="password"
              id="password"
              name="password"
              value={formData.password}
              onChange={handleChange}
              required={formData.useEncryption}
              disabled={isSubmitting}
            />
          </div>
        )}
        
        <button
          type="submit"
          className="transfer-button"
          disabled={isSubmitting || !formData.host || !formData.file || (formData.useEncryption && !formData.password)}
        >
          {isSubmitting ? 'Processing...' : formData.addToQueue ? 'Add to Queue' : 'Transfer Now'}
        </button>
      </form>
    </div>
  )
} 