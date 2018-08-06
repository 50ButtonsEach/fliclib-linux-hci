using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Threading.Tasks;
using System.Text;

namespace FliclibDotNetClient
{
    internal abstract class CommandPacket
    {
        protected int Opcode;

        public byte[] Construct()
        {
            MemoryStream stream = new MemoryStream();
            Write(new BinaryWriter(stream));
            byte[] res = new byte[3 + stream.Length];
            res[0] = (byte)(1 + stream.Length);
            res[1] = (byte)((1 + stream.Length) >> 8);
            res[2] = (byte)Opcode;
            Buffer.BlockCopy(stream.ToArray(), 0, res, 3, (int)stream.Length);
            return res;
        }

        protected abstract void Write(BinaryWriter writer);
    }

    internal class CmdGetInfo : CommandPacket
    {
        protected override void Write(BinaryWriter writer)
        {
            Opcode = 0;
        }
    }

    internal class CmdCreateScanner : CommandPacket
    {
        internal uint ScanId;

        protected override void Write(BinaryWriter writer)
        {
            Opcode = 1;
            writer.Write(ScanId);
        }
    }

    internal class CmdRemoveScanner : CommandPacket
    {
        internal uint ScanId;

        protected override void Write(BinaryWriter writer)
        {
            Opcode = 2;
            writer.Write(ScanId);
        }
    }

    internal class CmdCreateConnectionChannel : CommandPacket
    {
        internal uint ConnId;
        internal Bdaddr BdAddr;
        internal LatencyMode LatencyMode;
        internal short AutoDisconnectTime;

        protected override void Write(BinaryWriter writer)
        {
            Opcode = 3;
            writer.Write(ConnId);
            BdAddr.WriteBytes(writer);
            writer.Write((byte)LatencyMode);
            writer.Write(AutoDisconnectTime);
        }
    }

    internal class CmdRemoveConnectionChannel : CommandPacket
    {
        internal uint ConnId;

        protected override void Write(BinaryWriter writer)
        {
            Opcode = 4;
            writer.Write(ConnId);
        }
    }

    internal class CmdForceDisconnect : CommandPacket
    {
        internal Bdaddr BdAddr;

        protected override void Write(BinaryWriter writer)
        {
            Opcode = 5;
            BdAddr.WriteBytes(writer);
        }
    }

    internal class CmdChangeModeParameters : CommandPacket
    {
        internal uint ConnId;
        internal LatencyMode LatencyMode;
        internal short AutoDisconnectTime;

        protected override void Write(BinaryWriter writer)
        {
            Opcode = 6;
            writer.Write(ConnId);
            writer.Write((byte)LatencyMode);
            writer.Write(AutoDisconnectTime);
        }
    }

    internal class CmdPing : CommandPacket
    {
        internal uint PingId;

        protected override void Write(BinaryWriter writer)
        {
            Opcode = 7;
            writer.Write(PingId);
        }
    }

    internal class CmdGetButtonInfo : CommandPacket
    {
        internal Bdaddr BdAddr;

        protected override void Write(BinaryWriter writer)
        {
            Opcode = 8;
            BdAddr.WriteBytes(writer);
        }
    }

    internal class CmdCreateScanWizard : CommandPacket
    {
        internal uint ScanWizardId;

        protected override void Write(BinaryWriter writer)
        {
            Opcode = 9;
            writer.Write(ScanWizardId);
        }
    }

    internal class CmdCancelScanWizard : CommandPacket
    {
        internal uint ScanWizardId;

        protected override void Write(BinaryWriter writer)
        {
            Opcode = 10;
            writer.Write(ScanWizardId);
        }
    }

    internal class CmdDeleteButton : CommandPacket
    {
        internal Bdaddr BdAddr;

        protected override void Write(BinaryWriter writer)
        {
            Opcode = 11;
            BdAddr.WriteBytes(writer);
        }
    }

