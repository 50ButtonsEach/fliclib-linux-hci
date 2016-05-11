"""Flic client library for python

Requires python 3.3 or higher.

For detailed documentation, see the protocol documentation.

Notes on the data type used in this python implementation compared to the protocol documentation:
All kind of integers are represented as python integers.
Booleans use the Boolean type.
Enums use the defined python enums below.
Bd addr are represented as standard python strings, e.g. "aa:bb:cc:dd:ee:ff".
"""

from enum import Enum
from collections import namedtuple
import time
import socket
import select
import struct
import itertools
import queue
import threading

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
		
		with self._client._lock:
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
		
		with self._client._lock:
			self._auto_disconnect_time = auto_disconnect_time
			if not self._client._closed:
				self._client._send_command("CmdChangeModeParameters", {"conn_id": self._conn_id, "latency_mode": self._latency_mode, "auto_disconnect_time": self._auto_disconnect_time})

class FlicClient:
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
		("EvtPingResponse", "<I", "ping_id")
	]
	_EVENT_STRUCTS = list(map(lambda x: struct.Struct(x[1]), _EVENTS))
	_EVENT_NAMED_TUPLES = list(map(lambda x: namedtuple(x[0], x[2]), _EVENTS))
	
	_COMMANDS = [
		("CmdGetInfo", "", ""),
		("CmdCreateScanner", "<I", "scan_id"),
		("CmdRemoveScanner", "<I", "scan_id"),
		("CmdCreateConnectionChannel", "<I6sBh", "conn_id bd_addr latency_mode auto_disconnect_time"),
		("CmdRemoveConnectionChannel", "<I", "conn_id"),
		("CmdForceDisconnect", "<6s", "bd_addr"),
		("CmdChangeModeParameters", "<IBh", "conn_id latency_mode auto_disconnect_time"),
		("CmdPing", "<I", "ping_id")
	]
	
	_COMMAND_STRUCTS = list(map(lambda x: struct.Struct(x[1]), _COMMANDS))
	_COMMAND_NAMED_TUPLES = list(map(lambda x: namedtuple(x[0], x[2]), _COMMANDS))
	_COMMAND_NAME_TO_OPCODE = dict((x[0], i) for i, x in enumerate(_COMMANDS))
	
	def _bdaddr_bytes_to_string(bdaddr_bytes):
		return ":".join(map(lambda x: "%02x" % x, reversed(bdaddr_bytes)))
	
	def _bdaddr_string_to_bytes(bdaddr_string):
		return bytearray.fromhex("".join(reversed(bdaddr_string.split(":"))))
	
	def __init__(self, host, port = 5551):
		self._sock = socket.create_connection((host, port), None)
		self._lock = threading.RLock()
		self._scanners = {}
		self._connection_channels = {}
		self._get_info_response_queue = queue.Queue()
		self._timers = queue.PriorityQueue()
		self._handle_event_thread_ident = None
		self._closed = False
		
		self.on_new_verified_button = lambda bd_addr: None
		self.on_no_space_for_new_connection = lambda max_concurrently_connected_buttons: None
		self.on_got_space_for_new_connection = lambda max_concurrently_connected_buttons: None
		self.on_bluetooth_controller_state_change = lambda state: None
	
	def close(self):
		"""Closes the client. The handle_events() method will return."""
		with self._lock:
			if self._closed:
				return
			
			if threading.get_ident() != self._handle_event_thread_ident:
				self._send_command("CmdPing", {"ping_id": 0}) # To unblock socket select
			
			self._closed = True
	
	def add_scanner(self, scanner):
		"""Add a ButtonScanner object.
		
		The scan will start directly once the scanner is added.
		"""
		with self._lock:
			if scanner._scan_id in self._scanners:
				return
			
			self._scanners[scanner._scan_id] = scanner
			self._send_command("CmdCreateScanner", {"scan_id": scanner._scan_id})
	
	def remove_scanner(self, scanner):
		"""Remove a ButtonScanner object.
		
		You will no longer receive advertisement packets.
		"""
		with self._lock:
			if scanner._scan_id not in self._scanners:
				return
			
			del self._scanners[scanner._scan_id]
			self._send_command("CmdRemoveScanner", {"scan_id": scanner._scan_id})
	
	def add_connection_channel(self, channel):
		"""Adds a connection channel to a specific Flic button.
		
		This will start listening for a specific Flic button's connection and button events.
		Make sure the Flic is either in public mode (by holding it down for 7 seconds) or already verified before calling this method.
		
		The on_create_connection_channel_response callback property will be called on the
		connection channel after this command has been received by the server.
		
		You may have as many connection channels as you wish for a specific Flic Button.
		"""
		with self._lock:
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
		with self._lock:
			if channel._conn_id not in self._connection_channels:
				return
			
			self._send_command("CmdRemoveConnectionChannel", {"conn_id": channel._conn_id})
	
	def force_disconnect(self, bd_addr):
		"""Force disconnection or cancel pending connection of a specific Flic button.
		
		This removes all connection channels for all clients connected to the server for this specific Flic button.
		"""
		self._send_command("CmdForceDisconnect", {"bd_addr": bd_addr})
	
	def get_info(self, callback):
		"""Get info about the current state of the server.
		
		The server will send back its information directly and the callback will be called once the response arrives.
		The callback takes only one parameter: info. This info parameter is a dictionary with the following objects:
		bluetooth_controller_state, my_bd_addr, my_bd_addr_type, max_pending_connections, max_concurrently_connected_buttons,
		current_pending_connections, currently_no_space_for_new_connection, bd_addr_of_verified_buttons (a list of bd addresses).
		"""
		self._get_info_response_queue.put(callback)
		self._send_command("CmdGetInfo", {})
	
	def set_timer(self, timeout_millis, callback):
		"""Set a timer
		
		This timer callback will run after the specified timeout_millis on the thread that handles the events.
		"""
		point_in_time = time.monotonic() + timeout_millis / 1000.0
		self._timers.put((point_in_time, callback))
		
		if threading.get_ident() != self._handle_event_thread_ident:
			self._send_command("CmdPing", {"ping_id": 0}) # To unblock socket select
	
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
		with self._lock:
			if not self._closed:
				self._sock.sendall(bytes)
	
	def _dispatch_event(self, data):
		if len(data) == 0:
			return
		opcode = data[0]
		if opcode >= len(FlicClient._EVENTS):
			return
		
		event_name = FlicClient._EVENTS[opcode][0]
		data_tuple = FlicClient._EVENT_STRUCTS[opcode].unpack(data[1 : 1 + FlicClient._EVENT_STRUCTS[opcode].size])
		items = FlicClient._EVENT_NAMED_TUPLES[opcode]._make(data_tuple)._asdict()
		
		# Process some kind of items whose data type is not supported by struct
		if "bd_addr" in items:
			items["bd_addr"] = FlicClient._bdaddr_bytes_to_string(items["bd_addr"])
		
		if event_name == "EvtAdvertisementPacket":
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
			self._get_info_response_queue.get()(items)
		
		if event_name == "EvtNoSpaceForNewConnection":
			self.on_no_space_for_new_connection(items["max_concurrently_connected_buttons"])
		
		if event_name == "EvtGotSpaceForNewConnection":
			self.on_got_space_for_new_connection(items["max_concurrently_connected_buttons"])
		
		if event_name == "EvtBluetoothControllerStateChange":
			self.on_bluetooth_controller_state_change(items["state"])
	
	def _handle_one_event(self):
		if len(self._timers.queue) > 0:
			current_timer = self._timers.queue[0]
			timeout = max(current_timer[0] - time.monotonic(), 0)
			if timeout == 0:
				self._timers.get()[1]()
				return True
			if len(select.select([self._sock], [], [], timeout)[0]) == 0:
				return True
		
		len_arr = bytearray(2)
		view = memoryview(len_arr)
		
		toread = 2
		while toread > 0:
			nbytes = self._sock.recv_into(view, toread)
			if nbytes == 0:
				return False
			view = view[nbytes:]
			toread -= nbytes
		
		packet_len = len_arr[0] | (len_arr[1] << 8)
		data = bytearray(packet_len)
		view = memoryview(data)
		toread = packet_len
		while toread > 0:
			nbytes = self._sock.recv_into(view, toread)
			if nbytes == 0:
				return False
			view = view[nbytes:]
			toread -= nbytes
		
		self._dispatch_event(data)
		return True
		
	def handle_events(self):
		"""Start the main loop for this client.
		
		This method will not return until the socket has been closed.
		Once it has returned, any use of this FlicClient is illegal.
		"""
		self._handle_event_thread_ident = threading.get_ident()
		while not self._closed:
			if not self._handle_one_event():
				break
		self._sock.close()
