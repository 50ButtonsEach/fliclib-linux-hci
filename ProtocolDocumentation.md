Fliclib socket protocol documentation
=====================================

The protocol is designed to be simple to implement and use, while still being powerful enough to handle more complicated situations. It is a binary client - server protocol over a TCP socket. The client sends _commands_ and the server sends _events_. Each command or event is called a _packet_ and every packet is prefixed with a 16-bit little endian encoded length integer when sent over the socket.

 - The length header does not include its own length.
 - The first byte in each packet consists of an opcode that is unique for that kind of packet.
 - All integers are encoded in little endian byte order.
 - Booleans (bool) are encoded as a single byte - 0 means false and 1 means true.
 - Enums are encoded as a single byte. The corresponding integer byte value is 0 for the first possible value, then 1, then 2 and so on for the other values.
 - There are no alignment or padding between items in a packet.
 - The items are serialized in the same order as noted in this document.
 - In future versions the packets might get extended with more fields. When receiving a packet, ignore excessive bytes not specified in this document.

A bluetooth address (bdaddr_t) is encoded in little endan, 6 bytes in total. When such an address is written as a string, it is normally written in big endian, where each byte is encoded in hex and colon as separator for each byte. For example, the address `08:09:0a:0b:0c:0d` is encoded as the bytes `0x0d, 0x0c, 0x0b, 0x0a, 0x09, 0x08`.

