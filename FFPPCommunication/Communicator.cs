using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;

using log4net;


namespace FFPPCommunication
{
    /// <summary>
    /// Summary description for Communicator.
    /// </summary>
    public class Communicator
    {
        #region Private and Protected Data Members
        private static readonly ILog Log = LogManager.GetLogger(typeof(Communicator));

        private int _localPort;
        private IPEndPoint _localEndPoint;
        private UdpClient _udpClient;
        private int ResendAttempts = 0;

        private MessageQueue _queue = new MessageQueue();
        private AutoResetEvent _queueWaitHandle;
        private ReadWrite _readWrite = new ReadWrite();
        
        #endregion

        #region Constructors and Destructors

        /// <summary>
        /// Default Constructor
        /// </summary>
        public Communicator()
        {
            Initialize();
        }

        /// <summary>
        /// Primary Constructor
        /// </summary>
        /// <param name="localPort">If non-zero, the communicator will attempt to use this port</param>
        public Communicator(int localPort)
        {
            _localPort = localPort;
            Initialize();
        }

        public void Initialize()
        {
            Log.Debug("Initializing communicator");

            _localEndPoint = new IPEndPoint(IPAddress.Any, _localPort);
            _udpClient = new UdpClient(_localEndPoint);
            Log.Debug("Creating UdpClient with end point " + _localEndPoint);

            _localEndPoint = _udpClient.Client.LocalEndPoint as IPEndPoint;
            if (_localEndPoint != null)
            {
                _localPort = _localEndPoint.Port;
                Log.Info("Created Communicator's UdpClient, bound to " + _localEndPoint);

               // _queue = new MessageQueue();
                _queueWaitHandle = new AutoResetEvent(false);

                Log.Debug("Done initializing communicator");
            }
        }

        #endregion

        #region Public Properties and Methods

        public bool CommunicationsEnabled => _udpClient != null;

        public Int32 LocalPort
        {
            get { return _localPort; }
            set { _localPort = value; }
        }

        public IPEndPoint LocalEndPoint => _localEndPoint;

        public void Enqueue(Message m)
        {
            if (m != null)
            {
                Log.Debug("Enqueue message = " + m);
                _queue.Enqueue(m);
                _queueWaitHandle.Set();
            }
        }

        public Message MessageAvailable()
        {
                Log.InfoFormat(@"Packet available");
                var ep = new IPEndPoint(IPAddress.Any, 0);
                byte[] receiveBytes = _udpClient?.Receive(ref ep);
                Log.Debug($"Bytes received: {FormatBytesForDisplay(receiveBytes)}");
                _readWrite.DecodeMessage(receiveBytes);
                Message result = _readWrite.targetMessage;
                if (result != null)
                {
                    Log.InfoFormat($"Received type: /n/t'{result.thisMessageType}' " +
                        $"/n/t content: '{result.messageBody}' /n/t from: {result.fromAddress.Address}");
                }
                else
                {
                    Log.Warn(@"Data received, but could not be decoded");
                }
                return result;
        }

        public Message Dequeue()
        {
            Message result = null;

            if (_queue.Count > 0)
                result = _queue.Dequeue();

            if (result != null)
                Log.Debug("Dequeue message = " + result);
            return result;
        }

        //this function is used for the main listening loop. This will only process incoming requests.
        public void Listen()
        {
            Log.Debug("Entering Listening");

            try
            {
                // Wait for some data to become available
                while (CommunicationsEnabled)
                {
                    if (_udpClient?.Available > 0)
                    {
                        Enqueue(MessageAvailable());
                    }
                    Thread.Sleep(10);
                }

            }
            catch (SocketException err)
            {
                if (err.SocketErrorCode != SocketError.TimedOut && err.SocketErrorCode != SocketError.ConnectionReset)
                    Log.ErrorFormat($"Socket error: {err.SocketErrorCode}, {err.Message}");
            }
            catch (Exception err)
            {
                Log.ErrorFormat($"Unexpected expection while receiving datagram: {err} ");
            }
            Log.Debug("Leaving Receive");
        }

        //this function is used to listen for a specific repsonse. If result is null, need to resend request
        public Message Receive(int timeout)
        {
            Log.Debug("Entering Receive");

            Message result = null;
            try
            {
                // Wait for some data to become available
                while (CommunicationsEnabled && _udpClient?.Available <= 0 && timeout > 0)
                {
                    Thread.Sleep(10);
                    timeout -= 10;
                }

                // If there is data receive and communications are enabled, then read that data
                if (CommunicationsEnabled && _udpClient?.Available > 0)
                {
                    result = MessageAvailable();
                }

            }
            catch (SocketException err)
            {
                if (err.SocketErrorCode != SocketError.TimedOut && err.SocketErrorCode != SocketError.ConnectionReset)
                    Log.ErrorFormat($"Socket error: {err.SocketErrorCode}, {err.Message}");
            }
            catch (Exception err)
            {
                Log.ErrorFormat($"Unexpected expection while receiving datagram: {err} ");
            }
            Log.Debug("Leaving Receive");
            return result;
        }

        public bool Resend(Message msg, IPEndPoint targetEndPoint)
        {
            if(ResendAttempts < 3)
            {
                return Send(msg, targetEndPoint);
            }
            else
            {
                Log.Debug($"Message Failed. Message: {msg}, MessageBody: {msg.messageBody}");
                return false;
            }
        }

        public bool Send(Message msg, IPEndPoint targetEndPoint)
        {
            msg.fromAddress = targetEndPoint;
            return Send(msg);
        }

        public bool Send(Message msg)
        {
            Log.Debug("Entering Send");

            bool result = false;

            if (msg.fromAddress != null) 
            {
                try
                {
                    Log.Debug($"Send {msg} to {msg.fromAddress}");
                    byte[] buffer = _readWrite.EncodeMessage( msg );
                    Log.Debug($"Bytes sent: {FormatBytesForDisplay(buffer)}");
                    int count = _udpClient.Send(buffer, buffer.Length, msg.fromAddress); //??
                    result = (count == buffer.Length);
                    Log.Info($"Sent {msg.messageBody} of type '{msg.thisMessageType}' to {msg.fromAddress.Address}, result={result}");
                }
                catch (Exception err)
                {
                    Log.Error("Unexpected exception while sending datagram - ", err);
                }
            }

            Log.Debug("Leaving Send, result = " + result);
            return result;
        }

        public void Close()
        {
            _udpClient?.Close();
        }

        #endregion

        private string FormatBytesForDisplay(byte[] bytes)
        {
            return bytes.Aggregate(string.Empty, (current, b) => current + (b.ToString("X") + " "));
        }
    }
}