    internal abstract class EventPacket
    {
        internal const int EVT_ADVERTISEMENT_PACKET_OPCODE = 0;
        internal const int EVT_CREATE_CONNECTION_CHANNEL_RESPONSE_OPCODE = 1;
        internal const int EVT_CONNECTION_STATUS_CHANGED_OPCODE = 2;
        internal const int EVT_CONNECTION_CHANNEL_REMOVED_OPCODE = 3;
        internal const int EVT_BUTTON_UP_OR_DOWN_OPCODE = 4;
        internal const int EVT_BUTTON_CLICK_OR_HOLD_OPCODE = 5;
        internal const int EVT_BUTTON_SINGLE_OR_DOUBLE_CLICK_OPCODE = 6;
        internal const int EVT_BUTTON_SINGLE_OR_DOUBLE_CLICK_OR_HOLD_OPCODE = 7;
        internal const int EVT_NEW_VERIFIED_BUTTON_OPCODE = 8;
        internal const int EVT_GET_INFO_RESPONSE_OPCODE = 9;
        internal const int EVT_NO_SPACE_FOR_NEW_CONNECTION_OPCODE = 10;
        internal const int EVT_GOT_SPACE_FOR_NEW_CONNECTION_OPCODE = 11;
        internal const int EVT_BLUETOOTH_CONTROLLER_STATE_CHANGE_OPCODE = 12;
        internal const int EVT_PING_RESPONSE_OPCODE = 13;
        internal const int EVT_GET_BUTTON_INFO_RESPONSE_OPCODE = 14;
        internal const int EVT_SCAN_WIZARD_FOUND_PRIVATE_BUTTON_OPCODE = 15;
        internal const int EVT_SCAN_WIZARD_FOUND_PUBLIC_BUTTON_OPCODE = 16;
        internal const int EVT_SCAN_WIZARD_BUTTON_CONNECTED_OPCODE = 17;
        internal const int EVT_SCAN_WIZARD_COMPLETED_OPCODE = 18;
        internal const int EVT_BUTTON_DELETED_OPCODE = 19;
        
        internal void Parse(byte[] arr)
        {
            var stream = new MemoryStream(arr);
            stream.ReadByte();
            ParseInternal(new BinaryReader(stream));
        }

        protected abstract void ParseInternal(BinaryReader reader);
    }

    internal class EvtAdvertisementPacket : EventPacket
    {
        internal uint ScanId;
        internal Bdaddr BdAddr;
        internal string Name;
        internal int Rssi;
        internal bool IsPrivate;
        internal bool AlreadyVerified;

        protected override void ParseInternal(BinaryReader reader)
        {
            ScanId = reader.ReadUInt32();
            BdAddr = new Bdaddr(reader);
            int nameLen = reader.ReadByte();
            var bytes = new byte[nameLen];
            for (var i = 0; i < nameLen; i++)
            {
                bytes[i] = reader.ReadByte();
            }
            for (var i = nameLen; i < 16; i++)
            {
                reader.ReadByte();
            }
            Name = Encoding.UTF8.GetString(bytes);
            Rssi = reader.ReadSByte();
            IsPrivate = reader.ReadBoolean();
            AlreadyVerified = reader.ReadBoolean();
        }
    }

    internal class EvtCreateConnectionChannelResponse : EventPacket
    {
        internal uint ConnId;
        internal CreateConnectionChannelError Error;
        internal ConnectionStatus ConnectionStatus;

        protected override void ParseInternal(BinaryReader reader)
        {
            ConnId = reader.ReadUInt32();
            Error = (CreateConnectionChannelError)reader.ReadByte();
            ConnectionStatus = (ConnectionStatus)reader.ReadByte();
        }
    }

    internal class EvtConnectionStatusChanged : EventPacket
    {
        internal uint ConnId;
        internal ConnectionStatus ConnectionStatus;
        internal DisconnectReason DisconnectReason;

        protected override void ParseInternal(BinaryReader reader)
        {
            ConnId = reader.ReadUInt32();
            ConnectionStatus = (ConnectionStatus)reader.ReadByte();
            DisconnectReason = (DisconnectReason)reader.ReadByte();
        }
    }

    internal class EvtConnectionChannelRemoved : EventPacket
    {
        internal uint ConnId;
        internal RemovedReason RemovedReason;

        protected override void ParseInternal(BinaryReader reader)
        {
            ConnId = reader.ReadUInt32();
            RemovedReason = (RemovedReason)reader.ReadByte();
        }
    }

    internal class EvtButtonEvent : EventPacket
    {
        internal uint ConnId;
        internal ClickType ClickType;
        internal bool WasQueued;
        internal uint TimeDiff;

        protected override void ParseInternal(BinaryReader reader)
        {
            ConnId = reader.ReadUInt32();
            ClickType = (ClickType)reader.ReadByte();
            WasQueued = reader.ReadBoolean();
            TimeDiff = reader.ReadUInt32();
        }
    }

    internal class EvtNewVerifiedButton : EventPacket
    {
        internal Bdaddr BdAddr;

        protected override void ParseInternal(BinaryReader reader)
        {
            BdAddr = new Bdaddr(reader);
        }
    }

    internal class EvtGetInfoResponse : EventPacket
    {
        internal BluetoothControllerState BluetoothControllerState;
        internal Bdaddr MyBdAddr;
        internal BdAddrType MyBdAddrType;
        internal byte MaxPendingConnections;
        internal short MaxConcurrentlyConnectedButtons;
        internal byte CurrentPendingConnections;
        internal bool CurrentlyNoSpaceForNewConnection;
        internal Bdaddr[] BdAddrOfVerifiedButtons;

