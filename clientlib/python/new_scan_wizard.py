#!/usr/bin/env python3

# Scan Wizard application.
#
# This program starts scanning of new Flic buttons that have not previously been verified by the server.
# Once it finds a button that is in private mode, it shows a message that the user should hold it down for 7 seconds to make it public.
# Once it finds a button that is in public mode, it attempts to connect to it.
# If it could be successfully connected and verified, the bluetooth address is printed and the program exits.
# If it could not be verified within 30 seconds, the scan is restarted.

import fliclib

client = fliclib.FlicClient("localhost")

def on_found_private_button(scan_wizard):
	print("Found a private button. Please hold it down for 7 seconds to make it public.")

def on_found_public_button(scan_wizard, bd_addr, name):
	print("Found public button " + bd_addr + " (" + name + "), now connecting...")

def on_button_connected(scan_wizard, bd_addr, name):
	print("The button was connected, now verifying...")

def on_completed(scan_wizard, result, bd_addr, name):
	print("Scan wizard completed with result " + str(result) + ".")
	if result == fliclib.ScanWizardResult.WizardSuccess:
		print("Your button is now ready. The bd addr is " + bd_addr + ".")
	client.close()

wizard = fliclib.ScanWizard()
wizard.on_found_private_button = on_found_private_button
wizard.on_found_public_button = on_found_public_button
wizard.on_button_connected = on_button_connected
wizard.on_completed = on_completed
client.add_scan_wizard(wizard)

print("Welcome to Scan Wizard. Please press your Flic button.")

client.handle_events()
