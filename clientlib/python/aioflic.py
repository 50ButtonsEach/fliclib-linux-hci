"""Flic client library for python

Requires python 3.3 or higher.

For detailed documentation, see the protocol documentation.

Notes on the data type used in this python implementation compared to the protocol documentation:
All kind of integers are represented as python integers.
Booleans use the Boolean type.
Enums use the defined python enums below.
Bd addr are represented as standard python strings, e.g. "aa:bb:cc:dd:ee:ff".
"""
import asyncio
from enum import Enum
from collections import namedtuple
import time
import struct
import itertools

class CreateConnectionChannelError(Enum):
    NoError = 0
    MaxPendingConnectionsReached = 1

class ConnectionStatus(Enum):
    Disconnected = 0
    Connected = 1
    Ready = 2

class DisconnectReason(Enum):
    Unspecified = 0
    ConnectionEstablishmentFailed = 1
    TimedOut = 2
    BondingKeysMismatch = 3

class RemovedReason(Enum):
    RemovedByThisClient = 0
    ForceDisconnectedByThisClient = 1
    ForceDisconnectedByOtherClient = 2
    
    ButtonIsPrivate = 3
    VerifyTimeout = 4
    InternetBackendError = 5
    InvalidData = 6
    
    CouldntLoadDevice = 7

class ClickType(Enum):
    ButtonDown = 0
    ButtonUp = 1
    ButtonClick = 2
    ButtonSingleClick = 3
    ButtonDoubleClick = 4
    ButtonHold = 5

class BdAddrType(Enum):
    PublicBdAddrType = 0
    RandomBdAddrType = 1

class LatencyMode(Enum):
    NormalLatency = 0
    LowLatency = 1
    HighLatency = 2

class BluetoothControllerState(Enum):
    Detached = 0
    Resetting = 1
    Attached = 2

class ScanWizardResult(Enum):
    WizardSuccess = 0
    WizardCancelledByUser = 1
    WizardFailedTimeout = 2
    WizardButtonIsPrivate = 3
    WizardBluetoothUnavailable = 4
    WizardInternetBackendError = 5
    WizardInvalidData = 6

class ButtonScanner:
    """ButtonScanner class.
    
    Usage:
    scanner = ButtonScanner()
    scanner.on_advertisement_packet = lambda scanner, bd_addr, name, rssi, is_private, already_verified: ...
    client.add_scanner(scanner)
    """
    
    _cnt = itertools.count()
    
    def __init__(self):
        self._scan_id = next(ButtonScanner._cnt)
        self.on_advertisement_packet = lambda scanner, bd_addr, name, rssi, is_private, already_verified: None

class ScanWizard:
    """ScanWizard class
    
    Usage:
    wizard = ScanWizard()
    wizard.on_found_private_button = lambda scan_wizard: ...
    wizard.on_found_public_button = lambda scan_wizard, bd_addr, name: ...
    wizard.on_button_connected = lambda scan_wizard, bd_addr, name: ...
    wizard.on_completed = lambda scan_wizard, result, bd_addr, name: ...
    client.add_scan_wizard(wizard)
    """
    
    _cnt = itertools.count()
    
    def __init__(self):
        self._scan_wizard_id = next(ScanWizard._cnt)
        self._bd_addr = None
        self._name = None
        self.on_found_private_button = lambda scan_wizard: None
        self.on_found_public_button = lambda scan_wizard, bd_addr, name: None
        self.on_button_connected = lambda scan_wizard, bd_addr, name: None
        self.on_completed = lambda scan_wizard, result, bd_addr, name: None

