using System;

namespace Utility.Comm
{
  public interface IComm
  {
    event Action Connected;
    event Action Disconnected;
    event Action<byte[]> Received;

    void Start();
    void Stop();
    bool IsStarted { get; }
    bool IsConnected { get; }

    void Send(byte[] data);
  }
}