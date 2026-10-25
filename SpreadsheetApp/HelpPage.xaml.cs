namespace SpreadsheetApp;

public partial class HelpPage : ContentPage
{
    public HelpPage()
    {
        InitializeComponent();
    }

    private async void OnCloseClicked(object? sender, EventArgs e) => await Navigation.PopModalAsync();
}
