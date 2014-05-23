using System;
using System.Net.Sockets;

namespace Utility.Comm.Tcp
{
  public class ConnectedSocket
  {
    public delegate void ConnectedSocketClosedDelegate(ConnectedSocket src);

    public delegate void ConnectedSocketReceivedDelegate(ConnectedSocket src, byte[] buffer);

    public event ConnectedSocketClosedDelegate ConnectedSocketClosed;
    public event ConnectedSocketReceivedDelegate ConnectedSocketReceived;

    private const int ReceiveBufferSize = 1024;
    private readonly byte[] receiveBuffer = new byte[ReceiveBufferSize];
    private SocketError receiveError;

    private readonly object sync = new object();
    private bool stop;

    public ConnectedSocket(Socket socket)
    {
      Id = Guid.NewGuid();
      Socket = socket;
    }

    public void Start()
    {
      lock (sync) stop = false;
      Socket.BeginReceive(receiveBuffer, 0, ReceiveBufferSize, SocketFlags.None, out receiveError, ReceiveCallback, Socket);
    }

    public void Stop()
    {
      lock (sync) stop = true;
      Socket.Dispose();
    }

    public Guid Id { get; private set; }
    public Socket Socket { get; private set; }

    private void ReceiveCallback(IAsyncResult ar)
    {
      var logger = NLog.LogManager.GetCurrentClassLogger();
      try
      {
        lock (sync) if (stop) return;

        int received = Socket.EndReceive(ar);
        if (received == 0)
        {
          if (ConnectedSocketClosed != null) ConnectedSocketClosed(this);
          return;
        }

        lock (sync) if (stop) return;


        var buffer = new byte[received];
        Buffer.BlockCopy(receiveBuffer, 0, buffer, 0, received);
        if (ConnectedSocketReceived != null) ConnectedSocketReceived(this, buffer);

        Socket.BeginReceive(receiveBuffer, 0, ReceiveBufferSize, SocketFlags.None, out receiveError, ReceiveCallback, Socket);
      }
      catch (SocketException ex)
      {
        lock (sync) if (stop) return;

        if (ex.ErrorCode == 10054)
        {
          // An existing connection was forcibly closed by the remote host
          // Expected when a remote client disconnects.
          return;
        }

        logger.ErrorException(string.Format("ReceiveCallback : SocketException : {0} : {1}", ex.ErrorCode, ex.Message), ex);
      }
      catch (Exception ex)
      {
        lock (sync) if (stop) return;
        logger.ErrorException(string.Format("ReceiveCallback : {0} : {1}", ex.GetType(), ex.Message), ex);
      }
    }

  }
}