Summary
-------
  * [Enums](#enums)
    * [CreateConnectionChannelError](#createconnectionchannelerror)
    * [ConnectionStatus](#connectionstatus)
    * [DisconnectReason](#disconnectreason)
    * [RemovedReason](#removedreason)
    * [ClickType](#clicktype)
    * [BdAddrType](#bdaddrtype)
    * [LatencyMode](#latencymode)
    * [BluetoothControllerState](#bluetoothcontrollerstate)
    * [ScanWizardResult](#scanwizardresult)
  * [Commands](#commands)
    * [CmdGetInfo](#cmdgetinfo)
    * [CmdCreateScanner](#cmdcreatescanner)
    * [CmdRemoveScanner](#cmdremovescanner)
    * [CmdCreateConnectionChannel](#cmdcreateconnectionchannel)
    * [CmdRemoveConnectionChannel](#cmdremoveconnectionchannel)
    * [CmdForceDisconnect](#cmdforcedisconnect)
    * [CmdChangeModeParameters](#cmdchangemodeparameters)
    * [CmdPing](#cmdping)
    * [CmdGetButtonInfo](#cmdgetbuttoninfo)
    * [CmdCreateScanWizard](#cmdcreatescanwizard)
    * [CmdCancelScanWizard](#cmdcancelscanwizard)
    * [CmdDeleteButton](#cmddeletebutton)
    * [CmdCreateBatteryStatusListener](#cmdcreatebatterystatuslistener)
  * [Events](#events)
    * [EvtAdvertisementPacket](#evtadvertisementpacket)
    * [EvtCreateConnectionChannelResponse](#evtcreateconnectionchannelresponse)
    * [EvtConnectionStatusChanged](#evtconnectionstatuschanged)
    * [EvtConnectionChannelRemoved](#evtconnectionchannelremoved)
    * [EvtButtonEvent](#evtbuttonevent)
    * [EvtNewVerifiedButton](#evtnewverifiedbutton)
    * [EvtGetInfoResponse](#evtgetinforesponse)
    * [EvtNoSpaceForNewConnection](#evtnospacefornewconnection)
    * [EvtGotSpaceForNewConnection](#evtgotspacefornewconnection)
    * [EvtBluetoothControllerStateChange](#evtbluetoothcontrollerstatechange)
    * [EvtPingResponse](#evtpingresponse)
    * [EvtGetButtonInfoResponse](#evtgetbuttoninforesponse)
    * [EvtScanWizardFoundPrivateButton](#evtscanwizardfoundprivatebutton)
    * [EvtScanWizardFoundPublicButton](#evtscanwizardfoundpublicbutton)
    * [EvtScanWizardButtonConnected](#evtscanwizardbuttonconnected)
    * [EvtScanWizardCompleted](#evtscanwizardcompleted)
    * [EvtButtonDeleted](#evtbuttondeleted)
    * [EvtBatteryStatus](#evtbatterystatus)

Enums
-----
### CreateConnectionChannelError
**NoError** -
There were space in the bluetooth controller's white list to accept a physical pending connection for this button

**MaxPendingConnectionsReached** -
There were no space left in the bluetooth controller to allow a new pending connection

### ConnectionStatus
**Disconnected** -
Not currently an established connection, but will connect as soon as the button is pressed and it is in range as long as the connection channel hasn't been removed (and unless maximum number of concurrent connections has been reached or the bluetooth controller has been detached).

**Connected** -
The physical bluetooth connection has just been established and the server and the button are currently verifying each other. As soon as this is done, it will switch to the ready status.

**Ready** -
The verification is done and button events may now arrive.

### DisconnectReason
**Unspecified** -
Unknown reason

**ConnectionEstablishmentFailed** -
The bluetooth controller established a connection, but the Flic button didn't answer in time.

**TimedOut** -
The connection to the Flic button was lost due to either being out of range or some radio communication problems.

**BondingKeysMismatch** -
The server and the Flic button for some reason don't agree on the previously established bonding keys.

### RemovedReason
**RemovedByThisClient** -
The connection channel was removed by this client.

**ForceDisconnectedByThisClient** -
The connection channel was removed due to a force disconnect by this client.

**ForceDisconnectedByOtherClient** -
Another client force disconnected the button used in this connection channel.

The next four reasons might only happen if the Flic button is previously not verified, i.e. these are errors that might happen during the bonding process.

**ButtonIsPrivate** -
The button is not in public mode. Hold it down for 7 seconds while not trying to establish a connection, then try to reconnect by creating a new connection channel.

**VerifyTimeout** -
After the connection was established, the bonding procedure didn't complete in time.

**InternetBackendError** -
The internet request to the Flic backend failed.

**InvalidData** -
According to the Flic backend, this Flic button supplied invalid identity data.

The next reason may only occur on Windows (i.e. the Windows daemon is used).

**CouldntLoadDevice**
The file representing the Flic Bluetooth device could not be opened, or it is reporting invalid status. If this happens, manually unpair the device in Windows's Bluetooth settings.

**DeletedByThisClient** -
The button was deleted by this client by a call to CmdDeleteButton.

**DeletedByOtherClient** -
The button was deleted by another client by a call to CmdDeleteButton.

**ButtonBelongsToOtherPartner** -
The button belongs to another PbF partner.

**DeletedFromButton** -
The button was factory reset, or the pairing has been removed to fit a new one.

### ClickType
**ButtonDown** -
The button was pressed.

**ButtonUp** -
The button was released.

**ButtonClick** -
The button was clicked, and was held for at most 1 seconds between press and release.

**ButtonSingleClick** -
The button was clicked once.

**ButtonDoubleClick** -
The button was clicked twice. The time between the first and second press must be at most 0.5 seconds.

**ButtonHold** -
The button was held for at least 1 second.

### BdAddrType
The server can be configured to either use the burnt-in public address stored inside the bluetooth controller, or to use a custom random static address. This custom address is a good idea if you want to be able to use your database with bonding information with a different bluetooth controller.

**PublicBdAddrType**

**RandomBdAddrType**

### LatencyMode
This specifies the accepted latency mode for the corresponding connection channel. The physical bluetooth connection will use the lowest mode set by any connection channel. The battery usage for the Flic button is normally about the same for all modes if the connection is stable. However lower modes will have higher battery usage if the connection is unstable. Lower modes also consumes more power for the client, which is normally not a problem since most computers run on wall power or have large batteries.

**Normal** -
Up to 100 ms latency.

**Low** -
Up to 17.5 ms latency.

**High** -
Up to 275 ms latency.

### BluetoothControllerState
The server software detects when the bluetooth controller is removed or is made unavailable. It will then repeatedly retry to re-established a connection to the same bluetooth controller.

**Detached** -
The server software has lost the HCI socket to the bluetooth controller and is trying to reconnect.

**Resetting** -
The server software has just got connected to the HCI socket and initiated a reset of the bluetooth controller.

**Attached** -
The bluetooth controller has done initialization and is up and running.

### ScanWizardResult
The result of a scan wizard. When the scan wizard is completed it will stop and return a result.

**WizardSuccess** -
Indicates that a button was successfully paired and verified. You may now create a connection channel to that button.

**WizardCancelledByUser** -
A CmdCancelScanWizard was sent.

**WizardFailedTimeout** -
The scan wizard did not make any progress for some time. Current timeouts are 20 seconds for finding any button, 20 seconds for finding a public button (in case of a private button was found), 10 seconds for connecting the button, 30 seconds for pairing and verifying the button.

**WizardButtonIsPrivate** -
First the button was advertising public status, but after connecting it reports private. Probably it switched from public to private just when the connection attempt was started.

**WizardBluetoothUnavailable** -
The bluetooth controller is not attached.

**WizardInternetBackendError** -
The internet request to the Flic backend failed.

**WizardInvalidData** -
According to the Flic backend, this Flic button supplied invalid identity data.

**WizardButtonBelongsToOtherPartner** -
The button belongs to another PbF partner.

**WizardButtonAlreadyConnectedToOtherDevice** -
The Flic 2 button is already connected to another device. Please disconnect it first so it becomes available.

Commands
--------
### CmdGetInfo
This command is used to retrieve current state about the server. After this command is sent, an EvtGetInfoResponse is sent back.

_uint8\_t_ **opcode**: 0


### CmdCreateScanner
Creates a scanner with the given scan\_id. For each advertisement packet received from a Flic button by the server, an EvtAdvertisementPacket will be sent with the given scan\_id until it is removed using CmdRemoveScanner. If there is already an active scanner with this scan\_id, this does nothing.

_uint8\_t_ **opcode**: 1

_uint32\_t_ **scan_id**:
A unique identifier that is sent together with advertisement packets. This identifier is also used to remove the scanner.


### CmdRemoveScanner
Removes the scanner with the given scan\_id. Once this is received by the server, it will no longer send out EvtAdvertisementPackets with this scan\_id.

_uint8\_t_ **opcode**: 2

_uint32\_t_ **scan_id**:
The same identifier that was used in CmdCreateScanner.


### CmdCreateConnectionChannel
Creates a connection channel for a Flic button with the given bluetooth address. You assign a unique conn\_id for this connection channel that will later be used in commands and events to refer to this connection channel. After this command is received by the server, an EvtCreateConnectionChannelResponse is sent. If there already exists a connection channel with this conn\_id, this does nothing.

_uint8\_t_ **opcode**: 3

_uint32\_t_ **conn_id**:
A unique identifier.

_bdaddr\_t_ **bd_addr**:
The address of the Flic to connect to.

_enum LatencyMode_ **latency_mode**:
Latency you are willing to accept for this connection channel.

_uint16\_t_ **auto_disconnect_time**:
Time in seconds after the Flic button may disconnect after the latest press or release. The button will reconnect automatically when it is later pressed again and deliver its enqueued events. Valid values are 0 - 511. 511 is used to disable this feature, i.e. the button will remain connected. If there are multiple connection channels for this button, the maximum value will be used.


### CmdRemoveConnectionChannel
Removes a connection channel previously created with CmdCreateConnectionChannel. After this is received by the server, this connection channel is removed and no further events will be sent for this channel. If there are no other connection channels active to this Flic button among any client, the physical bluetooth connection is disconnected.

_uint8\_t_ **opcode**: 4

_uint32\_t_ **conn_id**:
Connection channel identifier.

### CmdForceDisconnect
Removes all connection channels among all clients for the specified Flic button bluetooth address.

_uint8\_t_ **opcode**: 5

_bdaddr\_t_ **bd_addr**:
The address of the Flic to disconnect.

### CmdChangeModeParameters
Changes the accepted latency for this connection channel and the auto disconnect time. The latency mode will be applied immediately but the auto disconnect time will be applied the next time tme Flic is getting connected.

_uint8\_t_ **opcode**: 6

_uint32\_t_ **conn_id**:
Connection channel identifier.

_enum LatencyMode_ **latency_mode**:
The accepted latency mode.

_int16\_t_ **auto_disconnect_time**:
See under CmdCreateConnectionChannel.

### CmdPing
If you for various reasons would like to ping the server, send this command. An EvtPingResponse will be sent back in return with the same ping_id.

_uint8\_t_ **opcode**: 7

_uint32\_t_ **ping_id**:
An identifier that will be sent back in return.

### CmdGetButtonInfo
Get info about a verified button. An EvtGetButtonInfoResponse will be sent back immediately in return with the bd\_addr field set to the same value as in the request.

_uint8\_t_ **opcode**: 8

_bdaddr\_t_ **bd_addr**:
Bluetooth device address of the button to look up.

### CmdCreateScanWizard
Starts a scan wizard. If there already exists a scan wizard with the same id, this does nothing.

_uint8\_t_ **opcode**: 9

_uint32\_t_ **scan_wizard_id**:
A unique identifier.

### CmdCancelScanWizard
Cancels a scan wizard that was previously started. If there exists a scan wizard with this id, it is cancelled and an EvtScanWizardCompleted is sent with the reason set to WizardCancelledByUser.

_uint8\_t_ **opcode**: 10

_uint32\_t_ **scan_wizard_id**:
The identifier that was given when the scan wizard was started.

### CmdDeleteButton
Deletes a button. If the button exists in the list of verified buttons, all connection channels will be removed for all clients for this button. After that the EvtButtonDeleted event will be triggered for all clients. If the button does not exist in the list of verified buttons, the request has no effects but an EvtButtonDeleted will be triggered anyway for this client with the same address as in the request.

_uint8\_t_ **opcode**: 11

_bdaddr\_t_ **address**:
The Bluetooth device address of the button being deleted.

## CmdCreateBatteryStatusListener
Creates a battery status listener for a specific button. If the given listener\_id already exists for this client, this does nothing. Once created, an EvtBatteryStatus will always immediately be sent with the current battery status. Every time the battery status later updates, an EvtBatteryStatus will be sent. This will usually happen not more often than every three hours. Note that by just having a battery status listener doesn't mean flicd will automatically connect to a Flic button in order to get updates. At least one client needs a connection channel for the particular button to be able to get new updates.

_uint8\_t_ **opcode**: 12

_uint32\_t_ **listener_id**:
Listener identifier.

_bdaddr\_t_ **address**:
The Bluetooth device address of the button being monitored.

## CmdRemoveBatteryStatusListener
Removes a battery status listener.

_uint8\_t_ **opcode**: 13

_uint32\_t_ **listener_id**:
Listener identifier.


Events
------
### EvtAdvertisementPacket
For each scanner the client has created, this packet will be sent for each bluetooth advertisement packet arriving that comes from a Flic button. Usually the Flic button sends out many advertisement packets, with higher frequency if it was lately pressed.

_uint8\_t_ **opcode**: 0

_uint32\_t_ **scan_id**:
The scan id corresponding to the scanner which this advertisement packet belongs to.

_bdaddr\_t_ **bd_addr**:
The bluetooth address of this Flic button. Use it to establish a connection chnanel.

_uint8\_t_ **name_length**:
The length in bytes of the name following.

_char[16]_ **name**:
The first _name\_length_ bytes of this array contain the UTF-8 encoding of the advertised name. The other bytes will be zeros.

_int8\_t_ **rssi**:
Signal strength in dBm, between -126 and 20, where -127 is weakest and 20 is strongest. -127 means not available.

_bool_ **is_private**:
The Flic button is currently in private mode and won't accept connections from unbonded clients. Hold it down for 7 seconds while not attempting to connect to it to make it public. First then you may connect.

_bool_ **already_verified**:
If the server has the bonding key for this Flic button, this value is true. That means you should be able to connect to it.

_bool_ **already_connected_to_this_device**:
This Flic 2 button is already connected to this device.

_bool_ **already_connected_to_other_device**:
This Flic 2 button is already connected to another device.

### EvtCreateConnectionChannelResponse
This event will always be sent when a CmdCreateConnectionChannel is received, containing the status of the request.

_uint8\_t_ **opcode**: 1

_uint32\_t_ **conn_id**:
Connection channel identifier.

_enum CreateConnectionChannelError_ **error**:
Whether the request succeeded or not.

_enum ConnectionStatus_ **connection_status**:
The current connection status to this button. This might be a non-disconnected status if there are already other active connection channels to this button.

### EvtConnectionStatusChanged
This event is sent when the connection status is changed.

_uint8\_t_ **opcode**: 2

_uint32\_t_ **conn_id**:
Connection channel identifier.

_enum ConnectionStatus_ **connection_status**:
New connection status.

_enum DisconnectReason_ **disconnect_reason**:
If the connection status is Disconnected, this contains the reason. Otherwise this parameter is considered invalid.

### EvtConnectionChannelRemoved
This event is sent when a connection channel is removed. After this event is sent from the server, it will no longer send events corresponding to this connection channel. From this point, the conn\_id can now be reused when creating new connection channels. Note: If you got an EvtCreateConnectionChannelResponse with an error different than NoError, the connection channel have never been considered created, and this event will thus never be sent afterwards.

_uint8\_t_ **opcode**: 3

_uint32\_t_ **conn_id**:
Connection channel identifier.

_enum RemovedReason_ **removed_reason**:
Reason for this connection channel being removed.

### EvtButtonEvent
There are four types of button events. For each type of event, there is a different set of possible ClickTypes. Normally one application would handle one type of events and discard the others, depending on how many different triggers you would like the Flic button to be used for.

The following event types are defined:
 - **ButtonUpOrDown**: Possible ClickTypes are ButtonUp and ButtonDown. Used to simply know when the button was pressed or released.
 - **ButtonClickOrHold**: Possible ClickTypes are ButtonClick and ButtonHold. Used if you want to distinguish between click and hold.
 - **ButtonSingleOrDoubleClick**: Possible ClickTypes are ButtonSingleClick and ButtonDoubleClick. Used if you want to distinguish between a single click and a double click.
 - **ButtonSingleOrDoubleClickOrHold**: Possible ClickTypes are ButtonSingleClick, ButtonDoubleClick and ButtonHold. Used if you want to distinguish between a single click, a double click and a hold.

_uint8\_t_ **opcode**: 4, 5, 6 or 7 for the different types of event, in the same order as above.

_uint32\_t_ **conn_id**:
Connection channel identifier.

_enum ClickType_ **click_type**:
The click type. For each opcode, there are different possible values.

_bool_ **was_queued**:
If this button event happened during the button was disconnected or not.

_uint32\_t_ **time_diff**:
If this button event happened during the button was disconnected, this will be the number of seconds since that event happened (otherwise it will most likely be 0). Depending on your application, you might want to discard too old events.

### EvtNewVerifiedButton
This is sent to all clients when a button has been successfully verified that was not verified before (for the current bluetooth controller bluetooth address).
Note: The EvtConnectionStatusChanged with connection_status = Ready will be sent just before this event.

_uint8\_t_ **opcode**: 8

_bdaddr\_t_ **bd_addr**:
The bluetooth address for the verified Flic button.

### EvtGetInfoResponse
This is sent as a response to a CmdGetInfo.

_uint8\_t_ **opcode**: 9

_enum BluetoothControllerState_ **bluetooth_controller_state**:
Current state of the HCI connection to the bluetooth controller.

_bdaddr\_t_ **my_bd_addr**:
Current bluetooth address / identity of this device.

_enum BdAddrType_ **my_bd_addr_type**:
Current bluetooth address type of this device.

_uint8\_t_ **max_pending_connections**:
The max number of Flic buttons that can be monitored at the same time, regardless of having an established connection or not.

_int16\_t_ **max_concurrently_connected_buttons**:
The max number of Flic buttons that can have an established bluetooth connection at the same time. If this amount is reached, no other pending connection will succeed until another one has disconnected. This value will be -1 until the value becomes known. It becomes known first when the maximum number of connections is currently established and there is an attempt to establish yet another connection. Not all bluetooth controllers handle this correctly; some simply hides the fact that the maximum is reached and further connections won't succeed successfully, until a previously established connection is disconnected. Note: For some bluetooth controllers we have tested we have already hardcoded the correct value and this parameter will thus not be -1 but the correct one.

_uint8\_t_ **current_pending_connections**:
Current number of Flic buttons that are monitored by the server, among all clients.

_bool_ **currently_no_space_for_new_connection**:
The maximum number of concurrently connected buttons has been reached.

_uint16\_t_ **nb_verified_buttons**:
Number of verified buttons for this my_bd_addr/my_bd_addr_type pair.

_bdaddr\_t_[nb_verified_buttons] **bd_addr_of_verified_buttons**:
An array of all the verified buttons.

### EvtNoSpaceForNewConnection
Sent when the maximum number of connections has been reached (immediately after the EvtConnectionStatusChanged event). If the maximum number of connections is unknown, it is sent when the maximum number of connections are reached and an attempt is made to connect yet another button.

_uint8\_t_ **opcode**: 10

_uint8\_t_ **max_concurrently_connected_buttons**:
Same as in EvtGetInfoResponse.

### EvtGotSpaceForNewConnection
Sent when the maximum number of concurrent connections was reached but a button has now disconnected, making room for one new connection. Now a new connection attempt will automatically be made to devices having a connection channel open but has not yet established a connection.

_uint8\_t_ **opcode**: 11

_uint8\_t_ **max_concurrently_connected_buttons**:
Same as in EvtGetInfoResponse.

### EvtBluetoothControllerStateChange
See enum BluetoothControllerStateChange. If the bluetooth controller is detached, the scanners and connection channels set up by the user will maintain their state (but obviously no advertisement packet / connection state change / button events will be received). When the state is changed to Attached, internally all pending connections and scanners will be recreated as they were before the bluetooth controller was detached. Note: before sending the Detached state to the client, the server will first send EvtConnectionStatusChanged for each connected button with connection_status = Disconnected. Note: If the bluetooth controller sends a hardware error event, the state will transition directly from Attached to Resetting and if it was able to reset, back to Attached.

_uint8\_t_ **opcode**: 12

_enum BluetoothControllerState_ **state**:
The new state.

### EvtPingResponse
Sent in response to a CmdPing

_uint8\_t_ **opcode**: 13

_uint32\_t_ **ping_id**:
Same ping id as sent in the CmdPing.

### EvtGetButtonInfoResponse
Sent in return to a CmdGetButtonInfo. If the button was not verified, all parameters except bd_addr will contain zero-bytes.

_uint8\_t_ **opcode**: 14

_bdaddr\_t_ **bd_addr**:
The bluetooth device address of the request.

_uint8\_t[16]_ **uuid**:
The uuid of the button. Each button has a unique 128-bit identifier.

_uint8\_t_ **color_length**:
The length in bytes of the color following.

_char[16]_ **color**:
The first _color\_length_ bytes of this array contain the UTF-8 encoding of the color. The other bytes will be zeros.
Currently the following strings are defined: `black`, `white`, `turquoise`, `green` and `yellow` but more colors may be added later, so don't expect these are the only possible values.

_uint8\_t_ **serial_number_length**:
The length in bytes of the serial number following.

_char[16]_ **serial_number**:
The serial number of the button, in UTF-8 encoding. Only the first _serial\_number\_length_ bytes are used. The other bytes will be zeros.

### EvtScanWizardFoundPrivateButton
Sent once if a previously not verified private button is found during the scan. If this is received, tell the user to hold the button down for 7 seconds.

_uint8\_t_ **opcode**: 15

_uint32\_t_ **scan_wizard_id**:
Scan wizard id.

### EvtScanWizardFoundPublicButton
Sent once if a previously not verified public button is found during scan. Now the scan wizard stops scanning internally and instead initiates a connection to this button.

_uint8\_t_ **opcode**: 16

_uint32\_t_ **scan_wizard_id**:
Scan wizard id.

_bdaddr\_t_ **bd_addr**:
The bluetooth address of the Flic button that was found.

_uint8\_t_ **name_length**:
The length in bytes of the name following.

_char[16]_ **name**:
The first _name\_length_ bytes of this array contain the UTF-8 encoding of the advertised name. The other bytes will be zeros.

### EvtScanWizardButtonConnected
Sent when the found button connects for the first time. Now the verification and pairing process will begin.

_uint8\_t_ **opcode**: 17

_uint32\_t_ **scan_wizard_id**:
Scan wizard id.

### EvtScanWizardCompleted
Sent when the scan wizard has completed. See ScanWizardResult documentation for more information.

_uint8\_t_ **opcode**: 18

_uint32\_t_ **scan_wizard_id**:
Scan wizard id.

_enum ScanWizardResult_ **result**:
Result of the scan wizard.

### EvtButtonDeleted
Sent as a response to CmdDeleteButton or when a verified button has been deleted from the database.

_uint8\_t_ **opcode**: 19

_bdaddr\_t_ **bd_addr**:
The bluetooth device address of the deleted button.

_bool_ **deleted_by_this_client**:
Whether or not the client that initiated the deletion was the current client.

### EvtBatteryStatus
Sent to a battery status listener created by CmdCreateBatteryStatusListener in order to indicate the current battery status.

_uint8\_t_ **opcode**: 20

_uint32\_t_ **listener_id**:
Listener identifier.

_int8\_t_ **battery_percentage**:
A value between 0 and 100 that indicates the current battery status. The value can also be -1 if unknown.

_int64\_t_ **timestamp**:
UNIX timestamp (time in seconds since 1970-01-01T00:00:00Z, excluding leap seconds).
