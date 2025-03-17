import { useState } from 'react';

export function ModeSelector({ onModeChange }) {
  const [selectedMode, setSelectedMode] = useState('client');

  const handleModeChange = (mode) => {
    setSelectedMode(mode);
    onModeChange(mode);
  };

  return (
    <div className="mode-selector">
      <button
        className={`mode-button ${selectedMode === 'client' ? 'active' : ''}`}
        onClick={() => handleModeChange('client')}
      >
        Client Mode
      </button>
      <button
        className={`mode-button ${selectedMode === 'server' ? 'active' : ''}`}
        onClick={() => handleModeChange('server')}
      >
        Server Mode
      </button>
    </div>
  );
} 