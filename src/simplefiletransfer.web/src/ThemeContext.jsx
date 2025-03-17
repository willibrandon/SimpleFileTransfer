import { createContext, useState, useEffect, useContext } from 'react';

// Available themes
export const THEMES = {
  DARK: 'dark',
  LIGHT: 'light',
  HC_DARK: 'hc-dark',
  HC_LIGHT: 'hc-light'
};

// Create the context
export const ThemeContext = createContext();

// Theme provider component
export function ThemeProvider({ children }) {
  // Get initial theme from localStorage or system preference
  const getInitialTheme = () => {
    const savedTheme = localStorage.getItem('theme');
    if (savedTheme) {
      return savedTheme;
    }
    
    // Check for system preference
    if (window.matchMedia && window.matchMedia('(prefers-color-scheme: light)').matches) {
      return THEMES.LIGHT;
    }
    
    // Default to dark theme
    return THEMES.DARK;
  };

  const [theme, setTheme] = useState(getInitialTheme);

  // Apply theme to document
  useEffect(() => {
    document.documentElement.setAttribute('data-theme', theme);
    localStorage.setItem('theme', theme);
  }, [theme]);

  // Function to change theme
  const changeTheme = (newTheme) => {
    setTheme(newTheme);
  };

  return (
    <ThemeContext.Provider value={{ theme, changeTheme }}>
      {children}
    </ThemeContext.Provider>
  );
}

// Custom hook to use the theme context
export function useTheme() {
  const context = useContext(ThemeContext);
  if (context === undefined) {
    throw new Error('useTheme must be used within a ThemeProvider');
  }
  return context;
} 