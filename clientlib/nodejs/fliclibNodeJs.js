/**
 * Flic lib for Node.js.
 *
 * See the official protocol specification for more details.
 */

var util = require('util');
var net = require('net');
var EventEmitter = require('events').EventEmitter;

var FlicCommandOpcodes = {
	GetInfo: 0,
	CreateScanner: 1,
	RemoveScanner: 2,
	CreateConnectionChannel: 3,
	RemoveConnectionChannel: 4,
	ForceDisconnect: 5,
	ChangeModeParameters: 6,
	Ping: 7,
	GetButtonInfo: 8,
	CreateScanWizard: 9,
	CancelScanWizard: 10,
	DeleteButton: 11,
	CreateBatteryStatusListener: 12,
	RemoveBatteryStatusListener: 13
};

var FlicEventOpcodes = {
	AdvertisementPacket: 0,
	CreateConnectionChannelResponse: 1,
	ConnectionStatusChanged: 2,
	ConnectionChannelRemoved: 3,
	ButtonUpOrDown: 4,
	ButtonClickOrHold: 5,
	ButtonSingleOrDoubleClick: 6,
	ButtonSingleOrDoubleClickOrHold: 7,
	NewVerifiedButton: 8,
	GetInfoResponse: 9,
	NoSpaceForNewConnection: 10,
	GotSpaceForNewConnection: 11,
	BluetoothControllerStateChange: 12,
	PingResponse: 13,
	GetButtonInfoResponse: 14,
	ScanWizardFoundPrivateButton: 15,
	ScanWizardFoundPublicButton: 16,
	ScanWizardButtonConnected: 17,
	ScanWizardCompleted: 18,
	ButtonDeleted: 19,
	BatteryStatus: 20
};

function createBuffer(arr, offset, len) {
	arr = new Uint8Array(arr, offset, len);
	return Buffer.allocUnsafe ? Buffer.from(arr) : new Buffer(arr);
}

/**
 * FlicRawClient
 *
 * This is a low level client that is used by the high level FlicClient below.
 *
 */
