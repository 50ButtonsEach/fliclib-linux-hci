using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace FliclibDotNetClient
{
    /// <summary>
    /// AdvertisementPacketEventArgs
    /// </summary>
    public class AdvertisementPacketEventArgs : EventArgs
    {
        /// <summary>
        /// Bluetooth device address
        /// </summary>
        public Bdaddr BdAddr { get; internal set; }

        /// <summary>
        /// The advertised name
        /// </summary>
        public string Name { get; internal set; }

        /// <summary>
        /// RSSI value (signal strength)
        /// </summary>
        public int Rssi { get; internal set; }

        /// <summary>
        /// Whether the button is currently in private mode (does not accept connections from unknown clients) or not
        /// </summary>
        public bool IsPrivate { get; internal set; }

        /// <summary>
        /// This button is already verified at the server
        /// </summary>
        public bool AlreadyVerified { get; internal set; }

        /// <summary>
        /// This button is already connected to this device
        /// </summary>
        public bool AlreadyConnectedToThisDevice { get; internal set; }

        /// <summary>
        /// This button is already connected to another device
        /// </summary>
        public bool AlreadyConnectedToOtherDevice { get; internal set; }
    }

    /// <summary>
    /// A raw scanner class.
    /// Add an instance of this class to a FlicClient with AddScanner and register the AdvertisementPacket event.
    /// </summary>
    public class ButtonScanner
    {
        private static int _nextId = 0;
        internal uint ScanId = (uint)Interlocked.Increment(ref _nextId);

        /// <summary>
        /// This event will be raised for every advertisement packet received
        /// </summary>
        public event EventHandler<AdvertisementPacketEventArgs> AdvertisementPacket;

        protected internal virtual void OnAdvertisementPacket(AdvertisementPacketEventArgs e)
        {
            AdvertisementPacket.RaiseEvent(this, e);
        }
    }
}
