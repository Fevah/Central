namespace Central.Engine.Shell;

/// <summary>
/// Sequential startup worker pipeline with progress reporting.
/// Modules register workers; the engine orchestrates them with a splash screen.
///
/// Based on TotalLink's StartupWorkerManager.
/// </summary>
public class StartupWorkerManager
{
    private readonly List<StartupWorkerBase> _workers = new();
    private int _currentIndex;

    /// <summary>Total worker count.</summary>
    public int Total => _workers.Count;

    /// <summary>Current worker index (1-based).</summary>
    public int Current => _currentIndex + 1;

    /// <summary>Overall progress 0–100.</summary>
    public int OverallProgress => Total > 0 ? (int)((double)_currentIndex / Total * 100) : 0;

    /// <summary>Current step description.</summary>
    public string CurrentStep { get; private set; } = "";

    /// <summary>Fires on each progress update (step name, overall %).</summary>
    public event Action<string, int>? ProgressChanged;

    /// <summary>Fires when all workers complete.</summary>
    public event Action? Completed;

    /// <summary>Fires if a worker fails (worker, exception).</summary>
    public event Action<StartupWorkerBase, Exception>? WorkerFailed;

    /// <summary>Enqueue a worker to run during startup.</summary>
    public void Enqueue(StartupWorkerBase worker) => _workers.Add(worker);

    /// <summary>Run all workers sequentially.</summary>
    public async Task RunAsync()
    {
        for (_currentIndex = 0; _currentIndex < _workers.Count; _currentIndex++)
        {
            var worker = _workers[_currentIndex];
            CurrentStep = worker.Description;
            ProgressChanged?.Invoke(CurrentStep, OverallProgress);

            try
            {
                await worker.ExecuteAsync(progress =>
                {
                    ProgressChanged?.Invoke(progress, OverallProgress);
                });
            }
            catch (Exception ex)
            {
                WorkerFailed?.Invoke(worker, ex);
                // Continue with next worker unless critical
                if (worker.IsCritical) throw;
            }
        }

        CurrentStep = "Ready";
        ProgressChanged?.Invoke(CurrentStep, 100);
        Completed?.Invoke();
    }
}

/// <summary>
/// Base class for startup workers. Modules create workers to run during splash screen.
/// </summary>
public abstract class StartupWorkerBase
{
    /// <summary>Human-readable description shown during splash.</summary>
    public abstract string Description { get; }

    /// <summary>If true, failure stops the entire startup pipeline.</summary>
    public virtual bool IsCritical => false;

    /// <summary>Execute the worker. Report progress via the callback.</summary>
    public abstract Task ExecuteAsync(Action<string> reportProgress);
}