var FlicRawClient = function(inetAddress, port) {
	var enumValues = {
		CreateConnectionChannelError: {
			NoError: 0,
			MaxPendingConnectionsReached: 1
		},
		ConnectionStatus: {
			Disconnected: 0,
			Connected: 1,
			Ready: 2
		},

		DisconnectReason: {
			Unspecified: 0,
			ConnectionEstablishmentFailed: 1,
			TimedOut: 2,
			BondingKeysMismatch: 3
		},

		RemovedReason: {
			RemovedByThisClient: 0,
			ForceDisconnectedByThisClient: 1,
			ForceDisconnectedByOtherClient: 2,
			
			ButtonIsPrivate: 3,
			VerifyTimeout: 4,
			InternetBackendError: 5,
			InvalidData: 6,
			
			CouldntLoadDevice: 7,
			
			DeletedByThisClient: 8,
			DeletedByOtherClient: 9,
			ButtonBelongsToOtherPartner: 10,
			DeletedFromButton: 11
		},

		ClickType: {
			ButtonDown: 0,
			ButtonUp: 1,
			ButtonClick: 2,
			ButtonSingleClick: 3,
			ButtonDoubleClick: 4,
			ButtonHold: 5
		},

		BdAddrType: {
			PublicBdAddrType: 0,
			RandomBdAddrType: 1
		},

		LatencyMode: {
			NormalLatency: 0,
			LowLatency: 1,
			HighLatency: 2
		},

		ScanWizardResult: {
			WizardSuccess: 0,
			WizardCancelledByUser: 1,
			WizardFailedTimeout: 2,
			WizardButtonIsPrivate: 3,
			WizardBluetoothUnavailable: 4,
			WizardInternetBackendError: 5,
			WizardInvalidData: 6,
			WizardButtonBelongsToOtherPartner: 7,
			WizardButtonAlreadyConnectedToOtherDevice: 8
		},

		BluetoothControllerState: {
			Detached: 0,
			Resetting: 1,
			Attached: 2
		}
	};
	
	var socket = net.connect({host: inetAddress, port: port});
	socket.once("connect", onOpen);
	socket.on("close", onClose);
	socket.on("error", onError);
	socket.on("data", onData);
	
	var currentPacketData = null;
	
	var me = this;
	
	function onOpen(event) {
		me.onOpen(event);
	}
	
	function onClose(had_error) {
		me.onClose(had_error);
	}
	
	function onError(error) {
		me.onError(error);
	}
	
	function onData(data) {
		currentPacketData = currentPacketData == null ? data : Buffer.concat([currentPacketData, data], currentPacketData.length + data.length);
		while (currentPacketData.length >= 2) {
			var len = currentPacketData[0] | (currentPacketData[1] << 8);
			if (currentPacketData.length >= 2 + len) {
				var packet = currentPacketData.slice(2, 2 + len);
				currentPacketData = currentPacketData.slice(2 + len);
				if (packet.length > 0) {
					onMessage(packet);
				}
			} else {
				break;
			}
		}
	}
	
	function onMessage(pkt) {
		var pos = 0;
		function readUInt8() {
			return pkt[pos++];
		}
		function readInt8() {
			return (readUInt8() << 24) >> 24;
		}
		function readUInt16() {
			return pkt[pos++] | (pkt[pos++] << 8);
		}
		function readInt16() {
			return (readUInt16() << 16) >> 16;
		}
		function readInt32() {
			return readUInt16() | (readUInt16() << 16);
		}
		function readUInt32() {
			return readInt32() >>> 0;
		}
		function readUInt64() { // Can not really handle 64 bits since Javascript only supports 64-bit floating point values
			return readUInt32() + readUInt32() * 0x100000000;
		}
		function readBdAddr() {
			var str = "";
			for (var i = 5; i >= 0; i--) {
				str += (0x100 + pkt[pos + i]).toString(16).substr(-2);
				if (i != 0) {
					str += ":";
				}
			}
			pos += 6;
			return str;
		}
		function readString() {
			var len = readUInt8();
			var s = pkt.slice(pos, pos + len).toString();
			pos += 16;
			return s;
		}
		function readBoolean() {
			return readUInt8() != 0;
		}
		function readEnum(type) {
			var value = readUInt8();
			var values = enumValues[type];
			for (var key in values) {
				if (values.hasOwnProperty(key)) {
					if (values[key] == value) {
						return key;
					}
				}
			}
		}
		function readUuid() {
			var str = "";
			for (var i = 0; i < 16; i++) {
				str += (0x100 + pkt[pos + i]).toString(16).substr(-2);
			}
			pos += 16;
			if (str == "00000000000000000000000000000000") {
				str = null;
			}
			return str;
		}
		
		var opcode = readUInt8();
		switch (opcode) {
			case FlicEventOpcodes.AdvertisementPacket: {
				var evt = {
					scanId: readInt32(),
					bdAddr: readBdAddr(),
					name: readString(),
					rssi: readInt8(),
					isPrivate: readBoolean(),
					alreadyVerified: readBoolean(),
					alreadyConnectedToThisDevice: readBoolean(),
					alreadyConnectedToOtherDevice: readBoolean()
				};
				me.onEvent(opcode, evt);
				break;
			}
			case FlicEventOpcodes.CreateConnectionChannelResponse: {
				var evt = {
					connId: readInt32(),
					error: readEnum("CreateConnectionChannelError"),
					connectionStatus: readEnum("ConnectionStatus")
				};
				me.onEvent(opcode, evt);
				break;
			}
			case FlicEventOpcodes.ConnectionStatusChanged: {
				var evt = {
					connId: readInt32(),
					connectionStatus: readEnum("ConnectionStatus"),
					disconnectReason: readEnum("DisconnectReason")
				};
				me.onEvent(opcode, evt);
				break;
			}
			case FlicEventOpcodes.ConnectionChannelRemoved: {
				var evt = {
					connId: readInt32(),
					removedReason: readEnum("RemovedReason")
				};
				me.onEvent(opcode, evt);
				break;
			}
			case FlicEventOpcodes.ButtonUpOrDown:
			case FlicEventOpcodes.ButtonClickOrHold:
			case FlicEventOpcodes.ButtonSingleOrDoubleClick:
			case FlicEventOpcodes.ButtonSingleOrDoubleClickOrHold: {
				var evt = {
					connId: readInt32(),
					clickType: readEnum("ClickType"),
					wasQueued: readBoolean(),
					timeDiff: readInt32()
				}
				me.onEvent(opcode, evt);
				break;
			}
			case FlicEventOpcodes.NewVerifiedButton: {
				var evt = {
					bdAddr: readBdAddr()
				};
				me.onEvent(opcode, evt);
				break;
			}
			case FlicEventOpcodes.GetInfoResponse: {
				var evt = {
					bluetoothControllerState: readEnum("BluetoothControllerState"),
					myBdAddr: readBdAddr(),
					myBdAddrType: readEnum("BdAddrType"),
					maxPendingConnections: readUInt8(),
					maxConcurrentlyConnectedButtons: readInt16(),
					currentPendingConnections: readUInt8(),
					currentlyNoSpaceForNewConnection: readBoolean(),
					bdAddrOfVerifiedButtons: new Array(readUInt16())
				};
				for (var i = 0; i < evt.bdAddrOfVerifiedButtons.length; i++) {
					evt.bdAddrOfVerifiedButtons[i] = readBdAddr();
				}
				me.onEvent(opcode, evt);
				break;
			}
			case FlicEventOpcodes.NoSpaceForNewConnection:
			case FlicEventOpcodes.GotSpaceForNewConnection: {
				var evt = {
					maxConcurrentlyConnectedButtons: readUInt8()
				};
				me.onEvent(opcode, evt);
				break;
			}
			case FlicEventOpcodes.BluetoothControllerStateChange: {
				var evt = {
					state: readEnum("BluetoothControllerState")
				};
				me.onEvent(opcode, evt);
				break;
			}
			case FlicEventOpcodes.PingResponse: {
				var evt = {
					pingId: readInt32()
				}
				me.onEvent(opcode, evt);
				break;
			}
			case FlicEventOpcodes.GetButtonInfoResponse: {
				var evt = {
					bdAddr: readBdAddr(),
					uuid: readUuid(),
					color: readString() || null,
					serialNumber: readString() || null,
					flicVersion: readUInt8() || null,
					firmwareVersion: readUInt32() || null
				};
				me.onEvent(opcode, evt);
				break;
			}
			case FlicEventOpcodes.ScanWizardFoundPrivateButton:
			case FlicEventOpcodes.ScanWizardButtonConnected: {
				var evt = {
					scanWizardId: readInt32()
				};
				me.onEvent(opcode, evt);
				break;
			}
			case FlicEventOpcodes.ScanWizardFoundPublicButton: {
				var evt = {
					scanWizardId: readInt32(),
					bdAddr: readBdAddr(),
					name: readString()
				};
				me.onEvent(opcode, evt);
				break;
			}
			case FlicEventOpcodes.ScanWizardCompleted: {
				var evt = {
					scanWizardId: readInt32(),
					result: readEnum("ScanWizardResult")
				};
				me.onEvent(opcode, evt);
				break;
			}
			case FlicEventOpcodes.ButtonDeleted: {
				var evt = {
					bdAddr: readBdAddr(),
					deletedByThisClient: readBoolean()
				};
				me.onEvent(opcode, evt);
				break;
			}
			case FlicEventOpcodes.BatteryStatus: {
				var evt = {
					listenerId: readInt32(),
					batteryPercentage: readInt8(),
					timestamp: new Date(readUInt64() * 1000)
				};
				me.onEvent(opcode, evt);
				break;
			}
		}
	}
	
	this.sendCommand = function(opcode, obj) {
		var arrayBuffer = new ArrayBuffer(100);
		var arr = new Uint8Array(arrayBuffer);
		var pos = 2;
		function writeUInt8(v) {
			arr[pos++] = v;
		}
		function writeInt16(v) {
			arr[pos++] = v;
			arr[pos++] = v >> 8;
		}
		function writeInt32(v) {
			writeInt16(v);
			writeInt16(v >> 16);
		}
		function writeBdAddr(v) {
			for (var i = 15; i >= 0; i -= 3) {
				writeUInt8(parseInt(v.substr(i, 2), 16));
			}
		}
		function writeEnum(type, v) {
			writeUInt8(enumValues[type][v]);
		}
		
		writeUInt8(opcode);
		switch (opcode) {
			case FlicCommandOpcodes.GetInfo: {
				break;
			}
			case FlicCommandOpcodes.CreateScanner:
			case FlicCommandOpcodes.RemoveScanner: {
				writeInt32(obj.scanId);
				break;
			}
			case FlicCommandOpcodes.CreateConnectionChannel: {
				writeInt32(obj.connId);
				writeBdAddr(obj.bdAddr);
				writeEnum("LatencyMode", obj.latencyMode);
				writeInt16(obj.autoDisconnectTime);
				break;
			}
			case FlicCommandOpcodes.RemoveConnectionChannel: {
				writeInt32(obj.connId);
				break;
			}
			case FlicCommandOpcodes.ForceDisconnect:
			case FlicCommandOpcodes.GetButtonInfo:
			case FlicCommandOpcodes.DeleteButton: {
				writeBdAddr(obj.bdAddr);
				break;
			}
			case FlicCommandOpcodes.ChangeModeParameters: {
				writeInt32(obj.connId);
				writeEnum("LatencyMode", obj.latencyMode);
				writeInt16(obj.autoDisconnectTime);
				break;
			}
			case FlicCommandOpcodes.Ping: {
				writeInt32(obj.pingId);
				break;
			}
			case FlicCommandOpcodes.CreateScanWizard:
			case FlicCommandOpcodes.CancelScanWizard: {
				writeInt32(obj.scanWizardId);
				break;
			}
			case FlicCommandOpcodes.CreateBatteryStatusListener: {
				writeInt32(obj.listenerId);
				writeBdAddr(obj.bdAddr);
				break;
			}
			case FlicCommandOpcodes.RemoveBatteryStatusListener: {
				writeInt32(obj.listenerId);
				break;
			}
			default:
				return;
		}
		arr[0] = (pos - 2) & 0xff;
		arr[1] = (pos - 2) >> 8;
		var buffer = createBuffer(arrayBuffer, 0, pos);
		socket.write(buffer);
	};
	
	this.close = function() {
		socket.destroy();
	};
	
	// Public event listeners that is to be assigned
	this.onOpen = function() {};
	this.onClose = function(hadError) {};
	this.onEvent = function(opcode, evt) {};
	this.onError = function(error) {};
};

