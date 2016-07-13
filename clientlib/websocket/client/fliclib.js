/**
 * Flic lib for javascript over WebSocket.
 *
 * You need to run the websocketproxy program and connect through that to the flicd.
 * See the index.html file for examples.
 * See the official protocol specification for more details.
 */

var FlicCommandOpcodes = {
	GetInfo: 0,
	CreateScanner: 1,
	RemoveScanner: 2,
	CreateConnectionChannel: 3,
	RemoveConnectionChannel: 4,
	ForceDisconnect: 5,
	ChangeModeParameters: 6,
	Ping: 7,
	GetButtonUUID: 8,
	CreateScanWizard: 9,
	CancelScanWizard: 10
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
	GetButtonUUIDResponse: 14,
	ScanWizardFoundPrivateButton: 15,
	ScanWizardFoundPublicButton: 16,
	ScanWizardButtonConnected: 17,
	ScanWizardCompleted: 18
};

/**
 * FlicRawWebsocketClient
 *
 * This is a low level client that is used by the high level FlicClient below.
 * 
 * Example:
 * var cl = new FlicRawWebsocketClient("ws://localhost:5553");
 * cl.onEvent = function(opcode, evt) {
 *     console.log("Incoming event " + opcode, evt);
 * }
 * cl.onWsOpen = function() {
 *     console.log("open");
 *     cl.sendCommand(FlicCommandOpcodes.GetInfo, {});
 * }
 * 
 */
var FlicRawWebsocketClient = function(wsAddress) {
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
			
			CouldntLoadDevice: 7
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
			WizardInvalidData: 6
		},

		BluetoothControllerState: {
			Detached: 0,
			Resetting: 1,
			Attached: 2
		}
	};
	
	var ws = new WebSocket(wsAddress);
	ws.binaryType = "arraybuffer";
	ws.onopen = onWsOpen;
	ws.onclose = onWsClose;
	ws.onmessage = onWsMessage;
	ws.onerror = onWsError;
	
	var me = this;
	
	function onWsOpen(event) {
		me.onWsOpen(event);
	}
	
	function onWsClose(event) {
		me.onWsClose(event);
	}
	
	function onWsError(event) {
		console.log("Warning: " + event.data);
	}
	
	function onWsMessage(event) {
		var pkt = new Uint8Array(event.data);
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
		function readName() {
			var len = readUInt8();
			var nameString = new TextDecoder("utf-8").decode(new Uint8Array(event.data, pos, len));
			pos += 16;
			return nameString;
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
					name: readName(),
					rssi: readInt8(),
					isPrivate: readBoolean(),
					alreadyVerified: readBoolean()
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
				};
				me.onEvent(opcode, evt);
				break;
			}
			case FlicEventOpcodes.GetButtonUUIDResponse: {
				var evt = {
					bdAddr: readBdAddr(),
					uuid: readUuid()
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
					name: readName()
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
		}
	}
	
	this.sendCommand = function(opcode, obj) {
		var arrayBuffer = new ArrayBuffer(100);
		var arr = new Uint8Array(arrayBuffer);
		var pos = 0;
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
			case FlicCommandOpcodes.GetButtonUUID: {
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
			default:
				return;
		}
		ws.send(new Uint8Array(arrayBuffer, 0, pos));
	};
	
	// Public event listeners that is to be assigned
	this.onWsOpen = function(event) {};
	this.onWsClose = function(event) {};
	this.onEvent = function(opcode, evt) {};
};

/**
 * FlicConnectionChannel
 *
 * A logical connection to a Flic button.
 * First create a connection channel, then add it to a FlicClient.
 */
