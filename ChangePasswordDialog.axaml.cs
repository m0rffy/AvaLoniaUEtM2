using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace UETM2;

public partial class ChangePasswordDialog : Window
{
    public string SelectedRole => ((ComboBoxItem?)cmbRole.SelectedItem)?.Content?.ToString() ?? "";
    public string CurrentPassword => txtCurrent.Text ?? "";
    public string NewPassword => txtNew.Text ?? "";

    public ChangePasswordDialog()
    {
        InitializeComponent();
    }

    private async void btnOk_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(txtCurrent.Text) ||
            string.IsNullOrWhiteSpace(txtNew.Text) ||
            string.IsNullOrWhiteSpace(txtConfirm.Text))
        {
            await DialogHelper.ShowMessageBox("Ошибка", "Заполните все поля.");
            return;
        }
        if (txtNew.Text != txtConfirm.Text)
        {
            await DialogHelper.ShowMessageBox("Ошибка", "Новый пароль и подтверждение не совпадают.");
            return;
        }
        Close(true);
    }

    private void btnCancel_Click(object sender, RoutedEventArgs e)
    {
        Close(false);
    }
}