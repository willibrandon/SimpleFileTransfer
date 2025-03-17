import { useTheme, THEMES } from './ThemeContext';

export function ThemeSwitcher() {
  const { theme, changeTheme } = useTheme();

  return (
    <div className="theme-switcher">
      <button
        className={`theme-button dark ${theme === THEMES.DARK ? 'active' : ''}`}
        onClick={() => changeTheme(THEMES.DARK)}
        title="Dark Theme"
        aria-label="Switch to Dark Theme"
      />
      <button
        className={`theme-button light ${theme === THEMES.LIGHT ? 'active' : ''}`}
        onClick={() => changeTheme(THEMES.LIGHT)}
        title="Light Theme"
        aria-label="Switch to Light Theme"
      />
      <button
        className={`theme-button hc-dark ${theme === THEMES.HC_DARK ? 'active' : ''}`}
        onClick={() => changeTheme(THEMES.HC_DARK)}
        title="High Contrast Dark Theme"
        aria-label="Switch to High Contrast Dark Theme"
      />
      <button
        className={`theme-button hc-light ${theme === THEMES.HC_LIGHT ? 'active' : ''}`}
        onClick={() => changeTheme(THEMES.HC_LIGHT)}
        title="High Contrast Light Theme"
        aria-label="Switch to High Contrast Light Theme"
      />
    </div>
  );
} 