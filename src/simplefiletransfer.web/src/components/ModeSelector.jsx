import { useState, useEffect } from 'react';

export function ModeSelector({ mode, onModeChange }) {
  const [selectedMode, setSelectedMode] = useState(mode || 'server');
  
  // Update local state when prop changes
  useEffect(() => {
    if (mode) {
      setSelectedMode(mode);
    }
  }, [mode]);

  const handleModeChange = (mode) => {
    setSelectedMode(mode);
    onModeChange(mode);
  };

  return (
    <div className="mode-selector">
      <button
        className={`mode-button ${selectedMode === 'server' ? 'active' : ''}`}
        onClick={() => handleModeChange('server')}
      >
        Server Mode
      </button>
      <button
        className={`mode-button ${selectedMode === 'client' ? 'active' : ''}`}
        onClick={() => handleModeChange('client')}
      >
        Client Mode
      </button>
    </div>
  );
} 