        protected override void ParseInternal(BinaryReader reader)
        {
            BluetoothControllerState = (BluetoothControllerState)reader.ReadByte();
            MyBdAddr = new Bdaddr(reader);
            MyBdAddrType = (BdAddrType)reader.ReadByte();
            MaxPendingConnections = reader.ReadByte();
            MaxConcurrentlyConnectedButtons = reader.ReadInt16();
            CurrentPendingConnections = reader.ReadByte();
            CurrentlyNoSpaceForNewConnection = reader.ReadBoolean();
            var nbVerifiedButtons = reader.ReadUInt16();
            BdAddrOfVerifiedButtons = new Bdaddr[nbVerifiedButtons];
            for (var i = 0; i < nbVerifiedButtons; i++)
            {
                BdAddrOfVerifiedButtons[i] = new Bdaddr(reader);
            }
        }
    }

    internal class EvtNoSpaceForNewConnection : EventPacket
    {
        internal byte MaxConcurrentlyConnectedButtons;

        protected override void ParseInternal(BinaryReader reader)
        {
            MaxConcurrentlyConnectedButtons = reader.ReadByte();
        }
    }

    internal class EvtGotSpaceForNewConnection : EventPacket
    {
        internal byte MaxConcurrentlyConnectedButtons;

        protected override void ParseInternal(BinaryReader reader)
        {
            MaxConcurrentlyConnectedButtons = reader.ReadByte();
        }
    }

    internal class EvtBluetoothControllerStateChange : EventPacket
    {
        internal BluetoothControllerState State;

        protected override void ParseInternal(BinaryReader reader)
        {
            State = (BluetoothControllerState)reader.ReadByte();
        }
    }

    internal class EvtGetButtonInfoResponse : EventPacket
    {
        internal Bdaddr BdAddr;
        internal string Uuid;
        internal string Color;

        protected override void ParseInternal(BinaryReader reader)
        {
            BdAddr = new Bdaddr(reader);
            var uuidBytes = reader.ReadBytes(16);
            if (uuidBytes.Length != 16)
            {
                throw new EndOfStreamException();
            }
            var sb = new StringBuilder(32);
            for (var i = 0; i < 16; i++)
            {
                sb.Append(string.Format("{0:x2}", uuidBytes[i]));
            }
            Uuid = sb.ToString();
            if (Uuid == "00000000000000000000000000000000")
            {
                Uuid = null;
            }

            if (reader.PeekChar() == -1)
            {
                // For old protocol
                return;
            }
            int colorLen = reader.ReadByte();
            var bytes = new byte[colorLen];
            for (var i = 0; i < colorLen; i++)
            {
                bytes[i] = reader.ReadByte();
            }
            for (var i = colorLen; i < 16; i++)
            {
                reader.ReadByte();
            }
            Color = colorLen == 0 ? null : Encoding.UTF8.GetString(bytes);
        }
    }

    internal class EvtScanWizardFoundPrivateButton : EventPacket
    {
        internal uint ScanWizardId;

        protected override void ParseInternal(BinaryReader reader)
        {
            ScanWizardId = reader.ReadUInt32();
        }
    }

    internal class EvtScanWizardFoundPublicButton : EventPacket
    {
        internal uint ScanWizardId;
        internal Bdaddr BdAddr;
        internal string Name;

        protected override void ParseInternal(BinaryReader reader)
        {
            ScanWizardId = reader.ReadUInt32();
            BdAddr = new Bdaddr(reader);
            int nameLen = reader.ReadByte();
            var bytes = new byte[nameLen];
            for (var i = 0; i < nameLen; i++)
            {
                bytes[i] = reader.ReadByte();
            }
            for (var i = nameLen; i < 16; i++)
            {
                reader.ReadByte();
            }
            Name = Encoding.UTF8.GetString(bytes);
        }
    }

    internal class EvtScanWizardButtonConnected : EventPacket
    {
        internal uint ScanWizardId;

        protected override void ParseInternal(BinaryReader reader)
        {
            ScanWizardId = reader.ReadUInt32();
        }
    }

    internal class EvtScanWizardCompleted : EventPacket
    {
        internal uint ScanWizardId;
        internal ScanWizardResult Result;

        protected override void ParseInternal(BinaryReader reader)
        {
            ScanWizardId = reader.ReadUInt32();
            Result = (ScanWizardResult)reader.ReadByte();
        }
    }

    internal class EvtButtonDeleted : EventPacket
    {
        internal Bdaddr BdAddr;
        internal bool DeletedByThisClient;

        protected override void ParseInternal(BinaryReader reader)
        {
            BdAddr = new Bdaddr(reader);
            DeletedByThisClient = reader.ReadBoolean();
        }
    }
}
