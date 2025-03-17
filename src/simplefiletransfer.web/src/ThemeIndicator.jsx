import { useTheme, THEMES } from './ThemeContext';

export function ThemeIndicator() {
  const { theme, changeTheme } = useTheme();
  
  const themes = [
    THEMES.TOKYO_NIGHT, 
    THEMES.TOKYO_LIGHT, 
    THEMES.DARK, 
    THEMES.LIGHT,
    THEMES.HC_DARK,
    THEMES.HC_LIGHT
  ];
  
  const handleClick = () => {
    const currentIndex = themes.indexOf(theme);
    const nextIndex = (currentIndex + 1) % themes.length;
    changeTheme(themes[nextIndex]);
  };
  
  // Get a more user-friendly name for the theme
  const getThemeName = (themeValue) => {
    switch(themeValue) {
      case THEMES.TOKYO_NIGHT: return 'Tokyo Night';
      case THEMES.TOKYO_LIGHT: return 'Tokyo Light';
      case THEMES.DARK: return 'Dark';
      case THEMES.LIGHT: return 'Light';
      case THEMES.HC_DARK: return 'HC Dark';
      case THEMES.HC_LIGHT: return 'HC Light';
      default: return themeValue;
    }
  };
  
  return (
    <div id="theme-indicator" onClick={handleClick}>
      theme: {getThemeName(theme)}
    </div>
  );
} 