/**
 * FlicConnectionChannel
 *
 * A logical connection to a Flic button.
 * First create a connection channel, then add it to a FlicClient.
 * 
 * Constructor: bdAddr, options
 *   options is a dictionary containing the optional parameters latencyMode and autoDisconnectTime
 * 
 * Properties:
 * latencyMode
 * autoDisconnectTime
 * 
 * Events:
 * 
 * createResponse: error, connectionStatus
 * removed: removedReason
 * connectionStatusChanged: connectionStatus, disconnectReason
 * 
 * buttonUpOrDown: clickType, wasQueued, timeDiff
 * buttonClickOrHold: clickType, wasQueued, timeDiff
 * buttonSingleOrDoubleClick: clickType, wasQueued, timeDiff
 * buttonSingleOrDoubleClickOrHold: clickType, wasQueued, timeDiff
 */
var FlicConnectionChannel = (function() {
	var counter = 0;
	
	return function(bdAddr, options) {
		options = options || {};
		var latencyMode = ("latencyMode" in options) ? options.latencyMode : "NormalLatency";
		var autoDisconnectTime = ("autoDisconnectTime" in options) ? options.autoDisconnectTime : 511;
		
		EventEmitter.call(this);
		var id = counter;
		counter = (counter + 1) | 0;
		var me = this;
		
		var client = null;
		
		this._getId = function() { return id; };
		
		this._attach = function(rawClient) {
			client = rawClient;
			rawClient.sendCommand(FlicCommandOpcodes.CreateConnectionChannel, {
				connId: id,
				bdAddr: bdAddr,
				latencyMode: latencyMode,
				autoDisconnectTime: autoDisconnectTime
			});
		};
		this._detach = function(rawClient) {
			rawClient.sendCommand(FlicCommandOpcodes.RemoveConnectionChannel, {
				connId: id
			});
		};
		this._detached = function() {
			client = null;
		};
		this._onEvent = function(opcode, event) {
			switch (opcode) {
				case FlicEventOpcodes.CreateConnectionChannelResponse:
					me.emit("createResponse", event.error, event.connectionStatus);
					break;
				case FlicEventOpcodes.ConnectionStatusChanged:
					me.emit("connectionStatusChanged", event.connectionStatus, event.disconnectReason);
					break;
				case FlicEventOpcodes.ConnectionChannelRemoved:
					me.emit("removed", event.removedReason);
					break;
				case FlicEventOpcodes.ButtonUpOrDown:
					me.emit("buttonUpOrDown", event.clickType, event.wasQueued, event.timeDiff);
					break;
				case FlicEventOpcodes.ButtonClickOrHold:
					me.emit("buttonClickOrHold", event.clickType, event.wasQueued, event.timeDiff);
					break;
				case FlicEventOpcodes.ButtonSingleOrDoubleClick:
					me.emit("buttonSingleOrDoubleClick", event.clickType, event.wasQueued, event.timeDiff);
					break;
				case FlicEventOpcodes.ButtonSingleOrDoubleClickOrHold:
					me.emit("buttonSingleOrDoubleClickOrHold", event.clickType, event.wasQueued, event.timeDiff);
					break;
			}
		};
		
		Object.defineProperty(this, "latencyMode", {
			get: function() {
				return latencyMode;
			},
			set: function(value) {
				latencyMode = value;
				if (client != null) {
					client.sendCommand(FlicCommandOpcodes.ChangeModeParameters, {
						connId: id,
						latencyMode: latencyMode,
						autoDisconnectTime: autoDisconnectTime
					});
				}
			}
		});
		
		Object.defineProperty(this, "autoDisconnectTime", {
			get: function() {
				return autoDisconnectTime;
			},
			set: function(value) {
				autoDisconnectTime = value;
				if (client != null) {
					client.sendCommand(FlicCommandOpcodes.ChangeModeParameters, {
						connId: id,
						latencyMode: latencyMode,
						autoDisconnectTime: autoDisconnectTime
					});
				}
			}
		});
	};
})();
util.inherits(FlicConnectionChannel, EventEmitter);

