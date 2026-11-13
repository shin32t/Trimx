using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Trimx.Core.Documents;

/// <summary>
/// Base class for all layers in an <see cref="ImageDocument"/>.
/// Raises <see cref="PropertyChanged"/> so the owning document and the UI can react to edits.
/// </summary>
public abstract class Layer : INotifyPropertyChanged, IDisposable
{
    private string _name = string.Empty;
    private bool _isVisible = true;
    private float _opacity = 1f;
    private int _x;
    private int _y;

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Name
    {
        get => _name;
        set => SetField(ref _name, value);
    }

    public bool IsVisible
    {
        get => _isVisible;
        set => SetField(ref _isVisible, value);
    }

    /// <summary>Opacity in the range 0.0 to 1.0. Values outside the range are clamped.</summary>
    public float Opacity
    {
        get => _opacity;
        set => SetField(ref _opacity, Math.Clamp(value, 0f, 1f));
    }

    /// <summary>Left position in document pixels.</summary>
    public int X
    {
        get => _x;
        set => SetField(ref _x, value);
    }

    /// <summary>Top position in document pixels.</summary>
    public int Y
    {
        get => _y;
        set => SetField(ref _y, value);
    }

    /// <summary>
    /// Creates an independent copy with the same content and properties (including name).
    /// The caller owns the copy; it does not belong to any document.
    /// </summary>
    public abstract Layer Clone();

    /// <summary>Copies the properties common to all layers onto a clone, without raising change events.</summary>
    protected T CopyBaseTo<T>(T target)
        where T : Layer
    {
        target._name = _name;
        target._isVisible = _isVisible;
        target._opacity = _opacity;
        target._x = _x;
        target._y = _y;
        return target;
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>Releases rendering resources owned by the layer.</summary>
    protected virtual void Dispose(bool disposing)
    {
    }

    protected void RaisePropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }
}
