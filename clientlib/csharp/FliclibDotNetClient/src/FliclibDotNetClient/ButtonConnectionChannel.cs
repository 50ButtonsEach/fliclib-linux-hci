using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace FliclibDotNetClient
{
    /// <summary>
    /// CreateConnectionChannelResponseEventArgs
    /// </summary>
    public class CreateConnectionChannelResponseEventArgs : EventArgs
    {
        /// <summary>
        /// Whether the request succeeded or not, and if not, what error.
        /// </summary>
        public CreateConnectionChannelError Error { get; internal set; }

        /// <summary>
        /// The current connection status to this button.
        /// This might be a non-disconnected status if there are already other active connection channels to this button.
        /// </summary>
        public ConnectionStatus ConnectionStatus { get; internal set; }
    }

    /// <summary>
    /// ConnectionChannelRemovedEventArgs
    /// </summary>
    public class ConnectionChannelRemovedEventArgs : EventArgs
    {
        /// <summary>
        /// Reason for this connection channel being removed.
        /// </summary>
        public RemovedReason RemovedReason { get; internal set; }
    }

    /// <summary>
    /// ConnectionStatusChangedEventArgs
    /// </summary>
    public class ConnectionStatusChangedEventArgs : EventArgs
    {
        /// <summary>
        /// New connection status
        /// </summary>
        public ConnectionStatus ConnectionStatus { get; internal set; }

        /// <summary>
        /// If the connection status is Disconnected, this contains the reason. Otherwise this parameter is considered invalid.
        /// </summary>
        public DisconnectReason DisconnectReason { get; internal set; }
    }

    /// <summary>
    /// ButtonEventEventArgs
    /// </summary>
    public class ButtonEventEventArgs : EventArgs
    {
        /// <summary>
        /// The possible click type values depend on the event type
        /// </summary>
        public ClickType ClickType { get; internal set; }

        /// <summary>
        /// If this button event happened during the button was disconnected or not
        /// </summary>
        public bool WasQueued { get; internal set; }

        /// <summary>
        /// If this button event happened during the button was disconnected,
        /// this will be the number of seconds since that event happened (otherwise it will most likely be 0).
        /// Depending on your application, you might want to discard too old events.
        /// </summary>
        public uint TimeDiff { get; internal set; }
    }

    /// <summary>
    /// A button connection channel.
    /// 
    /// Register the events and add an instance of this class to a FlicClient with AddConnectionChannel.
    /// </summary>
    public class ButtonConnectionChannel
    {
        private static int _nextId = 0;
        internal uint ConnId = (uint)Interlocked.Increment(ref _nextId);
        
        private LatencyMode _latencyMode;
        private short _autoDisconnectTime;
        internal FlicClient FlicClient;

        /// <summary>
        /// Full Constructor with all options
        /// </summary>
        /// <param name="bdAddr">Bluetooth device address</param>
        /// <param name="latencyMode">Latency mode</param>
        /// <param name="autoDisconnectTime">Auto disconnect time</param>
        public ButtonConnectionChannel(Bdaddr bdAddr, LatencyMode latencyMode, short autoDisconnectTime)
        {
            if (bdAddr == null)
            {
                throw new ArgumentNullException(nameof(bdAddr));
            }

            BdAddr = bdAddr;
            _latencyMode = latencyMode;
            _autoDisconnectTime = autoDisconnectTime;
        }

        /// <summary>
        /// Constructor that uses default values of latency mode (normal latency) and auto disconnect time (disable auto disconnect mechanism)
        /// </summary>
        /// <param name="bdAddr">Bluetooth device address</param>
        public ButtonConnectionChannel(Bdaddr bdAddr) : this(bdAddr, LatencyMode.NormalLatency, 0x1ff)
        {
            
        }

        /// <summary>
        /// Gets the Bluetooth device address that is assigned to this connection channel
        /// </summary>
        public Bdaddr BdAddr { get; private set; }

        /// <summary>
        /// Gets or sets the latency mode for this connection channel
        /// </summary>
        public LatencyMode LatencyMode
        {
            get
            {
                return _latencyMode;
            }
            set
            {
                if (_latencyMode != value)
                {
                    _latencyMode = value;
                    UpdateMode();
                }
            }
        }

        /// <summary>
        /// Gets or sets the auto disconnect time for this connection channel. The new value will be applied the next time the button connects.
        /// </summary>
        public short AutoDisconnectTime
        {
            get
            {
                return _autoDisconnectTime;
            }
            set
            {
                if (_autoDisconnectTime != value)
                {
                    _autoDisconnectTime = value;
                    UpdateMode();
                }
            }
        }

        private void UpdateMode()
        {
            if (FlicClient != null)
            {
                FlicClient.SendPacket(new CmdChangeModeParameters { ConnId = ConnId, AutoDisconnectTime = _autoDisconnectTime, LatencyMode = _latencyMode });
            }
        }

        /// <summary>
        /// Event raised when the server has received the request to add this connection channel
        /// </summary>
        public event EventHandler<CreateConnectionChannelResponseEventArgs> CreateConnectionChannelResponse;

        /// <summary>
        /// Event raised when the connection channel has been removed at the server
        /// </summary>
        public event EventHandler<ConnectionChannelRemovedEventArgs> Removed;

        /// <summary>
        /// Event raised when the connection status has changed
        /// </summary>
        public event EventHandler<ConnectionStatusChangedEventArgs> ConnectionStatusChanged;

        /// <summary>
        /// Used to simply know when the button was pressed or released.
        /// </summary>
        public event EventHandler<ButtonEventEventArgs> ButtonUpOrDown;

        /// <summary>
        /// Used if you want to distinguish between click and hold.
        /// </summary>
        public event EventHandler<ButtonEventEventArgs> ButtonClickOrHold;

        /// <summary>
        /// Used if you want to distinguish between a single click and a double click.
        /// </summary>
        public event EventHandler<ButtonEventEventArgs> ButtonSingleOrDoubleClick;

        /// <summary>
        /// Used if you want to distinguish between a single click, a double click and a hold.
        /// </summary>
        public event EventHandler<ButtonEventEventArgs> ButtonSingleOrDoubleClickOrHold;

        protected internal virtual void OnCreateConnectionChannelResponse(CreateConnectionChannelResponseEventArgs e)
        {
            CreateConnectionChannelResponse.RaiseEvent(this, e);
        }

        protected internal virtual void OnRemoved(ConnectionChannelRemovedEventArgs e)
        {
            Removed.RaiseEvent(this, e);
        }

        protected internal virtual void OnConnectionStatusChanged(ConnectionStatusChangedEventArgs e)
        {
            ConnectionStatusChanged.RaiseEvent(this, e);
        }

        protected internal virtual void OnButtonUpOrDown(ButtonEventEventArgs e)
        {
            ButtonUpOrDown.RaiseEvent(this, e);
        }

        protected internal virtual void OnButtonClickOrHold(ButtonEventEventArgs e)
        {
            ButtonClickOrHold.RaiseEvent(this, e);
        }

        protected internal virtual void OnButtonSingleOrDoubleClick(ButtonEventEventArgs e)
        {
            ButtonSingleOrDoubleClick.RaiseEvent(this, e);
        }

        protected internal virtual void OnButtonSingleOrDoubleClickOrHold(ButtonEventEventArgs e)
        {
            ButtonSingleOrDoubleClickOrHold.RaiseEvent(this, e);
        }
    }
}