/*
 * FlicBatteryStatusListener
 * 
 * First create a FlicBatteryStatusListener, then add it to the FlicClient.
 * 
 * Constructor: bdAddr
 * 
 * Events:
 * batteryStatus: batteryPercentage, timestamp (JS Date object)
 */
var FlicBatteryStatusListener = (function() {
	var counter = 0;
	
	return function(bdAddr) {
		EventEmitter.call(this);
		var me = this;
		
		var id = counter;
		counter = (counter + 1) | 0;
		
		this._getId = function() { return id; }
		
		this._attach = function(rawClient) {
			rawClient.sendCommand(FlicCommandOpcodes.CreateBatteryStatusListener, {
				listenerId: id,
				bdAddr: bdAddr
			});
		};
		this._detach = function(rawClient) {
			rawClient.sendCommand(FlicCommandOpcodes.RemoveBatteryStatusListener, {
				listenerId: id
			});
		};
		this._onEvent = function(opcode, event) {
			switch (opcode) {
				case FlicEventOpcodes.BatteryStatus:
					me.emit("batteryStatus", event.batteryPercentage, event.timestamp);
					break;
			}
		};
	}
})();
util.inherits(FlicBatteryStatusListener, EventEmitter);

/*
 * FlicScanner
 *
 * First create a FlicScanner, then add it to the FlicClient.
 * 
 * Constructor: no parameters
 * 
 * Events:
 * advertisementPacket: bdAddr, name, rssi, isPrivate, alreadyVerified, alreadyConnectedToThisDevice, alreadyConnectedToOtherDevice
 */
