#!/usr/bin/env python3

import fliclib
import paho.mqtt.client as mqtt

mqclient = mqtt.Client(clean_session=True)
mqclient.connect("test.mosquitto.org", 1883, 60)
mqclient.loop_start()
mqclient.publish("flic/gateway", "Start")

client = fliclib.FlicClient("localhost")


def button_up_or_down(channel, click_type, was_queued, time_diff):
    print(channel.bd_addr)
    mqclient.publish("flic/%s" % channel.bd_addr, str(click_type))


def status_changed(channel, connection_status, disconnect_reason):
    print(channel.bd_addr)
    print(connection_status)
    print(disconnect_reason)
    mqclient.publish("flic/%s" % channel.bd_addr, str(connection_status))


def got_button(bd_addr):
    cc = fliclib.ButtonConnectionChannel(bd_addr)
    cc.on_button_up_or_down = button_up_or_down
    cc.on_connection_status_changed = status_changed
    client.add_connection_channel(cc)


def got_info(items):
    for bd_addr in items["bd_addr_of_verified_buttons"]:
        got_button(bd_addr)


client.get_info(got_info)
client.on_new_verified_button = got_button
client.handle_events()
