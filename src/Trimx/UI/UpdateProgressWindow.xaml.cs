using System.Windows;

namespace Trimx.UI;

/// <summary>Shows the download progress of an update, with a Cancel button.</summary>
public partial class UpdateProgressWindow : Window
{
    private readonly CancellationTokenSource _cancellation = new();

    public UpdateProgressWindow()
    {
        InitializeComponent();
    }

    /// <summary>Cancelled when the user presses Cancel or closes the window.</summary>
    public CancellationToken Token => _cancellation.Token;

    /// <param name="fraction">Progress from 0 to 1.</param>
    public void SetProgress(double fraction) => Progress.Value = fraction;

    public void SetStatus(string text) => StatusText.Text = text;

    private void OnCancelClick(object sender, RoutedEventArgs e) => _cancellation.Cancel();

    protected override void OnClosed(EventArgs e)
    {
        _cancellation.Cancel();
        _cancellation.Dispose();
        base.OnClosed(e);
    }
}
