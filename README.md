# Flic SDK for Linux Beta
With this SDK you can connect to and interact with Flic buttons.

## Previous SDK

Previously we had an implementation for Linux that made use of the Bluez bluetooth stack. There were several complications however about that one. The first is that all the BLE functionality is marked as experimental, meaning you have to compile and run a specialized version of Bluez. The other one is that there are breakages between different Bluez versions. The third reason is that we find many things unintuitive and problematic with their current BLE implementation that currently makes it unusable for the Flic concept. Therefore we have decided to change to a new approach.

## High level description

This library is built on top of the HCI_CHANNEL_USER capability of the Linux kernel. This gives the library an exclusive access to directly access the bluetooth controller, which means no Bluetooth stack (like Bluez) is needed on the host side. This way we can (hopefully) guarantee stability compared to other solutions but also optimize the protocol to only exchange the packets that are needed for the Flic buttons to communicate. The downside however is that you will not be able to use that dedicated bluetooth controller for other bluetooth connections, such as streaming audio. If you need to use bluetooth for something else, you can always plug in an extra bluetooth dongle and dedicate that for Flic instead. The library consists of a client - server solution. The server is a software that runs and interacts with the bluetooth controller to connect to Flic buttons. Client programs can connect to this server using a simple well-documented API over a TCP socket to scan and connect Flic buttons and get their button events.

### What's included
* `flicd` - This is the central daemon that manages all buttons. Run it with ./flicd -f flic.sqlite3
* `clientlib/java` - A library that implements the protocol that should be very easy to use. Two example programs are included as well. Open it up in IntelliJ.
* `clientlib/python` - A library for python 3.3 or higher, very similar to the Java library. Some example programs included.
* `clientlib/websocket` - A websocket proxy and a demo client in html/javascript which you can use to scan and connect buttons.
* `clientlib/nodejs` - A library for nodejs and examples.
* `simpleclient` - A simple command line client with source code that can be used to test the protocol.
* `client_protocol_packets.h` - C/C++ structs for all packets that can be included in a C/C++ program.

### Supported platforms
Binaries and libraries has been compiled for x86_64, i386 and armv6l. The minimum Linux kernel supported is 3.13. All code has been compiled and tested on Ubuntu 15.10 for desktop and Raspbian Jessy. This means it should be compatible with desktop systems and Raspberry Pi 1, 2 & 3. I have tried to make the binaries as portable as possible.

If you have compiled your own kernel you must make sure to include support for Bluetooth. In kernel config, enable at least Networking support -> Bluetooth subsystem support -> Bluetooth device drivers -> HCI USB driver and HCI UART driver.

### Bluetooth controllers
All Bluetooth controllers with support for Bluetooth 4.0 and Bluetooth Low Energy (Bluetooth Smart) that have Linux support should work. Generally small cheap USB dongles seem to have shorter range than those integrated inside computers. We have tested compatibility with some common Bluetooth controllers. The following devices have been tested and confirmed:

**Plugable USB Bluetooth 4.0 Low Energy Micro Adapter / Asus USB-BT400 (Broadcom BCM20702 Bluetooth 4.0)**
- Supports 14 concurrent connections and in total 32 pending connections.

**Cambridge Silicon Radio CSR8510 A10 based controllers (Bluetooth 4.0)**
- Supports 5 concurrent connections and in total 25 pending connections.

**Raspberry Pi 3 model B (Broadcom BCM43438 Bluetooth 4.1)**
- Supports 10 concurrent connections and in total 128 pending connections.

**Intel Centrino Advanced-N 6235 (Bluetooth 4.0)**
- Supports 3 concurrent connections and in total 25 pending connections.

**Intel Wireless 7260 (Bluetooth 4.0)**
- Supports 7 concurrent connections and in total 32 pending connections.

**IMC Networks Atheros AR3012 Bluetooth (Bluetooth 4.0)**
- Supports 10 concurrent connections and in total 128 pending connections. Can be a bit buggy sometimes, like dropping and duplicating BLE packets. Also sometimes "forgets" to disconnect a BLE link when instructed to. Should however work ok in most cases.

## Quick start
### Packages
There are no dependencies except the standard C/C++ libraries, which should be installed by default on most Linux distributions.
### Running
It might be a good idea to disable a currently running bluez daemon (bluetoothd) to avoid interference, although not necessary. To see if it's running, run `ps aux | grep bluetoothd`. If it's running, try to disable it through the system's tools `service bluetooth stop` or `systemctl stop bluetooth` on Ubuntu, or just kill the process.

The server process needs to have access to the Bluetooth HCI channel. There are two ways to get this. Either run the daemon as root or give the process permissions by executing `sudo setcap cap_net_admin=ep ./flicd` which enables you to run it later as a normal user.

Now start the daemon in one terminal by executing `./flicd -f flic.sqlite3`. Additional options are listed if you leave out the database argument.

In another terminal open the simpleclient directory, compile it with make and run with `./simpleclient localhost`. You will be shown the available commands. Type `startScanWizard` and press enter to scan and add a button. Then press your flic button (and make sure it is disconnected to any other devices such as a smartphone) and follow the instructions in the console. After your button has been added, enter the command `connect <BDADDR> <id>` where `<BDADDR>` is the address that appeared during scan. For `<id>`, put any integer that will be used later to refer to this connection. The button should now connect and you will see click events appear. Type `disconnect <id>` to later disconnect.

You can also try out the websocket example. Run both the daemon and the websocket proxy. Then open up the client html page.

### Usage of flicd
```
Usage: ./flicd -f sqlite_db_file.db [options]


    --help          Prints this text and exits.

-f  --db-file       Sqlite3 db file to use. In this file bonding information is stored for verified Flic buttons.
                    If the file doesn't exist, it is created.
                    
-b  --my-bdaddr     Static random bdaddr to use for the bluetooth controller.
                    Use this optional argument to assign a custom bdaddr instead of using the one burnt-in into the controller.
                    It's useful for scenarios where you want to use previously set up Flic bonding information
                    with a different bluetooth controller. Then simply use the same bdaddr for both controllers.
                    Must be of the form xx:xx:xx:xx:xx:xx (6 hexadecimal numbers) where the first byte is between 0xc0 and 0xff.
                    
-s  --server-addr   Server IP address to bind to. 127.0.0.1 is the default which means only clients on this computer
                    can connect to the server. Use 0.0.0.0 if you want the server to be accessible from the outside.
                    
-p  --server-port   Server port to bind to. The default is 5551.
                    
-h  --hci-dev       HCI device to use. The default is hci0.

-d  --daemon        Run flicd as a Linux daemon.

-l  --log-file      Specify a log file name instead of using stderr.

-w  --wait-for-hci  When starting flicd, wait for hci endpoint to become available instead of exiting with failure status.
```

## Troubleshooting
To see the available HCI available bluetooth controllers, run the `hciconfig` command. If it prints nothing, Linux can't find it. Run `dmesg` to see if there are any kernel error messages.
If for some reason your bluetooth controller is powered off, power it on by executing the `sudo bluetoothctl` command and in that type `power on`.

You can also use run `sudo btmon` - an HCI packet monitor by Bluez, to see exactly what is going on.

## Documentation
The full specification for the protocol that is used to talk to the server deamon can be found in ProtocolDocumentation.md.

Documentation for the Java implementation is included as javadoc.

## Feedback
Be sure to post a Github issue if you find a bug, something you don't like or if something is wrong or should be changed. You can also submit a Pull request if you have a ready improvement.
