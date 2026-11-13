namespace Trimx.Core.Commands;

/// <summary>
/// Undo / redo history for one document.
/// </summary>
public sealed class CommandHistory : IDisposable
{
    public const int DefaultCapacity = 100;

    private readonly int _capacity;
    private readonly LinkedList<IDocumentCommand> _undoList = new(); // Oldest first.
    private readonly Stack<IDocumentCommand> _redoStack = new();

    // True while the most recent command may absorb the next one. Closed by EndMerge, undo and redo.
    private bool _canMerge;

    public CommandHistory(int capacity = DefaultCapacity)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);
        _capacity = capacity;
    }

    // The latest command when the document was last saved (null = nothing had been done).
    // Comparing it with the current latest command tells whether the document differs from the saved state,
    // and undoing back to it makes the document clean again.
    private IDocumentCommand? _savePoint;

    // True once the command at the save point has left the history (trimmed or discarded as redo),
    // so the saved state can no longer be reached.
    private bool _savePointLost;

    public bool CanUndo => _undoList.Count > 0;

    public bool CanRedo => _redoStack.Count > 0;

    /// <summary>True if the document differs from the state recorded by <see cref="MarkSaved"/>.</summary>
    public bool IsModified => _savePointLost || !ReferenceEquals(_undoList.Last?.Value, _savePoint);

    /// <summary>Records the current state as saved. The next edit always starts a new undo step.</summary>
    public void MarkSaved()
    {
        _savePoint = _undoList.Last?.Value;
        _savePointLost = false;
        _canMerge = false;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Raised when the undo or redo availability may have changed.</summary>
    public event EventHandler? Changed;

    /// <summary>
    /// Executes a command and records it. Clears the redo history.
    /// </summary>
    /// <param name="command">The command to run.</param>
    /// <param name="allowMerge">
    /// True for one step of a continuous edit (slider drag, typing, dragging a layer): it is merged
    /// into the previous step of the same edit. Call <see cref="EndMerge"/> when the edit ends.
    /// Discrete actions (clicks) leave it false so each stays a separate undo step.
    /// </param>
    public void Execute(IDocumentCommand command, bool allowMerge = false)
    {
        ArgumentNullException.ThrowIfNull(command);

        command.Execute();
        ClearRedo();

        if (allowMerge && _canMerge && _undoList.Last?.Value is IMergeableCommand last && last.TryMerge(command))
        {
            command.Dispose();
        }
        else
        {
            _undoList.AddLast(command);
            TrimToCapacity();
        }

        _canMerge = allowMerge && command is IMergeableCommand;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Ends the current continuous edit so the next command starts a new undo step.</summary>
    public void EndMerge()
    {
        _canMerge = false;
    }

    /// <summary>Undoes the latest command and returns it, or null if there is none.</summary>
    public IDocumentCommand? Undo()
    {
        if (_undoList.Last is not { } node)
        {
            return null;
        }

        _undoList.RemoveLast();
        var command = node.Value;
        command.Undo();
        _redoStack.Push(command);
        _canMerge = false;
        Changed?.Invoke(this, EventArgs.Empty);
        return command;
    }

    /// <summary>Redoes the latest undone command and returns it, or null if there is none.</summary>
    public IDocumentCommand? Redo()
    {
        if (!_redoStack.TryPop(out var command))
        {
            return null;
        }

        command.Execute();
        _undoList.AddLast(command);
        _canMerge = false;
        Changed?.Invoke(this, EventArgs.Empty);
        return command;
    }

    public void Dispose()
    {
        ClearRedo();
        foreach (var command in _undoList)
        {
            command.Dispose();
        }

        _undoList.Clear();
    }

    private void ClearRedo()
    {
        while (_redoStack.TryPop(out var command))
        {
            if (ReferenceEquals(command, _savePoint))
            {
                _savePointLost = true;
            }

            command.Dispose();
        }
    }

    private void TrimToCapacity()
    {
        // Old steps are dropped so large removed images do not accumulate indefinitely.
        while (_undoList.Count > _capacity && _undoList.First is { } oldest)
        {
            // Saved at an empty history, or at this very command: that state is no longer reachable by undo.
            if (_savePoint is null || ReferenceEquals(oldest.Value, _savePoint))
            {
                _savePointLost = true;
            }

            _undoList.RemoveFirst();
            oldest.Value.Dispose();
        }
    }
}
