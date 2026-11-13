using Trimx.Core.Documents;

namespace Trimx.Core.Commands;

/// <summary>
/// Merges a layer into the layer directly below it. The two source layers are kept for undo
/// while the merge is applied; the merged layer is kept while it is undone.
/// Takes ownership of all three layers.
/// </summary>
public sealed class MergeLayersCommand : IDocumentCommand
{
    private readonly ImageDocument _document;
    private readonly Layer _upper;
    private readonly Layer _lower;
    private readonly Layer _merged;
    private int _lowerIndex = -1;
    private bool _isApplied;

    public MergeLayersCommand(ImageDocument document, Layer upper, Layer lower, Layer merged)
    {
        _document = document;
        _upper = upper;
        _lower = lower;
        _merged = merged;
    }

    /// <summary>The merged layer while applied; the upper (originally selected) layer after undo.</summary>
    public Layer? TargetLayer => _isApplied ? _merged : _upper;

    public void Execute()
    {
        _lowerIndex = _document.IndexOf(_lower);
        if (_lowerIndex < 0 || _document.IndexOf(_upper) != _lowerIndex + 1)
        {
            throw new InvalidOperationException("The layers must be adjacent in the document.");
        }

        _document.ReplaceLayer(_lower, _merged);
        _document.RemoveLayer(_upper);
        _isApplied = true;
    }

    public void Undo()
    {
        _document.ReplaceLayer(_merged, _lower);
        _document.InsertLayer(_lowerIndex + 1, _upper);
        _isApplied = false;
    }

    public void Dispose()
    {
        if (_isApplied)
        {
            _upper.Dispose();
            _lower.Dispose();
        }
        else
        {
            _merged.Dispose();
        }
    }
}