class ButtonConnectionChannel:
    """ButtonConnectionChannel class.
    
    This class represents a connection channel to a Flic button.
    Add this button connection channel to a FlicClient by executing client.add_connection_channel(connection_channel).
    You may only have this connection channel added to one FlicClient at a time.
    
    Before you add the connection channel to the client, you should set up your callback functions by assigning
    the corresponding properties to this object with a function. Each callback function has a channel parameter as the first one,
    referencing this object.
    
    Available properties and the function parameters are:
    on_create_connection_channel_response: channel, error, connection_status
    on_removed: channel, removed_reason
    on_connection_status_changed: channel, connection_status, disconnect_reason
    on_button_up_or_down / on_button_click_or_hold / on_button_single_or_double_click / on_button_single_or_double_click_or_hold: channel, click_type, was_queued, time_diff
    """
    
    _cnt = itertools.count()
    
    def __init__(self, bd_addr, latency_mode = LatencyMode.NormalLatency, auto_disconnect_time = 511):
        self._conn_id = next(ButtonConnectionChannel._cnt)
        self._bd_addr = bd_addr
        self._latency_mode = latency_mode
        self._auto_disconnect_time = auto_disconnect_time
        self._client = None
        
        self.on_create_connection_channel_response = lambda channel, error, connection_status: None
        self.on_removed = lambda channel, removed_reason: None
        self.on_connection_status_changed = lambda channel, connection_status, disconnect_reason: None
        self.on_button_up_or_down = lambda channel, click_type, was_queued, time_diff: None
        self.on_button_click_or_hold = lambda channel, click_type, was_queued, time_diff: None
        self.on_button_single_or_double_click = lambda channel, click_type, was_queued, time_diff: None
        self.on_button_single_or_double_click_or_hold = lambda channel, click_type, was_queued, time_diff: None
    
    @property
    def bd_addr(self):
        return self._bd_addr
    
    @property
    def latency_mode(self):
        return self._latency_mode
    
    @latency_mode.setter
    def latency_mode(self, latency_mode):
        if self._client is None:
            self._latency_mode = latency_mode
            return
        
        self._latency_mode = latency_mode
        if not self._client._closed:
            self._client._send_command("CmdChangeModeParameters", {"conn_id": self._conn_id, "latency_mode": self._latency_mode, "auto_disconnect_time": self._auto_disconnect_time})

    @property
    def auto_disconnect_time(self):
        return self._auto_disconnect_time
    
    @auto_disconnect_time.setter
    def auto_disconnect_time(self, auto_disconnect_time):
        if self._client is None:
            self._auto_disconnect_time = auto_disconnect_time
            return
        
        self._auto_disconnect_time = auto_disconnect_time
        if not self._client._closed:
            self._client._send_command("CmdChangeModeParameters", {"conn_id": self._conn_id, "latency_mode": self._latency_mode, "auto_disconnect_time": self._auto_disconnect_time})

