using System.Text.RegularExpressions;
using System.Windows.Controls;
using System.Windows.Input;

namespace EdilManager.Desktop.Views;

public partial class PresenzaFormView : UserControl
{
    private static readonly Regex Consentiti = new(@"^[0-9.,]+$");

    public PresenzaFormView() => InitializeComponent();

    private void OnOrePreviewTextInput(object sender, TextCompositionEventArgs e) =>
        e.Handled = !Consentiti.IsMatch(e.Text);
}
