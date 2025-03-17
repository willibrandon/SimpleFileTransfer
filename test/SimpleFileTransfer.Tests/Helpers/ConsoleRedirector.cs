
namespace SimpleFileTransfer.Tests.Helpers;

/// <summary>
/// Helper class to synchronize console redirection across all tests.
/// </summary>
public static class ConsoleRedirector
{
    private static readonly Lock _lock = new();
    private static StringWriter? _consoleWriter;
    private static TextWriter? _originalConsole;
    private static int _referenceCount = 0;

    /// <summary>
    /// Redirects the console output to a StringWriter.
    /// </summary>
    /// <returns>A disposable object that will restore the console output when disposed.</returns>
    public static IDisposable RedirectConsole()
    {
        lock (_lock)
        {
            if (_referenceCount == 0)
            {
                _originalConsole = Console.Out;
                _consoleWriter = new StringWriter();
                Console.SetOut(_consoleWriter);
            }
            
            _referenceCount++;
            
            return new ConsoleRedirectionHandle();
        }
    }

    private class ConsoleRedirectionHandle : IDisposable
    {
        private bool _disposed = false;

        public void Dispose()
        {
            if (_disposed)
                return;

            lock (_lock)
            {
                _referenceCount--;
                
                if (_referenceCount == 0)
                {
                    if (_originalConsole != null)
                    {
                        try
                        {
                            Console.SetOut(_originalConsole);
                        }
                        catch (Exception)
                        {
                            // Ignore any errors during cleanup
                        }
                    }
                    
                    _consoleWriter?.Dispose();
                    _consoleWriter = null;
                    _originalConsole = null;
                }
                
                _disposed = true;
            }
        }
    }
}
