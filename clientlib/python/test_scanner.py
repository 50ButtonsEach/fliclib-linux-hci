#!/usr/bin/env python3

# Simple program that only scans for buttons

import fliclib

client = fliclib.FlicClient("localhost")

scanner = fliclib.ButtonScanner()
scanner.on_advertisement_packet = \
	lambda scanner, bd_addr, name, rssi, is_private, already_verified, already_connected_to_this_device, already_connected_to_other_device: \
		print(bd_addr + " " + name + " " + ("Private" if is_private else "Public") + (" already verified" if already_verified else " not verified before"))
client.add_scanner(scanner)

client.handle_events()