var FlicScanner = (function() {
	var counter = 0;
	
	return function() {
		
		EventEmitter.call(this);
		var me = this;
		
		var id = counter;
		counter = (counter + 1) | 0;
		
		this._getId = function() { return id; };
		
		this._attach = function(rawClient) {
			rawClient.sendCommand(FlicCommandOpcodes.CreateScanner, {
				scanId: id
			});
		};
		this._detach = function(rawClient) {
			rawClient.sendCommand(FlicCommandOpcodes.RemoveScanner, {
				scanId: id
			});
		}
		this._onEvent = function(opcode, event) {
			switch (opcode) {
				case FlicEventOpcodes.AdvertisementPacket:
					me.emit("advertisementPacket", event.bdAddr, event.name, event.rssi, event.isPrivate, event.alreadyVerified, event.alreadyConnectedToThisDevice, event.alreadyConnectedToOtherDevice);
					break;
			}
		};
	}
})();
util.inherits(FlicScanner, EventEmitter);

/*
 * FlicScanWizard
 *
 * First create a FlicScanWizard, then add it to the FlicClient.
 * 
 * Constructor: no parameters
 * 
 * Events:
 * foundPrivateButton: (no parameters)
 * foundPublicButton: bdAddr, name
 * buttonConnected: bdAddr, name
 * completed: result, bdAddr, name
 */
