using System;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Utility.Comm.Tcp
{
  public class TcpClient : IComm
  {
    public event Action Connected;
    public event Action Disconnected;
    public event Action<byte[]> Received;

    private readonly object sync = new object();

    private Socket socket;
    private bool isStarted;
    private IPEndPoint _endPoint;

    private bool stop;

    private const int ReceiveBufferSize = 1024;
    private readonly byte[] receiveBuffer = new byte[ReceiveBufferSize];

    public string ServerAddress { get; private set; }

    public int ServerPort { get; private set; }

    public TcpClient(string serverAddress, int serverPort)
    {
      ServerAddress = serverAddress;
      ServerPort = serverPort;
    }

    public void Start()
    {
      if (IsStarted)
        return;

      lock (sync) stop = false;

      IsStarted = true;

      Dns.BeginGetHostAddresses(ServerAddress, GetHostAddressesCallback, null);
    }

    public virtual void Stop()
    {
      if (!IsStarted)
        return;

      lock (sync)
      {
        stop = true;
        _endPoint = null;
        if (socket != null)
        {
          if (socket.Connected)
          {
            RaiseDisconnected();
          }
          socket.Close();
          socket = null;
        }
      }

      IsStarted = false;
    }

    public bool IsStarted
    {
      get { return isStarted; }
      protected set
      {
        if (isStarted.Equals(value))
        {
          return;
        }
        isStarted = value;

        var logger = NLog.LogManager.GetCurrentClassLogger();
        if (isStarted)
          logger.Info("{0} Started : {1}:{2}", GetType().Name, ServerAddress, ServerPort);
        else
          logger.Info("{0} Stopped", GetType().Name);
      }
    }

    public bool IsConnected
    {
      get { lock(sync) return socket != null && socket.Connected; }
    }

    public void Send(byte[] message)
    {
      try
      {
        lock (sync)
        {
          if (stop || socket == null || !socket.Connected)
          {
            return;
          }
          socket.Send(message);
        }
      }
      catch (SocketException ex)
      {
        if (ex.ErrorCode == 10054)
        {
          // 10054 == An existing connection was forcibly closed by the remote host
          // Expected when a remote client disconnects.
          ServerDisconnected();
          return;
        }

        NLog.LogManager.GetCurrentClassLogger()
            .ErrorException(string.Format("Send : SocketException : {0} : {1}", ex.ErrorCode, ex.Message), ex);
      }
      catch (Exception ex)
      {
        NLog.LogManager.GetCurrentClassLogger()
            .ErrorException(string.Format("Send : {0} : {1}", ex.GetType(), ex.Message), ex);
      }
    }

    private void GetHostAddressesCallback(IAsyncResult ar)
    {
      try
      {
        lock (sync) if (stop) return;

        var ipAddresses = Dns.EndGetHostAddresses(ar);
        var serverIpAddress = ipAddresses.FirstOrDefault(ipAddress => ipAddress.AddressFamily == AddressFamily.InterNetwork);

        if(serverIpAddress == null)
          throw new Exception(string.Format("Could not resolve server address '{0}'", ServerAddress));

        _endPoint = new IPEndPoint(serverIpAddress, ServerPort);

        lock (sync)
        {
          socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
          socket.BeginConnect(_endPoint, ConnectCallback, null);
        }

      }
      catch (Exception ex)
      {
        NLog.LogManager.GetCurrentClassLogger().WarnException(string.Format("GetHostAddressesCallback : {0} : {1}", ex.GetType(), ex.Message), ex);
        Thread.Sleep(2000);

        lock (sync) if (stop) return;
        Dns.BeginGetHostAddresses(ServerAddress, GetHostAddressesCallback, null);
      }
    }


    private void ConnectCallback(IAsyncResult ar)
    {
      try
      {
        lock (sync)
        {
          if (stop) return;

          socket.EndConnect(ar);

          RaiseConnected();

          SocketError receiveError;
          socket.BeginReceive(receiveBuffer, 0, ReceiveBufferSize, SocketFlags.None, out receiveError, ReceiveCallback, null);
        }
      }
      catch (SocketException ex)
      {
        NLog.LogManager.GetCurrentClassLogger().WarnException(string.Format("ConnectCallback : SocketException : {0} : {1}", ex.ErrorCode, ex.Message),
                             ex);
        Thread.Sleep(2000);
        lock (sync)
        {
          if (stop) return;
          socket.BeginConnect(_endPoint, ConnectCallback, null);
        }
      }
      catch (Exception ex)
      {
        NLog.LogManager.GetCurrentClassLogger().WarnException(string.Format("ConnectCallback : {0} : {1}", ex.GetType(), ex.Message), ex);
        Thread.Sleep(2000);
        lock (sync)
        {
          if (stop) return;
          socket.BeginConnect(_endPoint, ConnectCallback, ar.AsyncState);
        }
      }
    }


    private void ReceiveCallback(IAsyncResult ar)
    {
      var logger = NLog.LogManager.GetCurrentClassLogger();
      try
      {
        int received;
        lock (sync)
        {
          if (stop) return;

          received = socket.EndReceive(ar);
          if (received == 0)
          {
            if (stop) return;
            ServerDisconnected();
          }
        }

        var buffer = new byte[received];
        Buffer.BlockCopy(receiveBuffer, 0, buffer, 0, received);

        RaiseReceived(buffer);

        lock (sync)
        {
          if (stop) return;
          SocketError receiveError;
          socket.BeginReceive(receiveBuffer, 0, ReceiveBufferSize, SocketFlags.None, out receiveError, ReceiveCallback, null);
        }
      }
      catch (SocketException ex)
      {

        if (ex.ErrorCode == 10054 || ex.ErrorCode == 10060)
        {
          // 10054 == An existing connection was forcibly closed by the remote host
          // 10060 == An established connection failed because connected host has failed to respond
          // Expected when a remote client disconnects.
          ServerDisconnected();
          return;
        }

        logger.ErrorException(string.Format("ReceiveCallback : SocketException : {0} : {1}", ex.ErrorCode, ex.Message),
                              ex);

        lock (sync) if (stop) return;

      }
      catch (Exception ex)
      {
        lock (sync) if (stop) return;
        logger.ErrorException(string.Format("ReceiveCallback : {0} : {1}", ex.GetType(), ex.Message), ex);
      }
    }

    private void ServerDisconnected()
    {
      lock (sync)
      {
        if (stop) return;
        if (socket.Connected)
        {
          socket.Disconnect(true);
        }
        RaiseDisconnected();
        socket.BeginConnect(_endPoint, ConnectCallback, null);
      }
    }

    private void RaiseConnected()
    {
      //NLog.LogManager.GetCurrentClassLogger().Info("{0} Connected : {1}:{2}", GetType().Name, ServerAddress, ServerPort);
     
      if (Connected != null)
      {
        Connected();
      }
    }

    private void RaiseDisconnected()
    {
      //NLog.LogManager.GetCurrentClassLogger().Info("{0} Disconnected : {1}:{2}", GetType().Name, ServerAddress, ServerPort);

      if (Disconnected != null)
      {
        Disconnected();
      }
    }

    private void RaiseReceived(byte[] data)
    {
      //NLog.LogManager.GetCurrentClassLogger().Info("{0} Received : {1}:{2}", GetType().Name, ServerAddress, ServerPort);

      if (Received != null)
      {
        Received(data);
      }
      
    }
  }
}