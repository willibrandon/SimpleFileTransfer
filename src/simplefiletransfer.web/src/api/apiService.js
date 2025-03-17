/**
 * API Service for SimpleFileTransfer
 * Handles all communication with the backend API
 */

const API_BASE_URL = '/api';
const HEALTH_CHECK_URL = '/health';
const MAX_RETRIES = 10;
const RETRY_DELAY = 1000; // 1 second

/**
 * Check if the backend API is available
 * @returns {Promise<boolean>} True if the API is available, false otherwise
 */
export const checkApiHealth = async () => {
  try {
    const response = await fetch(HEALTH_CHECK_URL);
    return response.ok;
  } catch (error) {
    console.warn('Health check failed:', error);
    return false;
  }
};

/**
 * Wait for the backend API to become available
 * @returns {Promise<boolean>} True if the API became available, false if it timed out
 */
export const waitForApiAvailability = async () => {
  console.log('Waiting for backend API to become available...');
  
  for (let i = 0; i < MAX_RETRIES; i++) {
    const isAvailable = await checkApiHealth();
    if (isAvailable) {
      console.log('Backend API is available!');
      return true;
    }
    
    console.log(`Backend not ready, retrying in ${RETRY_DELAY}ms... (${i + 1}/${MAX_RETRIES})`);
    await new Promise(resolve => setTimeout(resolve, RETRY_DELAY));
  }
  
  console.error('Backend API is not available after maximum retries');
  return false;
};

// Server API endpoints
export const serverApi = {
  // Get server status
  getStatus: async () => {
    try {
      const response = await fetch(`${API_BASE_URL}/server/status`);
      return await response.json();
    } catch (error) {
      console.error('Error fetching server status:', error);
      throw error;
    }
  },

  // Start the server
  start: async (config) => {
    try {
      const response = await fetch(`${API_BASE_URL}/server/start`, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify(config),
      });
      return await response.json();
    } catch (error) {
      console.error('Error starting server:', error);
      throw error;
    }
  },

  // Stop the server
  stop: async () => {
    try {
      const response = await fetch(`${API_BASE_URL}/server/stop`, {
        method: 'POST',
      });
      return await response.json();
    } catch (error) {
      console.error('Error stopping server:', error);
      throw error;
    }
  },

  // Get server configuration
  getConfig: async () => {
    try {
      const response = await fetch(`${API_BASE_URL}/server/config`);
      return await response.json();
    } catch (error) {
      console.error('Error fetching server config:', error);
      throw error;
    }
  },

  // Update server configuration
  updateConfig: async (config) => {
    try {
      const response = await fetch(`${API_BASE_URL}/server/config`, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify(config),
      });
      return await response.json();
    } catch (error) {
      console.error('Error updating server config:', error);
      throw error;
    }
  },

  // Get list of received files
  getFiles: async () => {
    try {
      const response = await fetch(`${API_BASE_URL}/server/files`);
      return await response.json();
    } catch (error) {
      console.error('Error fetching received files:', error);
      throw error;
    }
  },
};

// Client API endpoints
export const clientApi = {
  // Send a file
  sendFile: async (transferRequest) => {
    try {
      const response = await fetch(`${API_BASE_URL}/transfer`, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify(transferRequest),
      });
      return await response.json();
    } catch (error) {
      console.error('Error sending file:', error);
      throw error;
    }
  },

  // Add a transfer to the queue
  queueTransfer: async (transferRequest) => {
    try {
      const response = await fetch(`${API_BASE_URL}/client/queue`, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify(transferRequest),
      });
      return await response.json();
    } catch (error) {
      console.error('Error queuing transfer:', error);
      throw error;
    }
  },

  // Get queue status
  getQueue: async () => {
    try {
      const response = await fetch(`${API_BASE_URL}/client/queue`);
      return await response.json();
    } catch (error) {
      console.error('Error fetching queue status:', error);
      throw error;
    }
  },

  // Start queue processing
  startQueue: async () => {
    try {
      const response = await fetch(`${API_BASE_URL}/client/queue/start`, {
        method: 'POST',
      });
      return await response.json();
    } catch (error) {
      console.error('Error starting queue:', error);
      throw error;
    }
  },

  // Stop queue processing
  stopQueue: async () => {
    try {
      const response = await fetch(`${API_BASE_URL}/client/queue/stop`, {
        method: 'POST',
      });
      return await response.json();
    } catch (error) {
      console.error('Error stopping queue:', error);
      throw error;
    }
  },

  // Clear the queue
  clearQueue: async () => {
    try {
      const response = await fetch(`${API_BASE_URL}/client/queue/clear`, {
        method: 'POST',
      });
      return await response.json();
    } catch (error) {
      console.error('Error clearing queue:', error);
      throw error;
    }
  },

  // Get transfer history
  getHistory: async () => {
    try {
      console.log('Fetching transfer history from API...');
      const response = await fetch(`${API_BASE_URL}/client/history`);
      
      if (!response.ok) {
        const errorText = await response.text();
        console.error('Error response from history API:', response.status, errorText);
        throw new Error(`Server returned ${response.status}: ${response.statusText}`);
      }
      
      const responseText = await response.text();
      console.log('Raw history API response:', responseText);
      
      if (!responseText || responseText.trim() === '') {
        console.log('Empty response from history API, returning empty array');
        return { items: [] };
      }
      
      try {
        const data = JSON.parse(responseText);
        console.log('Parsed history API response:', data);
        return data;
      } catch (parseError) {
        console.error('Failed to parse history API response:', parseError);
        throw new Error('Invalid JSON response from server');
      }
    } catch (error) {
      console.error('Error fetching transfer history:', error);
      throw error;
    }
  },
}; 