var FlicScanWizard = (function() {
	var counter = 0;
	
	return function() {
		
		EventEmitter.call(this);
		var me = this;
		
		var id = counter;
		counter = (counter + 1) | 0;
		var _bdaddr = null;
		var _name = null;
		
		this._getId = function() { return id; };
		
		this._attach = function(rawClient) {
			rawClient.sendCommand(FlicCommandOpcodes.CreateScanWizard, {
				scanWizardId: id
			});
		};
		this._detach = function(rawClient) {
			rawClient.sendCommand(FlicCommandOpcodes.CancelScanWizard, {
				scanWizardId: id
			});
		};
		this._onEvent = function(opcode, event) {
			switch (opcode) {
				case FlicEventOpcodes.ScanWizardFoundPrivateButton:
					me.emit("foundPrivateButton");
					break;
				case FlicEventOpcodes.ScanWizardFoundPublicButton:
					_bdaddr = event.bdAddr;
					_name = event.name;
					me.emit("foundPublicButton", _bdaddr, _name);
					break;
				case FlicEventOpcodes.ScanWizardButtonConnected:
					me.emit("buttonConnected", _bdaddr, _name);
					break;
				case FlicEventOpcodes.ScanWizardCompleted:
					var bdaddr = _bdaddr;
					var name = _name;
					_bdaddr = null;
					_name = null;
					me.emit("completed", event.result, bdaddr, name);
					break;
			}
		};
	}
})();
util.inherits(FlicScanWizard, EventEmitter);

