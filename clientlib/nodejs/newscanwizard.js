/*
 * Use this tool to pair one Flic button.
 * Run the example.js to get button events.
 */

var fliclib = require("./fliclibNodeJs");
var FlicClient = fliclib.FlicClient;
var FlicScanWizard = fliclib.FlicScanWizard;

var client = new FlicClient("localhost", 5551);

function startScanWizard() {
	console.log("Welcome to the add new button wizard. Press your Flic button to add it.");
	
	var wizard = new FlicScanWizard();
	wizard.on("foundPrivateButton", function() {
		console.log("Your button is private. Hold down for 7 seconds to make it public.");
	});
	wizard.on("foundPublicButton", function(bdAddr, name) {
		console.log("Found public button " + bdAddr + " (" + name + "). Now connecting...");
	});
	wizard.on("buttonConnected", function(bdAddr, name) {
		console.log("Button connected. Now verifying and pairing...");
	});
	wizard.on("completed", function(result, bdAddr, name) {
		console.log("Completed with result: " + result);
		if (result == "WizardSuccess") {
			console.log("Your new button is " + bdAddr);
		}
		client.close();
	});
	
	client.addScanWizard(wizard);
}

client.on("ready", startScanWizard);
