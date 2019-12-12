#!/usr/bin/env python3

# Almost same as test_client.py but also includes a command line interface with a few commands

import fliclib
import threading
import time

client = fliclib.FlicClient("localhost")

def got_button(bd_addr):
	cc = fliclib.ButtonConnectionChannel(bd_addr)
	cc.on_button_up_or_down = \
		lambda channel, click_type, was_queued, time_diff: \
			print(channel.bd_addr + " " + str(click_type))
	cc.on_connection_status_changed = \
		lambda channel, connection_status, disconnect_reason: \
			print(channel.bd_addr + " " + str(connection_status) + (" " + str(disconnect_reason) if connection_status == fliclib.ConnectionStatus.Disconnected else ""))
	client.add_connection_channel(cc)

def got_info(items):
	for bd_addr in items["bd_addr_of_verified_buttons"]:
		got_button(bd_addr)

client.get_info(got_info)

client.on_new_verified_button = got_button

# If you have external input, such as a GUI or a command line interface, a separate thread is
# needed since the thread that handles the events has only capabilities for waiting on the socket connected to flicd.
class T(threading.Thread):
	def run(self):
		scanner = fliclib.ButtonScanner()
		scanner.on_advertisement_packet = \
			lambda scanner, bd_addr, name, rssi, is_private, already_verified, already_connected_to_this_device, already_connected_to_other_device: \
				print(bd_addr + " " + name + " " + ("Private" if is_private else "Public") + (" already verified" if already_verified else ""))
		
		print("Available commands: exit, startScan, stopScan")
		while True:
			cmd = input("> ")
			if cmd == "exit":
				client.close()
				return
			elif cmd == "startScan":
				client.add_scanner(scanner)
			elif cmd == "stopScan":
				client.remove_scanner(scanner)

T().start()

client.handle_events()