var FlicConnectionChannel = (function() {
	var counter = 0;
	
	return function(bdAddr, options) {
		options = options || {};
		var latencyMode = (latencyMode in options) ? options.latencyMode : "NormalLatency";
		var autoDisconnectTime = (autoDisconnectTime in options) ? options.autoDisconnectTime : 511;
		
		var onCreateResponse = options.onCreateResponse || function(error, connectionStatus){};
		var onRemoved = options.onRemoved || function(removedReason){};
		var onConnectionStatusChanged = options.onConnectionStatusChanged || function(connectionStatus, disconnectReason){};
		
		var onButtonUpOrDown = options.onButtonUpOrDown || function(clickType, wasQueued, timeDiff){};
		var onButtonClickOrHold = options.onButtonClickOrHold || function(clickType, wasQueued, timeDiff){};
		var onButtonSingleOrDoubleClick = options.onButtonSingleOrDoubleClick || function(clickType, wasQueued, timeDiff){};
		var onButtonSingleOrDoubleClickOrHold = options.onButtonSingleOrDoubleClickOrHold || function(clickType, wasQueued, timeDiff){};
		
		var id = counter++;
		
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
					onCreateResponse(event.error, event.connectionStatus);
					break;
				case FlicEventOpcodes.ConnectionStatusChanged:
					onConnectionStatusChanged(event.connectionStatus, event.disconnectReason);
					break;
				case FlicEventOpcodes.ConnectionChannelRemoved:
					onRemoved(event.removedReason);
					break;
				case FlicEventOpcodes.ButtonUpOrDown:
					onButtonUpOrDown(event.clickType, event.wasQueued, event.timeDiff);
					break;
				case FlicEventOpcodes.ButtonClickOrHold:
					onButtonClickOrHold(event.clickType, event.wasQueued, event.timeDiff);
					break;
				case FlicEventOpcodes.ButtonSingleOrDoubleClick:
					onButtonSingleOrDoubleClick(event.clickType, event.wasQueued, event.timeDiff);
					break;
				case FlicEventOpcodes.ButtonSingleOrDoubleClickOrHold:
					onButtonSingleOrDoubleClickOrHold(event.clickType, event.wasQueued, event.timeDiff);
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

/*
 * FlicScanner
 *
 * First create a FlicScanner, then add it to the FlicClient.
 */
var FlicScanner = (function() {
	var counter = 0;
	
	return function(options) {
		options = options || {};
		
		var onAdvertisementPacket = options.onAdvertisementPacket || function(bdAddr, name, rssi, isPrivate, alreadyVerified){};
		
		var id = counter++;
		
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
		};
		this._onEvent = function(opcode, event) {
			switch (opcode) {
				case FlicEventOpcodes.AdvertisementPacket:
					onAdvertisementPacket(event.bdAddr, event.name, event.rssi, event.isPrivate, event.alreadyVerified);
					break;
			}
		};
	}
})();

/*
 * FlicScanWizard
 *
 * First create a FlicScanWizard, then add it to the FlicClient.
 */
var FlicScanWizard = (function() {
	var counter = 0;
	
	return function(options) {
		options = options || {};
		
		var onFoundPrivateButton = options.onFoundPrivateButton || function(){};
		var onFoundPublicButton = options.onFoundPublicButton || function(bdAddr, name){}
		var onButtonConnected = options.onButtonConnected || function(bdAddr, name){}
		var onCompleted = options.onCompleted || function(result, bdAddr, name){}
		
		var id = counter++;
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
					onFoundPrivateButton();
					break;
				case FlicEventOpcodes.ScanWizardFoundPublicButton:
					_bdaddr = event.bdAddr;
					_name = event.name;
					onFoundPublicButton(_bdaddr, _name);
					break;
				case FlicEventOpcodes.ScanWizardButtonConnected:
					onButtonConnected(_bdaddr, _name);
					break;
				case FlicEventOpcodes.ScanWizardCompleted:
					var bdaddr = _bdaddr;
					var name = _name;
					_bdaddr = null;
					_name = null;
					onCompleted(event.result, bdaddr, name);
					break;
			}
		};
	}
})();

/**
 * FlicClient
 *
 * High level class for communicating with flicd through a WebSocket proxy.
 */
var FlicClient = function(wsAddress) {
	var rawClient = new FlicRawWebsocketClient(wsAddress);
	
	var me = this;
	
	var scanners = {};
	var scanWizards = {};
	var connectionChannels = {};
	
	var getInfoResponseCallbackQueue = [];
	var getButtonUUIDCallbackQueue = [];
	
	rawClient.onWsOpen = function(event) {
		me.onReady();
	};
	rawClient.onWsClose = function(event) {
		me.onClose();
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
				me.onNewVerifiedButton(event.bdAddr);
				break;
			}
			case FlicEventOpcodes.GetInfoResponse: {
				var callback = getInfoResponseCallbackQueue.shift();
				callback(event);
				break;
			}
			case FlicEventOpcodes.NoSpaceForNewConnection: {
				me.onNoSpaceForNewConnection(event.maxConcurrentlyConnectedButtons);
				break;
			}
			case FlicEventOpcodes.GotSpaceForNewConnection: {
				me.onGotSpaceForNewConnection(event.maxConcurrentlyConnectedButtons);
				break;
			}
			case FlicEventOpcodes.BluetoothControllerStateChange: {
				me.onBluetoothControllerStateChange(event.state);
				break;
			}
			case FlicEventOpcodes.GetButtonUUIDResponse: {
				var callback = getButtonUUIDCallbackQueue.shift();
				callback(event.bdAddr, event.uuid);
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
		}
	};
	
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
	
	this.getInfo = function(callback) {
		getInfoResponseCallbackQueue.push(callback);
		rawClient.sendCommand(FlicCommandOpcodes.GetInfo, {});
	};
	
	this.getButtonUUID = function(bdAddr, callback) {
		getButtonUUIDCallbackQueue.push(callback);
		rawClient.sendCommand(FlicCommandOpcodes.GetButtonUUID, {bdAddr: bdAddr});
	};
	
	this.onReady = function(){}
	this.onClose = function(){}
	
	this.onNewVerifiedButton = function(bdAddr){}
	this.onNoSpaceForNewConnection = function(maxConcurrentlyConnectedButtons){}
	this.onGotSpaceForNewConnection = function(maxConcurrentlyConnectedButtons){}
	this.onBluetoothControllerStateChange = function(state){}
};

