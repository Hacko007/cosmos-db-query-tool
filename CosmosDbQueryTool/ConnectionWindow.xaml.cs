using System.Windows;
using System.Windows.Controls;
using CosmosDbQueryTool.Models;

namespace CosmosDbQueryTool;

public partial class ConnectionWindow : Window
{
    public ConnectionWindow()
    {
        InitializeComponent();
    }

    public void InitializeFor(MainWindow owner)
    {
        Owner = owner;
        DataContext = owner;
    }

    private MainWindow MainViewModel => (MainWindow)DataContext;

    private void AddConnectionButton_OnClick(object sender, RoutedEventArgs e)
    {
        MainViewModel.AddConnectionTab();
    }

    private void CloseButton_OnClick(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void ConnectionPasswordBox_OnLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is not PasswordBox passwordBox || passwordBox.DataContext is not ConnectionTabViewModel connectionTab)
        {
            return;
        }

        if (passwordBox.Password != connectionTab.Key)
        {
            passwordBox.Password = connectionTab.Key;
        }
    }

    private void ConnectionPasswordBox_OnPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (sender is not PasswordBox passwordBox || passwordBox.DataContext is not ConnectionTabViewModel connectionTab)
        {
            return;
        }

        if (connectionTab.Key != passwordBox.Password)
        {
            connectionTab.Key = passwordBox.Password;
        }
    }
}