/**
 * FlicClient
 *
 * High level class for communicating with flicd through a WebSocket proxy.
 * 
 * Constructor: host, [port]
 * 
 * Methods:
 * addScanner: FlicScanner
 * removeScanner: FlicScanner
 * addScanWizard: FlicScanWizard
 * cancelScanWizard: FlicScanWizard
 * addConnectionChannel: FlicConnectionChannel
 * removeConnectionChannel: FlicConnectionChannel
 * addBatteryStatusListener: FlicBatteryStatusListener
 * removeBatteryStatusListener: FlicBatteryStatusListener
 * getInfo: a callback function with one parameter "info", where info is a dictionary containing:
 *   bluetoothControllerState,
 *   myBdAddr,
 *   myBdAddrType,
 *   maxPendingConnections,
 *   maxConcurrentlyConnectedButtons,
 *   currentPendingConnections,
 *   bdAddrOfVerifiedButtons
 * getButtonInfo: bdAddr, callback
 *   Callback parameters: bdAddr, uuid, color, serialNumber, flicVersion, serialNumber
 * deleteButton: bdAddr
 * close
 * 
 * 
 * Events:
 * ready: (no parameters)
 * close: hadError
 * error: error
 * newVerifiedButton: bdAddr
 * noSpaceForNewConnection: maxConcurrentlyConnectedButtons
 * gotSpaceForNewConnection: maxConcurrentlyConnectedButtons
 * bluetoothControllerState: state
 * buttonDeleted: bdAddr, deletedByThisClient
 */
