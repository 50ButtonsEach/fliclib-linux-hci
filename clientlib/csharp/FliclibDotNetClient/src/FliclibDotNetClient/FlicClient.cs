using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace FliclibDotNetClient
{
    /// <summary>
    /// Callback for GetInfo
    /// </summary>
    /// <param name="bluetoothControllerState">Bluetooth controller state</param>
    /// <param name="myBdAddr">The Bluetooth controller's device address</param>
    /// <param name="myBdAddrType">The type of the Bluetooth controller's device address</param>
    /// <param name="maxPendingConnections">The maximum number of pending connections (0 if unknown)</param>
    /// <param name="maxConcurrentlyConnectedButtons">The maximum number of concurrently connected buttons (-1 if unknown)</param>
    /// <param name="currentPendingConnections">Number of buttons that have at least one active connection channel</param>
    /// <param name="currentlyNoSpaceForNewConnection">Maximum number of connections is currently reached (only sent by Linux server implementation)</param>
    /// <param name="verifiedButtons">An array of verified buttons</param>
    public delegate void GetInfoResponseCallback(BluetoothControllerState bluetoothControllerState, Bdaddr myBdAddr,
                                           BdAddrType myBdAddrType, byte maxPendingConnections,
                                           short maxConcurrentlyConnectedButtons, byte currentPendingConnections,
                                           bool currentlyNoSpaceForNewConnection,
                                           Bdaddr[] verifiedButtons);

    /// <summary>
    /// Callback for GetButtonInfo
    /// </summary>
    /// <param name="bdAddr">The Bluetooth device address for the request</param>
    /// <param name="uuid">The UUID of the button. Will be null if the button was not verified before.</param>
    /// <param name="color">The color of the button. Will be null if unknown or the button was not verified before.</param>
    /// <param name="serialNumber">The serial number of the button. Will be null if the button was not verified before.</param>
    /// <param name="flicVersion">The Flic version (1 or 2). Will be 0 if the button was not verified before.</param>
    /// <param name="firmwareVersion">The firmware version of the button. Will be 0 if the button was not verified before.</param>
    public delegate void GetButtonInfoResponseCallback(Bdaddr bdAddr, string uuid, string color, string serialNumber, int flicVersion, uint firmwareVersion);

    /// <summary>
    /// NewVerifiedButtonEventArgs
    /// </summary>
    public class NewVerifiedButtonEventArgs : EventArgs
    {
        /// <summary>
        /// Bluetooth device address for new verified button
        /// </summary>
        public Bdaddr BdAddr { get; internal set; }
    }

    /// <summary>
    /// SpaceForNewConnectionEventArgs
    /// </summary>
    public class SpaceForNewConnectionEventArgs : EventArgs
    {
        /// <summary>
        /// The number of max concurrently connected buttons
        /// </summary>
        public byte MaxConcurrentlyConnectedButtons { get; internal set; }
    }

    /// <summary>
    /// BluetoothControllerStateChangeEventArgs
    /// </summary>
    public class BluetoothControllerStateChangeEventArgs : EventArgs
    {
        /// <summary>
        /// The new state of the Bluetooth controller
        /// </summary>
        public BluetoothControllerState State { get; internal set; }
    }

    /// <summary>
    /// ButtonDeletedEventArgs
    /// </summary>
    public class ButtonDeletedEventArgs : EventArgs
    {
        /// <summary>
        /// Bluetooth device address of removed button
        /// </summary>
        public Bdaddr BdAddr { get; internal set; }

        /// <summary>
        /// Whether or not the button was deleted by this client
        /// </summary>
        public bool DeletedByThisClient { get; internal set; }
    }

    /// <summary>
    /// Flic client class
    /// 
    /// For overview of the protocol and more detailed documentation, see the protocol documentation.
    /// 
    /// Create and connect a client to a server with Create or CreateAsync.
    /// Then call HandleEvents to start the event loop.
    /// </summary>
    public sealed class FlicClient : IDisposable
    {
        private TcpClient _tcpClient;
        private Socket _socket;

        private int _handleEventsThreadId;

        private readonly List<Socket> _readList = new List<Socket>(); 
        private readonly byte[] _lengthReadBuf = new byte[2];

        private int _hasPendingWrite;
        private readonly SocketAsyncEventArgs _socketWriteEventArgs;
        private readonly ConcurrentQueue<byte[]> _outgoingPackets = new ConcurrentQueue<byte[]>(); 

        private readonly ConcurrentDictionary<uint, ButtonScanner> _scanners = new ConcurrentDictionary<uint, ButtonScanner>();
        private readonly ConcurrentDictionary<uint, ButtonConnectionChannel> _connectionChannels = new ConcurrentDictionary<uint, ButtonConnectionChannel>();
        private readonly ConcurrentDictionary<uint, ScanWizard> _scanWizards = new ConcurrentDictionary<uint, ScanWizard>();
        private readonly ConcurrentQueue<GetInfoResponseCallback> _getInfoResponseCallbackQueue = new ConcurrentQueue<GetInfoResponseCallback>();
        private readonly Queue<GetButtonInfoResponseCallback> _getButtonInfoResponseCallbackQueue = new Queue<GetButtonInfoResponseCallback>();
        private readonly SortedDictionary<long, Action> _timers = new SortedDictionary<long, Action>(); 

        /// <summary>
        /// Raised when a new button is verified at the server (initiated by any client)
        /// </summary>
        public event EventHandler<NewVerifiedButtonEventArgs> NewVerifiedButton;
        
        /// <summary>
        /// Raised when the Bluetooth controller status changed, for example when it is plugged or unplugged or for any other reason becomes available / unavailable.
        /// During the controller is Detached, no scan events or button events will be received.
        /// </summary>
        public event EventHandler<BluetoothControllerStateChangeEventArgs> BluetoothControllerStateChange;

        /// <summary>
        /// This event will be raised when the maximum number of concurrent connections has been reached (only sent by the Linux server implementation).
        /// </summary>
        public event EventHandler<SpaceForNewConnectionEventArgs> NoSpaceForNewConnection;

        /// <summary>
        /// This event will be raised when the number of concurrent connections has decreased from the maximum by one (only sent by the Linux server implementation).
        /// </summary>
        public event EventHandler<SpaceForNewConnectionEventArgs> GotSpaceForNewConnection;

        /// <summary>
        /// Raised when a button is deleted, or when this client tries to delete a non-existing button.
        /// </summary>
        public event EventHandler<ButtonDeletedEventArgs> ButtonDeleted;

        private FlicClient()
        {
            _socketWriteEventArgs = new SocketAsyncEventArgs();
            _socketWriteEventArgs.Completed += SocketWriteEventArgsOnCompleted;
        }

        /// <summary>
        /// Connects to a server with default port 5551
        /// </summary>
        /// <param name="host">Hostname or IP address</param>
        /// <returns>A connected FlicClient</returns>
        /// <exception cref="System.Net.Sockets.SocketException">If a connection couldn't be established</exception>
        public static FlicClient Create(string host)
        {
            return Create(host, 5551);
        }

        /// <summary>
        /// Connects to a server
        /// </summary>
        /// <param name="host">Hostname or IP address</param>
        /// <param name="port">Port</param>
        /// <returns>A connected FlicClient</returns>
        /// <exception cref="System.Net.Sockets.SocketException">If a connection couldn't be established</exception>
        public static FlicClient Create(string host, int port)
        {
            var tcpClient = new TcpClient();
            tcpClient.NoDelay = true;
            tcpClient.Connect(host, port);

            var client = new FlicClient();
            client._tcpClient = tcpClient;
            client._socket = tcpClient.Client;
            return client;
        }

        /// <summary>
        /// Connects to a server
        /// </summary>
        /// <param name="host">Hostname or IP address</param>
        /// <returns>A connected FlicClient</returns>
        /// <exception cref="System.Net.Sockets.SocketException">If a connection couldn't be established</exception>
        public static Task<FlicClient> CreateAsync(string host)
        {
            return CreateAsync(host, 5551);
        }

        /// <summary>
        /// Connects to a server
        /// </summary>
        /// <param name="host">Hostname or IP address</param>
        /// <param name="port">Port</param>
        /// <returns>A connected FlicClient</returns>
        /// <exception cref="System.Net.Sockets.SocketException">If a connection couldn't be established</exception>
        public static async Task<FlicClient> CreateAsync(string host, int port)
        {
            var tcpClient = new TcpClient();
            tcpClient.NoDelay = true;
            await tcpClient.ConnectAsync(host, port);

            var client = new FlicClient();
            client._tcpClient = tcpClient;
            client._socket = tcpClient.Client;
            return client;
        }

        /// <summary>
        /// Initiates a disconnection of the FlicClient. The HandleEvents method will return once the disconnection is complete.
        /// </summary>
        public void Disconnect()
        {
            try
            {
                _socket.Shutdown(SocketShutdown.Both);
            }
            catch (ObjectDisposedException)
            {
                // In case the Socket has already been closed
            }
            catch (SocketException)
            {
                // Some problem happened on the socket so just close it
                Dispose();
            }
        }

        /// <summary>
        /// Disposes the client.
        /// The socket will be closed. If you for some reason want to close the socket before you call HandleEvents, execute this.
        /// Otherwise you should rather call Disconnect to make a more graceful disconnection.
        /// </summary>
        public void Dispose()
        {
            try
            {
                _tcpClient.Close();
            }
            catch (Exception)
            {
                // ignored
            }
        }

        /// <summary>
        /// Requests info from the server.
        /// </summary>
        /// <param name="callback">Callback to be invoked when the response arrives</param>
        public void GetInfo(GetInfoResponseCallback callback)
        {
            if (callback == null)
            {
                throw new ArgumentNullException(nameof(callback));
            }
            _getInfoResponseCallbackQueue.Enqueue(callback);
            SendPacket(new CmdGetInfo());
        }

        /// <summary>
        /// Requests info for a button.
        /// A null UUID will be sent to the callback if the button was not verified before.
        /// </summary>
        /// <param name="bdAddr">Bluetooth device address</param>
        /// <param name="callback">Callback to be invoked when the response arrives</param>
        public void GetButtonInfo(Bdaddr bdAddr, GetButtonInfoResponseCallback callback)
        {
            if (callback == null)
            {
                throw new ArgumentNullException(nameof(callback));
            }

            lock (_getButtonInfoResponseCallbackQueue)
            {
                _getButtonInfoResponseCallbackQueue.Enqueue(callback);
                SendPacket(new CmdGetButtonInfo { BdAddr = bdAddr });
            }
        }

        /// <summary>
        /// Adds a raw scanner.
        /// The AdvertisementPacket event will be raised on the scanner for each advertisement packet received.
        /// The scanner must not already be added.
        /// </summary>
        /// <param name="buttonScanner">A ButtonScanner</param>
        public void AddScanner(ButtonScanner buttonScanner)
        {
            if (buttonScanner == null)
            {
                throw new ArgumentNullException(nameof(buttonScanner));
            }
            if (!_scanners.TryAdd(buttonScanner.ScanId, buttonScanner))
            {
                throw new ArgumentException("Button scanner already added", nameof(buttonScanner));
            }

            SendPacket(new CmdCreateScanner { ScanId = buttonScanner.ScanId });
        }

        /// <summary>
        /// Removes a raw scanner.
        /// No further AdvertisementPacket events will be raised.
        /// The scanner must be currently added.
        /// </summary>
        /// <param name="buttonScanner">A ButtonScanner that was previously added</param>
        public void RemoveScanner(ButtonScanner buttonScanner)
        {
            if (buttonScanner == null)
            {
                throw new ArgumentNullException(nameof(buttonScanner));
            }
            ButtonScanner buttonScannerPrev;
            if (!_scanners.TryRemove(buttonScanner.ScanId, out buttonScannerPrev))
            {
                throw new ArgumentException("Button scanner was not added", nameof(buttonScanner));
            }

            SendPacket(new CmdRemoveScanner { ScanId = buttonScanner.ScanId });
        }

        /// <summary>
        /// Adds and starts a ScanWizard.
        /// Events on the scan wizard will be raised as it makes progress. Eventually Completed will be raised.
        /// The scan wizard must not currently be running.
        /// </summary>
        /// <param name="scanWizard">A ScanWizard</param>
        public void AddScanWizard(ScanWizard scanWizard)
        {
            if (scanWizard == null)
            {
                throw new ArgumentNullException(nameof(scanWizard));
            }

            if (!_scanWizards.TryAdd(scanWizard.ScanWizardId, scanWizard))
            {
                throw new ArgumentException("Scan wizard already added");
            }

            SendPacket(new CmdCreateScanWizard { ScanWizardId = scanWizard.ScanWizardId });
        }

        /// <summary>
        /// Cancels a ScanWizard.
        /// The Completed event will be raised with status WizardCancelledByUser, if it already wasn't completed before the server received this command.
        /// </summary>
        /// <param name="scanWizard">A ScanWizard</param>
        public void CancelScanWizard(ScanWizard scanWizard)
        {
            if (scanWizard == null)
            {
                throw new ArgumentNullException(nameof(scanWizard));
            }

            SendPacket(new CmdCancelScanWizard { ScanWizardId = scanWizard.ScanWizardId });
        }

        /// <summary>
        /// Adds a connection channel.
        /// The CreateConnectionChannelResponse event will be raised with the response.
        /// If the response was success, button events will be raised when the button is pressed.
        /// </summary>
        /// <param name="channel">A ButtonConnectionChannel</param>
        public void AddConnectionChannel(ButtonConnectionChannel channel)
        {
            if (channel == null)
            {
                throw new ArgumentNullException(nameof(channel));
            }
            if (!_connectionChannels.TryAdd(channel.ConnId, channel))
            {
                throw new ArgumentException("Connection channel already added");
            }

            channel.FlicClient = this;

            SendPacket(new CmdCreateConnectionChannel { ConnId = channel.ConnId, BdAddr = channel.BdAddr, LatencyMode = channel.LatencyMode, AutoDisconnectTime = channel.AutoDisconnectTime });
        }

        /// <summary>
        /// Removes a connection channel.
        /// Button events will no longer be received after the server has received this command.
        /// </summary>
        /// <param name="channel">A ButtonConnectionChannel</param>
        public void RemoveConnectionChannel(ButtonConnectionChannel channel)
        {
            if (channel == null)
            {
                throw new ArgumentNullException(nameof(channel));
            }

            SendPacket(new CmdRemoveConnectionChannel { ConnId = channel.ConnId });
        }

        /// <summary>
        /// Forces disconnect of a button.
        /// All connection channels among all clients the server has for this button will be removed.
        /// </summary>
        /// <param name="bdAddr">Bluetooth device address</param>
        public void ForceDisconnect(Bdaddr bdAddr)
        {
            if (bdAddr == null)
            {
                throw new ArgumentNullException(nameof(bdAddr));
            }

            SendPacket(new CmdForceDisconnect { BdAddr = bdAddr });
        }

        internal void SendPacket(CommandPacket packet)
        {
            if (!_socket.Connected)
            {
                return;
            }

            var buf = packet.Construct();

            if (Interlocked.CompareExchange(ref _hasPendingWrite, 1, 0) == 0)
            {
                // Don't execute SendAsync on non-threadpool threads since if such a thread exits before the operation completes, it is cancelled
                if (Thread.CurrentThread.IsThreadPoolThread)
                {
                    SendBufferHelper(buf);
                }
                else
                {
                    ThreadPool.QueueUserWorkItem(arg => SendBufferHelper(buf));
                }
            }
            else
            {
                _outgoingPackets.Enqueue(buf);
            }
        }

        private void SendBufferHelper(byte[] buf)
        {
            _socketWriteEventArgs.SetBuffer(buf, 0, buf.Length);
            bool completedSynchronously;
            try
            {
                completedSynchronously = !_socket.SendAsync(_socketWriteEventArgs);
            }
            catch (ObjectDisposedException)
            {
                return;
            }
            catch (SocketException)
            {
                Disconnect();
                return;
            }
            if (completedSynchronously)
            {
                SocketWriteEventArgsOnCompleted(null, _socketWriteEventArgs);
            }
        }
        
        private void SocketWriteEventArgsOnCompleted(object sender, SocketAsyncEventArgs socketAsyncEventArgs)
        {
            if (socketAsyncEventArgs.SocketError == SocketError.Success)
            {
                byte[] nextBuffer;
                if (_outgoingPackets.TryDequeue(out nextBuffer))
                {
                    SendBufferHelper(nextBuffer);
                    return;
                }

                _hasPendingWrite = 0;
            }
            else
            {
                Disconnect();
            }
        }

        /// <summary>
        /// Schedules an action to be executed on the on the same thread that handles events.
        /// Since only one event or callback is run concurrently, this might be useful to avoid race conditions in your code.
        /// </summary>
        /// <param name="timeoutMilliSeconds">Number of milliseconds to wait before the action should be run</param>
        /// <param name="action">An action</param>
        public void SetTimer(int timeoutMilliSeconds, Action action)
        {
            var pointInTime = Stopwatch.GetTimestamp() + (timeoutMilliSeconds * Stopwatch.Frequency / 1000);
            lock (_timers)
            {
                while (_timers.ContainsKey(pointInTime))
                {
                    pointInTime++;
                }
                _timers.Add(pointInTime, action);
            }

            if (Thread.CurrentThread.ManagedThreadId != _handleEventsThreadId)
            {
                // The only way to wake up the thread that waits for the socket to receive data is to make the server send something to us
                SendPacket(new CmdPing { PingId = 0 });
            }
        }

        /// <summary>
        /// Runs an action on the same thread that handles events.
        /// Since only one event or callback is run concurrently, this might be useful to avoid race conditions in your code.
        /// If the current thread already is the handle events thread, it's run immediately. Otherwise it is scheduled to run immediately or after the current event handler finishes.
        /// </summary>
        /// <param name="action">An action</param>
        public void RunOnHandleEventsThread(Action action)
        {
            if (Thread.CurrentThread.ManagedThreadId == _handleEventsThreadId)
            {
                action();
            }
            else
            {
                SetTimer(0, action);
            }
        }

        /// <summary>
        /// Starts the event loop.
        /// This must be called in order to receive events and callbacks.
        /// The method will not return until the socket has been disconnected.
        /// Once the socket disconnects (intentionally or unintentionally), this method will close the socket and return.
        /// No more events or callbacks will be raised or called after this method has returned.
        /// </summary>
        public void HandleEvents()
        {
            _handleEventsThreadId = Thread.CurrentThread.ManagedThreadId;
            try
            {
                while (_socket.Connected)
                {
                    KeyValuePair<long, Action> firstTimer;
                    lock (_timers)
                    {
                        firstTimer = _timers.FirstOrDefault();
                    }
                    long timeout = 0;
                    if (firstTimer.Key != 0)
                    {
                        timeout = 1000 * (firstTimer.Key - Stopwatch.GetTimestamp()) / Stopwatch.Frequency;
                        if (timeout <= 0)
                        {
                            lock (_timers)
                            {
                                _timers.Remove(firstTimer.Key);
                            }
                            firstTimer.Value();
                            continue;
                        }
                    }

                    if (_readList.Count == 0)
                    {
                        _readList.Add(_socket);
                    }

                    byte[] pkt;

                    try
                    {
                        Socket.Select(_readList, null, null, timeout == 0 ? -1 : Math.Max((int)timeout, 1000000) * 1000);
                        if (_readList.Count == 0)
                        {
                            continue;
                        }

                        if (!_socket.Connected)
                        {
                            break;
                        }

                        var received = _socket.Receive(_lengthReadBuf);
                        if (received == 0)
                        {
                            break;
                        }
                        if (received == 1)
                        {
                            received = _socket.Receive(_lengthReadBuf, 1, 1, SocketFlags.None);
                            if (received == 0)
                            {
                                break;
                            }
                        }
                        var len = _lengthReadBuf[0] | (_lengthReadBuf[1] << 8);

                        if (len == 0)
                        {
                            continue;
                        }

                        pkt = new byte[len];

                        var pos = 0;
                        while (pos < len)
                        {
                            var nbytes = _socket.Receive(pkt, pos, len - pos, SocketFlags.None);
                            if (nbytes == 0)
                            {
                                break;
                            }
                            pos += nbytes;
                        }
                    }
                    catch (ObjectDisposedException)
                    {
                        break;
                    }
                    catch (SocketException)
                    {
                        break;
                    }
                    DispatchPacket(pkt);
                }
            }
            finally
            {
                Dispose();
            }
        }

        private void DispatchPacket(byte[] packet)
        {
            int opcode = packet[0];
            switch (opcode)
            {
                case EventPacket.EVT_ADVERTISEMENT_PACKET_OPCODE:
                    {
                        var pkt = new EvtAdvertisementPacket();
                        pkt.Parse(packet);
                        ButtonScanner scanner;
                        if (_scanners.TryGetValue(pkt.ScanId, out scanner))
                        {
                            scanner.OnAdvertisementPacket(new AdvertisementPacketEventArgs { BdAddr = pkt.BdAddr, Name = pkt.Name, Rssi = pkt.Rssi, IsPrivate = pkt.IsPrivate, AlreadyVerified = pkt.AlreadyVerified, AlreadyConnectedToThisDevice = pkt.AlreadyConnectedToThisDevice, AlreadyConnectedToOtherDevice = pkt.AlreadyConnectedToOtherDevice });
                        }
                    }
                    break;
                case EventPacket.EVT_CREATE_CONNECTION_CHANNEL_RESPONSE_OPCODE:
                    {
                        var pkt = new EvtCreateConnectionChannelResponse();
                        pkt.Parse(packet);
                        var channel =_connectionChannels[pkt.ConnId];
                        if (pkt.Error != CreateConnectionChannelError.NoError)
                        {
                            _connectionChannels.TryRemove(channel.ConnId, out channel);
                        }
                        channel.OnCreateConnectionChannelResponse(new CreateConnectionChannelResponseEventArgs { Error = pkt.Error, ConnectionStatus = pkt.ConnectionStatus });
                    }
                    break;
                case EventPacket.EVT_CONNECTION_STATUS_CHANGED_OPCODE:
                    {
                        var pkt = new EvtConnectionStatusChanged();
                        pkt.Parse(packet);
                        var channel = _connectionChannels[pkt.ConnId];
                        channel.OnConnectionStatusChanged(new ConnectionStatusChangedEventArgs { ConnectionStatus = pkt.ConnectionStatus, DisconnectReason = pkt.DisconnectReason });
                    }
                    break;
                case EventPacket.EVT_CONNECTION_CHANNEL_REMOVED_OPCODE:
                    {
                        var pkt = new EvtConnectionChannelRemoved();
                        pkt.Parse(packet);
                        ButtonConnectionChannel channel;
                        _connectionChannels.TryRemove(pkt.ConnId, out channel);
                        channel.OnRemoved(new ConnectionChannelRemovedEventArgs { RemovedReason = pkt.RemovedReason });
                    }
                    break;
                case EventPacket.EVT_BUTTON_UP_OR_DOWN_OPCODE:
                case EventPacket.EVT_BUTTON_CLICK_OR_HOLD_OPCODE:
                case EventPacket.EVT_BUTTON_SINGLE_OR_DOUBLE_CLICK_OPCODE:
                case EventPacket.EVT_BUTTON_SINGLE_OR_DOUBLE_CLICK_OR_HOLD_OPCODE:
                    {
                        var pkt = new EvtButtonEvent();
                        pkt.Parse(packet);
                        var channel = _connectionChannels[pkt.ConnId];
                        var eventArgs = new ButtonEventEventArgs { ClickType = pkt.ClickType, WasQueued = pkt.WasQueued, TimeDiff = pkt.TimeDiff };
                        switch (opcode)
                        {
                            case EventPacket.EVT_BUTTON_UP_OR_DOWN_OPCODE:
                                channel.OnButtonUpOrDown(eventArgs);
                                break;
                            case EventPacket.EVT_BUTTON_CLICK_OR_HOLD_OPCODE:
                                channel.OnButtonClickOrHold(eventArgs);
                                break;
                            case EventPacket.EVT_BUTTON_SINGLE_OR_DOUBLE_CLICK_OPCODE:
                                channel.OnButtonSingleOrDoubleClick(eventArgs);
                                break;
                            case EventPacket.EVT_BUTTON_SINGLE_OR_DOUBLE_CLICK_OR_HOLD_OPCODE:
                                channel.OnButtonSingleOrDoubleClickOrHold(eventArgs);
                                break;
                        }
                    }
                    break;
                case EventPacket.EVT_NEW_VERIFIED_BUTTON_OPCODE:
                    {
                        var pkt = new EvtNewVerifiedButton();
                        pkt.Parse(packet);
                        NewVerifiedButton.RaiseEvent(this, new NewVerifiedButtonEventArgs { BdAddr = pkt.BdAddr });
                    }
                    break;
                case EventPacket.EVT_GET_INFO_RESPONSE_OPCODE:
                    {
                        var pkt = new EvtGetInfoResponse();
                        pkt.Parse(packet);
                        GetInfoResponseCallback callback;
                        _getInfoResponseCallbackQueue.TryDequeue(out callback);
                        callback(pkt.BluetoothControllerState, pkt.MyBdAddr, pkt.MyBdAddrType, pkt.MaxPendingConnections, pkt.MaxConcurrentlyConnectedButtons, pkt.CurrentPendingConnections, pkt.CurrentlyNoSpaceForNewConnection, pkt.BdAddrOfVerifiedButtons);
                    }
                    break;
                case EventPacket.EVT_NO_SPACE_FOR_NEW_CONNECTION_OPCODE:
                    {
                        var pkt = new EvtNoSpaceForNewConnection();
                        pkt.Parse(packet);
                        NoSpaceForNewConnection.RaiseEvent(this, new SpaceForNewConnectionEventArgs { MaxConcurrentlyConnectedButtons = pkt.MaxConcurrentlyConnectedButtons });
                    }
                    break;
                case EventPacket.EVT_GOT_SPACE_FOR_NEW_CONNECTION_OPCODE:
                    {
                        var pkt = new EvtGotSpaceForNewConnection();
                        pkt.Parse(packet);
                        GotSpaceForNewConnection.RaiseEvent(this, new SpaceForNewConnectionEventArgs { MaxConcurrentlyConnectedButtons = pkt.MaxConcurrentlyConnectedButtons });
                    }
                    break;
                case EventPacket.EVT_BLUETOOTH_CONTROLLER_STATE_CHANGE_OPCODE:
                    {
                        var pkt = new EvtBluetoothControllerStateChange();
                        pkt.Parse(packet);
                        BluetoothControllerStateChange.RaiseEvent(this, new BluetoothControllerStateChangeEventArgs { State = pkt.State });
                    }
                    break;
                case EventPacket.EVT_GET_BUTTON_INFO_RESPONSE_OPCODE:
                    {
                        var pkt = new EvtGetButtonInfoResponse();
                        pkt.Parse(packet);
                        GetButtonInfoResponseCallback callback;
                        lock (_getButtonInfoResponseCallbackQueue)
                        {
                            callback = _getButtonInfoResponseCallbackQueue.Dequeue();
                        }
                        callback(pkt.BdAddr, pkt.Uuid, pkt.Color, pkt.SerialNumber, pkt.FlicVersion, pkt.FirmwareVersion);
                    }
                    break;
                case EventPacket.EVT_SCAN_WIZARD_FOUND_PRIVATE_BUTTON_OPCODE:
                    {
                        var pkt = new EvtScanWizardFoundPrivateButton();
                        pkt.Parse(packet);
                        _scanWizards[pkt.ScanWizardId].OnFoundPrivateButton();
                    }
                    break;
                case EventPacket.EVT_SCAN_WIZARD_FOUND_PUBLIC_BUTTON_OPCODE:
                    {
                        var pkt = new EvtScanWizardFoundPublicButton();
                        pkt.Parse(packet);
                        var wizard = _scanWizards[pkt.ScanWizardId];
                        wizard.BdAddr = pkt.BdAddr;
                        wizard.Name = pkt.Name;
                        wizard.OnFoundPublicButton(new ScanWizardButtonInfoEventArgs { BdAddr = wizard.BdAddr, Name = wizard.Name });
                    }
                    break;
                case EventPacket.EVT_SCAN_WIZARD_BUTTON_CONNECTED_OPCODE:
                    {
                        var pkt = new EvtScanWizardButtonConnected();
                        pkt.Parse(packet);
                        var wizard = _scanWizards[pkt.ScanWizardId];
                        wizard.OnButtonConnected(new ScanWizardButtonInfoEventArgs { BdAddr = wizard.BdAddr, Name = wizard.Name });
                    }
                    break;
                case EventPacket.EVT_SCAN_WIZARD_COMPLETED_OPCODE:
                    {
                        var pkt = new EvtScanWizardCompleted();
                        pkt.Parse(packet);
                        ScanWizard wizard;
                        _scanWizards.TryRemove(pkt.ScanWizardId, out wizard);
                        var eventArgs = new ScanWizardCompletedEventArgs { BdAddr = wizard.BdAddr, Name = wizard.Name, Result = pkt.Result };
                        wizard.BdAddr = null;
                        wizard.Name = null;
                        wizard.OnCompleted(eventArgs);
                    }
                    break;
                case EventPacket.EVT_BUTTON_DELETED_OPCODE:
                    {
                        var pkt = new EvtButtonDeleted();
                        pkt.Parse(packet);
                        ButtonDeleted.RaiseEvent(this, new ButtonDeletedEventArgs { BdAddr = pkt.BdAddr, DeletedByThisClient = pkt.DeletedByThisClient });
                    }
                    break;
            }
        }
    }
}
