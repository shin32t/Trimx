using Trimx.Core.Documents;

namespace Trimx.Core.Commands;

/// <summary>
/// Changes the canvas size without resampling. Layers keep their pixels and are shifted so the
/// existing content stays at the chosen anchor.
/// </summary>
public sealed class ResizeCanvasCommand : IDocumentCommand
{
    private readonly ImageDocument _document;
    private readonly int _newWidth;
    private readonly int _newHeight;
    private readonly int _anchorX;
    private readonly int _anchorY;
    private int _oldWidth;
    private int _oldHeight;
    private int _dx;
    private int _dy;

    /// <param name="anchorX">0 = left, 1 = center, 2 = right.</param>
    /// <param name="anchorY">0 = top, 1 = middle, 2 = bottom.</param>
    public ResizeCanvasCommand(ImageDocument document, int newWidth, int newHeight, int anchorX, int anchorY)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(newWidth);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(newHeight);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(anchorX, 2);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(anchorY, 2);
        ArgumentOutOfRangeException.ThrowIfNegative(anchorX);
        ArgumentOutOfRangeException.ThrowIfNegative(anchorY);
        _document = document;
        _newWidth = newWidth;
        _newHeight = newHeight;
        _anchorX = anchorX;
        _anchorY = anchorY;
    }

    public Layer? TargetLayer => null;

    public void Execute()
    {
        _oldWidth = _document.Width;
        _oldHeight = _document.Height;
        _dx = (_newWidth - _oldWidth) * _anchorX / 2;
        _dy = (_newHeight - _oldHeight) * _anchorY / 2;

        _document.SetSize(_newWidth, _newHeight);
        Shift(_dx, _dy);
    }

    public void Undo()
    {
        Shift(-_dx, -_dy);
        _document.SetSize(_oldWidth, _oldHeight);
    }

    public void Dispose()
    {
    }

    private void Shift(int dx, int dy)
    {
        if (dx == 0 && dy == 0)
        {
            return;
        }

        foreach (var layer in _document.Layers)
        {
            layer.X += dx;
            layer.Y += dy;
        }
    }
}
