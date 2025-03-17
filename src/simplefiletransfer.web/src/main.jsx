import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import './index.css'
import './themes.css'
import App from './App.jsx'
import { ThemeProvider } from './ThemeContext'
import { WebSocketProvider } from './WebSocketContext'
import { waitForApiAvailability } from './api/apiService'

// Create a loading element
const renderLoading = () => {
  const root = createRoot(document.getElementById('root'));
  root.render(
    <div style={{ 
      display: 'flex', 
      flexDirection: 'column',
      justifyContent: 'center', 
      alignItems: 'center', 
      height: '100vh',
      fontFamily: 'system-ui, sans-serif'
    }}>
      <h2>Connecting to SimpleFileTransfer backend...</h2>
      <p>Please wait while the server starts up</p>
      <div style={{ 
        width: '50px', 
        height: '50px', 
        border: '5px solid #f3f3f3',
        borderTop: '5px solid #3498db',
        borderRadius: '50%',
        animation: 'spin 1s linear infinite',
        marginTop: '20px'
      }}></div>
      <style>{`
        @keyframes spin {
          0% { transform: rotate(0deg); }
          100% { transform: rotate(360deg); }
        }
      `}</style>
    </div>
  );
  return root;
};

// Initialize the app
const initializeApp = async () => {
  // Show loading screen
  const root = renderLoading();
  
  // Wait for the backend to be available
  await waitForApiAvailability();
  
  // Render the actual app
  root.render(
    <StrictMode>
      <ThemeProvider>
        <WebSocketProvider>
          <App />
        </WebSocketProvider>
      </ThemeProvider>
    </StrictMode>
  );
};

// Start the initialization process
initializeApp().catch(error => {
  console.error('Failed to initialize the app:', error);
  
  // Show error message if backend is not available
  createRoot(document.getElementById('root')).render(
    <div style={{ 
      display: 'flex', 
      flexDirection: 'column',
      justifyContent: 'center', 
      alignItems: 'center', 
      height: '100vh',
      fontFamily: 'system-ui, sans-serif',
      color: '#e74c3c'
    }}>
      <h2>Connection Error</h2>
      <p>Could not connect to the SimpleFileTransfer backend.</p>
      <p>Please make sure the server is running and try again.</p>
      <button 
        onClick={() => window.location.reload()}
        style={{
          marginTop: '20px',
          padding: '10px 20px',
          background: '#3498db',
          color: 'white',
          border: 'none',
          borderRadius: '4px',
          cursor: 'pointer'
        }}
      >
        Retry Connection
      </button>
    </div>
  );
});
