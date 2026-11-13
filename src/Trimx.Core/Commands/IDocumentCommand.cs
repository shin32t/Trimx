using Trimx.Core.Documents;

namespace Trimx.Core.Commands;

/// <summary>
/// An undoable operation on a document.
/// </summary>
/// <remarks>
/// A command may own resources depending on its state (e.g. a removed layer while the removal is applied).
/// <see cref="IDisposable.Dispose"/> is called when the command leaves the history and must release
/// whatever the command still owns at that point.
/// </remarks>
public interface IDocumentCommand : IDisposable
{
    /// <summary>The layer the command acts on, used to restore selection after undo / redo.</summary>
    Layer? TargetLayer { get; }

    void Execute();

    void Undo();
}