class FlicClient(asyncio.Protocol):
    """FlicClient class.
    
    When this class is constructed, a socket connection is established.
    You may then send commands to the server and set timers.
    Once you are ready with the initialization you must call the handle_events() method which is a main loop that never exits, unless the socket is closed.
    For a more detailed description of all commands, events and enums, check the protocol specification.
    
    All commands are wrapped in more high level functions and events are reported using callback functions.
    
    All methods called on this class will take effect only if you eventually call the handle_events() method.
    
    The ButtonScanner is used to set up a handler for advertisement packets.
    The ButtonConnectionChannel is used to interact with connections to flic buttons and receive their events.
    
    Other events are handled by the following callback functions that can be assigned to this object (and a list of the callback function parameters):
    on_new_verified_button: bd_addr
    on_no_space_for_new_connection: max_concurrently_connected_buttons
    on_got_space_for_new_connection: max_concurrently_connected_buttons
    on_bluetooth_controller_state_change: state
    """
    
    _EVENTS = [
        ("EvtAdvertisementPacket", "<I6s17pb??", "scan_id bd_addr name rssi is_private already_verified"),
        ("EvtCreateConnectionChannelResponse", "<IBB", "conn_id error connection_status"),
        ("EvtConnectionStatusChanged", "<IBB", "conn_id connection_status disconnect_reason"),
        ("EvtConnectionChannelRemoved", "<IB", "conn_id removed_reason"),
        ("EvtButtonUpOrDown", "<IBBI", "conn_id click_type was_queued time_diff"),
        ("EvtButtonClickOrHold", "<IBBI", "conn_id click_type was_queued time_diff"),
        ("EvtButtonSingleOrDoubleClick", "<IBBI", "conn_id click_type was_queued time_diff"),
        ("EvtButtonSingleOrDoubleClickOrHold", "<IBBI", "conn_id click_type was_queued time_diff"),
        ("EvtNewVerifiedButton", "<6s", "bd_addr"),
        ("EvtGetInfoResponse", "<B6sBBhBBH", "bluetooth_controller_state my_bd_addr my_bd_addr_type max_pending_connections max_concurrently_connected_buttons current_pending_connections currently_no_space_for_new_connection nb_verified_buttons"),
        ("EvtNoSpaceForNewConnection", "<B", "max_concurrently_connected_buttons"),
        ("EvtGotSpaceForNewConnection", "<B", "max_concurrently_connected_buttons"),
        ("EvtBluetoothControllerStateChange", "<B", "state"),
        ("EvtPingResponse", "<I", "ping_id"),
        ("EvtGetButtonUUIDResponse", "<6s16s", "bd_addr uuid"),
        ("EvtScanWizardFoundPrivateButton", "<I", "scan_wizard_id"),
        ("EvtScanWizardFoundPublicButton", "<I6s17p", "scan_wizard_id bd_addr name"),
        ("EvtScanWizardButtonConnected", "<I", "scan_wizard_id"),
        ("EvtScanWizardCompleted", "<IB", "scan_wizard_id result")
    ]
    _EVENT_STRUCTS = list(map(lambda x: None if x == None else struct.Struct(x[1]), _EVENTS))
    _EVENT_NAMED_TUPLES = list(map(lambda x: None if x == None else namedtuple(x[0], x[2]), _EVENTS))
    
    _COMMANDS = [
        ("CmdGetInfo", "", ""),
        ("CmdCreateScanner", "<I", "scan_id"),
        ("CmdRemoveScanner", "<I", "scan_id"),
        ("CmdCreateConnectionChannel", "<I6sBh", "conn_id bd_addr latency_mode auto_disconnect_time"),
        ("CmdRemoveConnectionChannel", "<I", "conn_id"),
        ("CmdForceDisconnect", "<6s", "bd_addr"),
        ("CmdChangeModeParameters", "<IBh", "conn_id latency_mode auto_disconnect_time"),
        ("CmdPing", "<I", "ping_id"),
        ("CmdGetButtonUUID", "<6s", "bd_addr"),
        ("CmdCreateScanWizard", "<I", "scan_wizard_id"),
        ("CmdCancelScanWizard", "<I", "scan_wizard_id")
    ]
    
    _COMMAND_STRUCTS = list(map(lambda x: struct.Struct(x[1]), _COMMANDS))
    _COMMAND_NAMED_TUPLES = list(map(lambda x: namedtuple(x[0], x[2]), _COMMANDS))
    _COMMAND_NAME_TO_OPCODE = dict((x[0], i) for i, x in enumerate(_COMMANDS))

    
    def _bdaddr_bytes_to_string(bdaddr_bytes):
        return ":".join(map(lambda x: "%02x" % x, reversed(bdaddr_bytes)))
    
    def _bdaddr_string_to_bytes(bdaddr_string):
        return bytearray.fromhex("".join(reversed(bdaddr_string.split(":"))))
    
    def __init__(self, loop,parent=None):
        self.loop = loop
        self.buffer=b""
        self.transport=None
        self.parent=parent
        self._scanners = {}
        self._scan_wizards = {}
        self._connection_channels = {}
        self._closed = False
        
        self.on_new_verified_button = lambda bd_addr: None
        self.on_no_space_for_new_connection = lambda max_concurrently_connected_buttons: None
        self.on_got_space_for_new_connection = lambda max_concurrently_connected_buttons: None
        self.on_bluetooth_controller_state_change = lambda state: None    
        self.on_get_info = lambda items: None
        self.on_get_button_uuid = lambda addr, uuid: None
         
    def connection_made(self, transport):
        self.transport=transport
        if self.parent:
            self.parent.register_protocol(self)
        
        
    def close(self):
        """Closes the client. The handle_events() method will return."""
        if self._closed:
            return

        self._closed = True
    
    def add_scanner(self, scanner):
        """Add a ButtonScanner object.
        
        The scan will start directly once the scanner is added.
        """
        if scanner._scan_id in self._scanners:
            return
        
        self._scanners[scanner._scan_id] = scanner
        self._send_command("CmdCreateScanner", {"scan_id": scanner._scan_id})
    
    def remove_scanner(self, scanner):
        """Remove a ButtonScanner object.
        
        You will no longer receive advertisement packets.
        """
        if scanner._scan_id not in self._scanners:
            return
        
        del self._scanners[scanner._scan_id]
        self._send_command("CmdRemoveScanner", {"scan_id": scanner._scan_id})
    
    def add_scan_wizard(self, scan_wizard):
        """Add a ScanWizard object.
        
        The scan wizard will start directly once the scan wizard is added.
        """
        if scan_wizard._scan_wizard_id in self._scan_wizards:
            return
        
        self._scan_wizards[scan_wizard._scan_wizard_id] = scan_wizard
        self._send_command("CmdCreateScanWizard", {"scan_wizard_id": scan_wizard._scan_wizard_id})
    
    def cancel_scan_wizard(self, scan_wizard):
        """Cancel a ScanWizard.
        
        Note: The effect of this command will take place at the time the on_completed event arrives on the scan wizard object.
        If cancelled due to this command, "result" in the on_completed event will be "WizardCancelledByUser".
        """
        if scan_wizard._scan_wizard_id not in self._scan_wizards:
            return
        
        self._send_command("CmdCancelScanWizard", {"scan_wizard_id": scan_wizard._scan_wizard_id})
    
    def add_connection_channel(self, channel):
        """Adds a connection channel to a specific Flic button.
        
        This will start listening for a specific Flic button's connection and button events.
        Make sure the Flic is either in public mode (by holding it down for 7 seconds) or already verified before calling this method.
        
        The on_create_connection_channel_response callback property will be called on the
        connection channel after this command has been received by the server.
        
        You may have as many connection channels as you wish for a specific Flic Button.
        """
        if channel._conn_id in self._connection_channels:
            return
        
        channel._client = self
        
        self._connection_channels[channel._conn_id] = channel
        self._send_command("CmdCreateConnectionChannel", {"conn_id": channel._conn_id, "bd_addr": channel.bd_addr, "latency_mode": channel._latency_mode, "auto_disconnect_time": channel._auto_disconnect_time})
    
    def remove_connection_channel(self, channel):
        """Remove a connection channel.
        
        This will stop listening for new events for a specific connection channel that has previously been added.
        Note: The effect of this command will take place at the time the on_removed event arrives on the connection channel object.
        """
        if channel._conn_id not in self._connection_channels:
            return
        
        self._send_command("CmdRemoveConnectionChannel", {"conn_id": channel._conn_id})
    
    def force_disconnect(self, bd_addr):
        """Force disconnection or cancel pending connection of a specific Flic button.
        
        This removes all connection channels for all clients connected to the server for this specific Flic button.
        """
        self._send_command("CmdForceDisconnect", {"bd_addr": bd_addr})
    
    def get_info(self):
        """Get info about the current state of the server.
        
        The server will send back its information directly and the callback will be called once the response arrives.
        The callback takes only one parameter: info. This info parameter is a dictionary with the following objects:
        bluetooth_controller_state, my_bd_addr, my_bd_addr_type, max_pending_connections, max_concurrently_connected_buttons,
        current_pending_connections, currently_no_space_for_new_connection, bd_addr_of_verified_buttons (a list of bd addresses).
        """
        self._send_command("CmdGetInfo", {})
    
    def get_button_uuid(self, bd_addr):
        """Get button uuid for a verified button.
        
        The server will send back its information directly and the callback will be called once the response arrives.
        Responses will arrive in the same order as requested.
        
        The callback takes two parameters: bd_addr, uuid (hex string of 32 characters).
        
        Note: if the button isn't verified, the uuid sent to the callback will rather be None.
        """
        self._send_command("CmdGetButtonUUID", {"bd_addr": bd_addr})
    
    
    def run_on_handle_events_thread(self, callback):
        """Run a function on the thread that handles the events."""
        if threading.get_ident() == self._handle_event_thread_ident:
            callback()
        else:
            self.set_timer(0, callback)
    
    def _send_command(self, name, items):
        
        for key, value in items.items():
            if isinstance(value, Enum):
                items[key] = value.value
        
        if "bd_addr" in items:
            items["bd_addr"] = FlicClient._bdaddr_string_to_bytes(items["bd_addr"])
        
        opcode = FlicClient._COMMAND_NAME_TO_OPCODE[name]
        data_bytes = FlicClient._COMMAND_STRUCTS[opcode].pack(*FlicClient._COMMAND_NAMED_TUPLES[opcode](**items))
        bytes = bytearray(3)
        bytes[0] = (len(data_bytes) + 1) & 0xff
        bytes[1] = (len(data_bytes) + 1) >> 8
        bytes[2] = opcode
        bytes += data_bytes
        self.transport.write(bytes)
    
    def _dispatch_event(self, data):
        if len(data) == 0:
            return
        opcode = data[0]
        
        if opcode >= len(FlicClient._EVENTS) or FlicClient._EVENTS[opcode] == None:
            return
        
        event_name = FlicClient._EVENTS[opcode][0]
        data_tuple = FlicClient._EVENT_STRUCTS[opcode].unpack(data[1 : 1 + FlicClient._EVENT_STRUCTS[opcode].size])
        items = FlicClient._EVENT_NAMED_TUPLES[opcode]._make(data_tuple)._asdict()
        
        # Process some kind of items whose data type is not supported by struct
        if "bd_addr" in items:
            items["bd_addr"] = FlicClient._bdaddr_bytes_to_string(items["bd_addr"])
        
        if "name" in items:
            items["name"] = items["name"].decode("utf-8")
        
        if event_name == "EvtCreateConnectionChannelResponse":
            items["error"] = CreateConnectionChannelError(items["error"])
            items["connection_status"] = ConnectionStatus(items["connection_status"])
        
        if event_name == "EvtConnectionStatusChanged":
            items["connection_status"] = ConnectionStatus(items["connection_status"])
            items["disconnect_reason"] = DisconnectReason(items["disconnect_reason"])
        
        if event_name == "EvtConnectionChannelRemoved":
            items["removed_reason"] = RemovedReason(items["removed_reason"])
        
        if event_name.startswith("EvtButton"):
            items["click_type"] = ClickType(items["click_type"])
        
        if event_name == "EvtGetInfoResponse":
            items["bluetooth_controller_state"] = BluetoothControllerState(items["bluetooth_controller_state"])
            items["my_bd_addr"] = FlicClient._bdaddr_bytes_to_string(items["my_bd_addr"])
            items["my_bd_addr_type"] = BdAddrType(items["my_bd_addr_type"])
            items["bd_addr_of_verified_buttons"] = []
            
            pos = FlicClient._EVENT_STRUCTS[opcode].size
            for i in range(items["nb_verified_buttons"]):
                items["bd_addr_of_verified_buttons"].append(FlicClient._bdaddr_bytes_to_string(data[1 + pos : 1 + pos + 6]))
                pos += 6
        
        if event_name == "EvtBluetoothControllerStateChange":
            items["state"] = BluetoothControllerState(items["state"])
        
        if event_name == "EvtGetButtonUUIDResponse":
            items["uuid"] = "".join(map(lambda x: "%02x" % x, items["uuid"]))
            if items["uuid"] == "00000000000000000000000000000000":
                items["uuid"] = None
        
        if event_name == "EvtScanWizardCompleted":
            items["result"] = ScanWizardResult(items["result"])
        
        # Process event
        if event_name == "EvtAdvertisementPacket":
            scanner = self._scanners.get(items["scan_id"])
            if scanner is not None:
                scanner.on_advertisement_packet(scanner, items["bd_addr"], items["name"], items["rssi"], items["is_private"], items["already_verified"])
        
        if event_name == "EvtCreateConnectionChannelResponse":
            channel = self._connection_channels[items["conn_id"]]
            if items["error"] != CreateConnectionChannelError.NoError:
                del self._connection_channels[items["conn_id"]]
            channel.on_create_connection_channel_response(channel, items["error"], items["connection_status"])
        
        if event_name == "EvtConnectionStatusChanged":
            channel = self._connection_channels[items["conn_id"]]
            channel.on_connection_status_changed(channel, items["connection_status"], items["disconnect_reason"])
        
        if event_name == "EvtConnectionChannelRemoved":
            channel = self._connection_channels[items["conn_id"]]
            del self._connection_channels[items["conn_id"]]
            channel.on_removed(channel, items["removed_reason"])
        
        if event_name == "EvtButtonUpOrDown":
            channel = self._connection_channels[items["conn_id"]]
            channel.on_button_up_or_down(channel, items["click_type"], items["was_queued"], items["time_diff"])
        if event_name == "EvtButtonClickOrHold":
            channel = self._connection_channels[items["conn_id"]]
            channel.on_button_click_or_hold(channel, items["click_type"], items["was_queued"], items["time_diff"])
        if event_name == "EvtButtonSingleOrDoubleClick":
            channel = self._connection_channels[items["conn_id"]]
            channel.on_button_single_or_double_click(channel, items["click_type"], items["was_queued"], items["time_diff"])
        if event_name == "EvtButtonSingleOrDoubleClickOrHold":
            channel = self._connection_channels[items["conn_id"]]
            channel.on_button_single_or_double_click_or_hold(channel, items["click_type"], items["was_queued"], items["time_diff"])
        
        if event_name == "EvtNewVerifiedButton":
            self.on_new_verified_button(items["bd_addr"])
        
        if event_name == "EvtGetInfoResponse":
            self.on_get_info(items)
        
        if event_name == "EvtNoSpaceForNewConnection":
            self.on_no_space_for_new_connection(items["max_concurrently_connected_buttons"])
        
        if event_name == "EvtGotSpaceForNewConnection":
            self.on_got_space_for_new_connection(items["max_concurrently_connected_buttons"])
        
        if event_name == "EvtBluetoothControllerStateChange":
            self.on_bluetooth_controller_state_change(items["state"])
        
        if event_name == "EvtGetButtonUUIDResponse":
            self.on_get_button_uuid(items["bd_addr"], items["uuid"])
        
        if event_name == "EvtScanWizardFoundPrivateButton":
            scan_wizard = self._scan_wizards[items["scan_wizard_id"]]
            scan_wizard.on_found_private_button(scan_wizard)
        
        if event_name == "EvtScanWizardFoundPublicButton":
            scan_wizard = self._scan_wizards[items["scan_wizard_id"]]
            scan_wizard._bd_addr = items["bd_addr"]
            scan_wizard._name = items["name"]
            scan_wizard.on_found_public_button(scan_wizard, scan_wizard._bd_addr, scan_wizard._name)
        
        if event_name == "EvtScanWizardButtonConnected":
            scan_wizard = self._scan_wizards[items["scan_wizard_id"]]
            scan_wizard.on_button_connected(scan_wizard, scan_wizard._bd_addr, scan_wizard._name)
        
        if event_name == "EvtScanWizardCompleted":
            scan_wizard = self._scan_wizards[items["scan_wizard_id"]]
            del self._scan_wizards[items["scan_wizard_id"]]
            scan_wizard.on_completed(scan_wizard, items["result"], scan_wizard._bd_addr, scan_wizard._name)
        
        
    def data_received(self,data):
        cdata=self.buffer+data
        self.buffer=b""
        while len(cdata):
            packet_len = cdata[0] | (cdata[1] << 8)
            packet_len += 2
            if len(cdata)>= packet_len:
                self._dispatch_event(cdata[2:packet_len])
                cdata=cdata[packet_len:]
            else:
                if len(cdata):
                    self.buffer=cdata #unlikely to happen but.....
                break
            

