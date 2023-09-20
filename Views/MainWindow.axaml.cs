using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using TestingEO.ViewModels;

namespace TestingEO.Views
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            this.Closing += MainWindow_Closing;
        }

        private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            if (DataContext is MainWindowViewModel vm) vm.Closing(sender, e);
        }

    }
}
