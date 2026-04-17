namespace Central.Engine.Services;

/// <summary>
/// Global re-entrancy guard for UI commands.
/// Prevents rapid-fire button clicks from starting multiple concurrent operations.
/// Use: if (!CommandGuard.TryEnter("AddTask")) return; try { ... } finally { CommandGuard.Exit("AddTask"); }
/// </summary>
public static class CommandGuard
{
    private static readonly HashSet<string> _running = new();
    private static readonly object _lock = new();

    /// <summary>Try to enter a named command. Returns false if already running.</summary>
    public static bool TryEnter(string commandName)
    {
        lock (_lock)
        {
            if (_running.Contains(commandName)) return false;
            _running.Add(commandName);
            return true;
        }
    }

    /// <summary>Exit a named command. Must be called in finally block.</summary>
    public static void Exit(string commandName)
    {
        lock (_lock) { _running.Remove(commandName); }
    }

    /// <summary>Check if a command is currently running.</summary>
    public static bool IsRunning(string commandName)
    {
        lock (_lock) { return _running.Contains(commandName); }
    }

    /// <summary>Execute an async action with re-entrancy protection.</summary>
    public static async Task RunAsync(string commandName, Func<Task> action)
    {
        if (!TryEnter(commandName)) return;
        try { await action(); }
        finally { Exit(commandName); }
    }

    /// <summary>Execute a sync action with re-entrancy protection.</summary>
    public static void Run(string commandName, Action action)
    {
        if (!TryEnter(commandName)) return;
        try { action(); }
        finally { Exit(commandName); }
    }
}
