import { useTheme, THEMES } from './ThemeContext';

export function ThemeIndicator() {
  const { theme, changeTheme } = useTheme();
  
  const themes = [THEMES.DARK, THEMES.LIGHT, THEMES.HC_DARK, THEMES.HC_LIGHT];
  
  const handleClick = () => {
    const currentIndex = themes.indexOf(theme);
    const nextIndex = (currentIndex + 1) % themes.length;
    changeTheme(themes[nextIndex]);
  };
  
  return (
    <div id="theme-indicator" onClick={handleClick}>
      theme: {theme}
    </div>
  );
} 