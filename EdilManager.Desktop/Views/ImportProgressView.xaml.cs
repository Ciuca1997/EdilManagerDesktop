using System.Windows.Controls;
using EdilManager.Desktop.ViewModels;

namespace EdilManager.Desktop.Views;

public partial class ImportProgressView : UserControl
{
    public ImportProgressView()
    {
        InitializeComponent();
        Loaded += (_, _) => (DataContext as ImportProgressViewModel)?.NotifyShown();
    }
}
