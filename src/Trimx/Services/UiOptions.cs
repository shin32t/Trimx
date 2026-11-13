using System.ComponentModel;

namespace Trimx.Services;

/// <summary>
/// Display options the XAML binds to directly, so a change in the settings dialog shows at once.
/// </summary>
public sealed class UiOptions : INotifyPropertyChanged
{
    private bool _showToolbarLabels;

    public static UiOptions Instance { get; } = new();

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Whether toolbar buttons show their name under the icon.</summary>
    public bool ShowToolbarLabels
    {
        get => _showToolbarLabels;
        set
        {
            if (_showToolbarLabels != value)
            {
                _showToolbarLabels = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ShowToolbarLabels)));
            }
        }
    }
}
