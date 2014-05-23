using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace TcpClientBuddy
{
  /// <summary>
  /// Interaction logic for MainWindow.xaml
  /// </summary>
  public partial class MainWindow : Window
  {
    private MainWindowViewModel viewModel;

    public MainWindow()
    {
      InitializeComponent();
    }

    private void WindowLoaded(object sender, RoutedEventArgs e)
    {
      try
      {
        viewModel = new MainWindowViewModel();
        viewModel.ServerAddress = Properties.Settings.Default.ServerAddress;
        viewModel.ServerPort = Properties.Settings.Default.ServerPort;
        viewModel.SendData = Properties.Settings.Default.SendData;
        this.DataContext = viewModel;
      }
      catch(Exception ex)
      {
        MessageBox.Show(ex.ToString(), "Exception", MessageBoxButton.OK, MessageBoxImage.Error);
        Close();
      }
    }

    private void WindowClosing(object sender, System.ComponentModel.CancelEventArgs e)
    {
      try
      {
        Properties.Settings.Default.ServerAddress = viewModel.ServerAddress;
        Properties.Settings.Default.ServerPort = viewModel.ServerPort;
        Properties.Settings.Default.SendData = viewModel.SendData;
        Properties.Settings.Default.Save();
      }
      catch (Exception ex)
      {
        MessageBox.Show(ex.ToString(), "Exception", MessageBoxButton.OK, MessageBoxImage.Error);
      }
    }

    private void StartClick(object sender, RoutedEventArgs e)
    {
      try
      {
        viewModel.Start();
      }
      catch (Exception ex)
      {
        MessageBox.Show(ex.ToString(), "Exception", MessageBoxButton.OK, MessageBoxImage.Error);
      }
    }

    private void StopClick(object sender, RoutedEventArgs e)
    {
      try
      {
        viewModel.Stop();
      }
      catch (Exception ex)
      {
        MessageBox.Show(ex.ToString(), "Exception", MessageBoxButton.OK, MessageBoxImage.Error);
      }
    }
    
    private void SendClick(object sender, RoutedEventArgs e)
    {
      try
      {
        viewModel.Send();
      }
      catch (Exception ex)
      {
        MessageBox.Show(ex.ToString(), "Exception", MessageBoxButton.OK, MessageBoxImage.Error);
      }
    }

  }
}