var FlicClient = function(host, port) {
	var rawClient = new FlicRawClient(host, port || 5551);
	
	EventEmitter.call(this);
	var me = this;
	
	var scanners = {};
	var scanWizards = {};
	var connectionChannels = {};
	var batteryStatusListeners = {};
	
	var getInfoResponseCallbackQueue = [];
	var getButtonInfoCallbackQueue = [];
	
	rawClient.onOpen = function() {
		me.emit("ready");
	};
	rawClient.onClose = function(hadError) {
		for (var connId in connectionChannels) {
			if (connectionChannels.hasOwnProperty(connId)) {
				connectionChannels[connId]._detached();
			}
		}
		me.emit("close", hadError);
	};
	
	rawClient.onEvent = function(opcode, event) {
		switch (opcode) {
			case FlicEventOpcodes.AdvertisementPacket: {
				if (scanners[event.scanId]) {
					scanners[event.scanId]._onEvent(opcode, event);
				}
				break;
			}
			case FlicEventOpcodes.CreateConnectionChannelResponse:
			case FlicEventOpcodes.ConnectionStatusChanged:
			case FlicEventOpcodes.ConnectionChannelRemoved:
			case FlicEventOpcodes.ButtonUpOrDown:
			case FlicEventOpcodes.ButtonClickOrHold:
			case FlicEventOpcodes.ButtonSingleOrDoubleClick:
			case FlicEventOpcodes.ButtonSingleOrDoubleClickOrHold: {
				if (connectionChannels[event.connId]) {
					var cc = connectionChannels[event.connId];
					if ((opcode == FlicEventOpcodes.CreateConnectionChannel && event.error != "NoError") || opcode == FlicEventOpcodes.ConnectionChannelRemoved) {
						delete connectionChannels[event.connId];
						cc._detached();
					}
					cc._onEvent(opcode, event);
				}
				break;
			}
			case FlicEventOpcodes.NewVerifiedButton: {
				me.emit("newVerifiedButton", event.bdAddr);
				break;
			}
			case FlicEventOpcodes.GetInfoResponse: {
				var callback = getInfoResponseCallbackQueue.shift();
				callback(event);
				break;
			}
			case FlicEventOpcodes.NoSpaceForNewConnection: {
				me.emit("noSpaceForNewConnection", event.maxConcurrentlyConnectedButtons);
				break;
			}
			case FlicEventOpcodes.GotSpaceForNewConnection: {
				me.emit("gotSpaceForNewConnection", event.maxConcurrentlyConnectedButtons);
				break;
			}
			case FlicEventOpcodes.BluetoothControllerStateChange: {
				me.emit("bluetoothControllerStateChange", event.state);
				break;
			}
			case FlicEventOpcodes.GetButtonInfoResponse: {
				var callback = getButtonInfoCallbackQueue.shift();
				callback(event.bdAddr, event.uuid, event.color, event.serialNumber, event.flicVersion, event.firmwareVersion);
				break;
			}
			case FlicEventOpcodes.ScanWizardFoundPrivateButton:
			case FlicEventOpcodes.ScanWizardFoundPublicButton:
			case FlicEventOpcodes.ScanWizardButtonConnected:
			case FlicEventOpcodes.ScanWizardCompleted: {
				if (scanWizards[event.scanWizardId]) {
					var scanWizard = scanWizards[event.scanWizardId];
					if (opcode == FlicEventOpcodes.ScanWizardCompleted) {
						delete scanWizards[event.scanWizardId];
					}
					scanWizard._onEvent(opcode, event);
				}
				break;
			}
			case FlicEventOpcodes.ButtonDeleted: {
				me.emit("buttonDeleted", event.bdAddr, event.deletedByThisClient);
				break;
			}
			case FlicEventOpcodes.BatteryStatus: {
				if (batteryStatusListeners[event.listenerId]) {
					batteryStatusListeners[event.listenerId]._onEvent(opcode, event);
				}
				break;
			}
		}
	};
	
	rawClient.onError = function(error) {
		me.emit("error", error);
	}
	
	// Public methods:
	
	this.addScanner = function(flicScanner) {
		if (flicScanner._getId() in scanners) {
			return;
		}
		scanners[flicScanner._getId()] = flicScanner;
		flicScanner._attach(rawClient);
	};
	this.removeScanner = function(flicScanner) {
		if (!(flicScanner._getId() in scanners)) {
			return;
		}
		delete scanners[flicScanner._getId()];
		flicScanner._detach(rawClient);
	};
	
	this.addScanWizard = function(flicScanWizard) {
		if (flicScanWizard._getId() in scanWizards) {
			return;
		}
		scanWizards[flicScanWizard._getId()] = flicScanWizard;
		flicScanWizard._attach(rawClient);
	};
	this.cancelScanWizard = function(flicScanWizard) {
		if (!(flicScanWizard._getId() in scanWizards)) {
			return;
		}
		flicScanWizard._detach(rawClient);
	};
	
	this.addConnectionChannel = function(connectionChannel) {
		if (connectionChannel._getId() in connectionChannels) {
			return;
		}
		connectionChannels[connectionChannel._getId()] = connectionChannel;
		connectionChannel._attach(rawClient);
	};
	this.removeConnectionChannel = function(connectionChannel) {
		if (!(connectionChannel._getId() in connectionChannels)) {
			return;
		}
		connectionChannel._detach(rawClient);
	};
	
	this.addBatteryStatusListener = function(listener) {
		if (listener._getId() in batteryStatusListeners) {
			return;
		}
		batteryStatusListeners[listener._getId()] = listener;
		listener._attach(rawClient);
	};
	this.removeBatteryStatusListener = function(listener) {
		if (!(listener._getId() in batteryStatusListeners)) {
			return;
		}
		listener._detach(rawClient);
	};
	
	this.getInfo = function(callback) {
		getInfoResponseCallbackQueue.push(callback);
		rawClient.sendCommand(FlicCommandOpcodes.GetInfo, {});
	};
	
	this.getButtonInfo = function(bdAddr, callback) {
		getButtonInfoCallbackQueue.push(callback);
		rawClient.sendCommand(FlicCommandOpcodes.GetButtonInfo, {bdAddr: bdAddr});
	};
	
	this.deleteButton = function(bdAddr) {
		rawClient.sendCommand(FlicCommandOpcodes.DeleteButton, {bdAddr: bdAddr});
	};
	
	this.close = function() {
		rawClient.close();
	};
};
util.inherits(FlicClient, EventEmitter);

module.exports = {
	FlicClient: FlicClient,
	FlicConnectionChannel: FlicConnectionChannel,
	FlicBatteryStatusListener: FlicBatteryStatusListener,
	FlicScanner: FlicScanner,
	FlicScanWizard: FlicScanWizard
};
