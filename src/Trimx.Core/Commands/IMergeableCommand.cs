namespace Trimx.Core.Commands;

/// <summary>
/// A command that can absorb a following command of the same kind, so continuous edits
/// (e.g. dragging a slider) become a single undo step.
/// </summary>
public interface IMergeableCommand : IDocumentCommand
{
    /// <summary>
    /// Absorbs <paramref name="next"/> (already executed) if compatible.
    /// Returns false when the commands cannot be combined.
    /// </summary>
    bool TryMerge(IDocumentCommand next);
}
