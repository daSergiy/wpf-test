using Avalonia.Controls;
using Avalonia.Interactivity;
using System;

namespace AvaloniaApp
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        public void OnBtnClick(object sender, RoutedEventArgs e)
        {
            textBlock.Text += "Hello world! " + DateTime.UtcNow + Environment.NewLine;
        }
    }
}