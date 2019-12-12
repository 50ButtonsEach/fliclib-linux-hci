/*
 * Use this tool to pair one Flic button.
 * Run the example.js to get button events.
 */

var fliclib = require("./fliclibNodeJs");
var FlicClient = fliclib.FlicClient;
var FlicConnectionChannel = fliclib.FlicConnectionChannel;
var FlicScanner = fliclib.FlicScanner;

var client = new FlicClient("localhost", 5551);

function startScanWizard() {
	console.log("Welcome to the add new button wizard. Press and hold down your Flic button to add it.");
	
	var timeout = null;
	
	function success(cc, bdAddr) {
		client.removeConnectionChannel(cc);
		console.log("Button " + bdAddr + " successfully added!");
		done();
	}
	function failed(msg) {
		console.log("Scan Wizard Failed: " + msg);
		done();
	}
	function done() {
		client.close();
		if (timeout != null) {
			clearTimeout(timeout);
		}
	}
	var cc = null;
	var scanner = new FlicScanner();
	scanner.on("advertisementPacket", function(bdAddr, name, rssi, isPrivate, alreadyVerified, alreadyConnectedToThisDevice, alreadyConnectedToOtherDevice) {
		if (alreadyVerified) {
			return;
		}
		if (isPrivate) {
			console.log("Your button is private. Hold down for 7 seconds to make it public.");
			return;
		}
		client.removeScanner(scanner);
		
		cc = new FlicConnectionChannel(bdAddr);
		cc.on("createResponse", function(error, connectionStatus) {
			if (connectionStatus == "Ready") {
				// Got verified by someone else between scan result and this event
				success(cc, bdAddr);
			} else if (error != "NoError") {
				failed("Too many pending connections");
			} else {
				console.log("Found a public button. Now connecting...");
				timeout = setTimeout(function() {
					client.removeConnectionChannel(cc);
				}, 30 * 1000);
			}
		});
		cc.on("connectionStatusChanged", function(connectionStatus, disconnectReason) {
			if (connectionStatus == "Ready") {
				success(cc, bdAddr);
			}
		});
		cc.on("removed", function(removedReason) {
			if (removedReason == "RemovedByThisClient") {
				failed("Timed out");
			} else {
				failed(removedReason);
			}
		});
		client.addConnectionChannel(cc);
	});
	client.addScanner(scanner);
}

client.on("ready", startScanWizard);
