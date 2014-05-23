using System;
using System.ComponentModel;
using System.Linq;
using System.Text;

namespace TcpClientBuddy
{
  public class MainWindowViewModel : INotifyPropertyChanged
  {
    private CommUtil.Tcp.TcpClient client;

    public event PropertyChangedEventHandler PropertyChanged;

    public const string ServerAddressProp = "ServerAddress";
    public const string ServerPortProp = "ServerPort";
    public const string SendDataProp = "SendData";

    public const string StartEnabledProp = "StartEnabled";
    public const string StopEnabledProp = "StopEnabled";
    public const string SendEnabledProp = "SendEnabled";

    public string ServerAddress
    {
      get { return serverAddress; }
      set
      {
        if(value.Equals(serverAddress))
          return;
        serverAddress = value;
        RaisePropertyChanged(ServerAddressProp);
        RaiseEnabledPropertiesChanged();
      }
    }
    private string serverAddress;

    public int ServerPort
    {
      get { return serverPort; }
      set
      {
        if (value.Equals(serverPort))
          return;
        serverPort = value;
        RaisePropertyChanged(ServerPortProp);
        RaiseEnabledPropertiesChanged();
      }
    }
    private int serverPort;


    public string SendData
    {
      get { return sendData; }
      set
      {
        if (value.Equals(sendData))
          return;
        sendData = value;
        RaisePropertyChanged(SendDataProp);
        RaiseEnabledPropertiesChanged();
      }
    }
    private string sendData;


    public bool StartEnabled
    {
      get
      {
        return !string.IsNullOrWhiteSpace(serverAddress) && serverPort > 0 && client == null;
      }
    }

    public bool StopEnabled
    {
      get { return client != null; }
    }

    public bool SendEnabled
    {
      get { return client != null && !string.IsNullOrWhiteSpace(SendData); }
    }

    public void Start()
    {
      if(client != null) return;
      
      client = new CommUtil.Tcp.TcpClient(serverAddress, serverPort);
      client.Start();

      RaiseEnabledPropertiesChanged();
    }


    public void Stop()
    {
      if(client == null) return;

      client.Stop();
      client = null;

      RaiseEnabledPropertiesChanged();
    }

    public void Send()
    {
      if(client == null) return;

      client.Send(UTF8Encoding.UTF8.GetBytes(SendData + "\r\n"));
    }

    protected void RaiseEnabledPropertiesChanged()
    {
      RaisePropertyChanged(StartEnabledProp);
      RaisePropertyChanged(StopEnabledProp);
      RaisePropertyChanged(SendEnabledProp);
    }

    protected void RaisePropertyChanged(string propertyName)
    {
      if (PropertyChanged != null)
      {
        PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
      }
    }
  }
}
