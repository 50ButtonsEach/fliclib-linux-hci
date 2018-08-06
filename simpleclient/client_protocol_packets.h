/*
 * Specification of the protocol messages used in the Flic protocol.
 * 
 * Note: 16-bit little endian length header is prepended to each packet. The length of the length field itself is not included in the length.
 * 
 * Note: These structures are only valid on little endian platforms.
 * 
 */

#ifndef CLIENT_PROTOCOL_PACKETS_H
#define CLIENT_PROTOCOL_PACKETS_H

#include <stdint.h>

#define PACKED __attribute__((packed))

#ifdef __cplusplus
namespace FlicClientProtocol {
#endif

/// Enums

enum CreateConnectionChannelError {
	NoError,
	MaxPendingConnectionsReached
} PACKED;

enum ConnectionStatus {
	Disconnected,
	Connected,
	Ready
} PACKED;

enum DisconnectReason {
	Unspecified,
	ConnectionEstablishmentFailed,
	TimedOut,
	BondingKeysMismatch
} PACKED;

enum RemovedReason {
	RemovedByThisClient,
	ForceDisconnectedByThisClient,
	ForceDisconnectedByOtherClient,
	
	ButtonIsPrivate,
	VerifyTimeout,
	InternetBackendError,
	InvalidData,
	
	CouldntLoadDevice,
	
	DeletedByThisClient,
	DeletedByOtherClient,
	
	ButtonBelongsToOtherPartner
} PACKED;

enum ClickType {
	ButtonDown,
	ButtonUp,
	ButtonClick,
	ButtonSingleClick,
	ButtonDoubleClick,
	ButtonHold
} PACKED;

enum BdAddrType {
	PublicBdAddrType,
	RandomBdAddrType
} PACKED;

enum LatencyMode {
	NormalLatency,
	LowLatency,
	HighLatency
} PACKED;

enum ScanWizardResult {
	WizardSuccess,
	WizardCancelledByUser,
	WizardFailedTimeout,
	WizardButtonIsPrivate,
	WizardBluetoothUnavailable,
	WizardInternetBackendError,
	WizardInvalidData,
	WizardButtonBelongsToOtherPartner
} PACKED;

enum BluetoothControllerState {
	Detached,
	Resetting,
	Attached
} PACKED;

/// Commands

#define CMD_GET_INFO_OPCODE 0
typedef struct {
	uint8_t opcode;
} PACKED CmdGetInfo;

#define CMD_CREATE_SCANNER_OPCODE 1
typedef struct {
	uint8_t opcode;
	uint32_t scan_id;
} PACKED CmdCreateScanner;

#define CMD_REMOVE_SCANNER_OPCODE 2
typedef struct {
	uint8_t opcode;
	uint32_t scan_id;
} PACKED CmdRemoveScanner;

#define CMD_CREATE_CONNECTION_CHANNEL_OPCODE 3
typedef struct {
	uint8_t opcode;
	uint32_t conn_id;
	uint8_t bd_addr[6];
	enum LatencyMode latency_mode;
	int16_t auto_disconnect_time;
} PACKED CmdCreateConnectionChannel;

#define CMD_REMOVE_CONNECTION_CHANNEL_OPCODE 4
typedef struct {
	uint8_t opcode;
	uint32_t conn_id;
} PACKED CmdRemoveConnectionChannel;

#define CMD_FORCE_DISCONNECT_OPCODE 5
typedef struct {
	uint8_t opcode;
	uint8_t bd_addr[6];
} PACKED CmdForceDisconnect;

#define CMD_CHANGE_MODE_PARAMETERS_OPCODE 6
typedef struct {
	uint8_t opcode;
	uint32_t conn_id;
	enum LatencyMode latency_mode;
	int16_t auto_disconnect_time;
} PACKED CmdChangeModeParameters;

#define CMD_PING_OPCODE 7
typedef struct {
	uint8_t opcode;
	uint32_t ping_id;
} PACKED CmdPing;

#define CMD_GET_BUTTON_INFO_OPCODE 8
typedef struct {
	uint8_t opcode;
	uint8_t bd_addr[6];
} PACKED CmdGetButtonInfo;

#define CMD_CREATE_SCAN_WIZARD_OPCODE 9
typedef struct {
	uint8_t opcode;
	uint32_t scan_wizard_id;
} PACKED CmdCreateScanWizard;

#define CMD_CANCEL_SCAN_WIZARD_OPCODE 10
typedef struct {
	uint8_t opcode;
	uint32_t scan_wizard_id;
} PACKED CmdCancelScanWizard;

#define CMD_DELETE_BUTTON_OPCODE 11
typedef struct {
	uint8_t opcode;
	uint8_t bd_addr[6];
} PACKED CmdDeleteButton;

#define CMD_CREATE_BATTERY_STATUS_LISTENER_OPCODE 12
typedef struct {
	uint8_t opcode;
	uint32_t listener_id;
	uint8_t bd_addr[6];
} PACKED CmdCreateBatteryStatusListener;

#define CMD_REMOVE_BATTERY_STATUS_LISTENER_OPCODE 13
typedef struct {
	uint8_t opcode;
	uint32_t listener_id;
} PACKED CmdRemoveBatteryStatusListener;

/// Events

#define EVT_ADVERTISEMENT_PACKET_OPCODE 0
typedef struct {
	uint8_t opcode;
	uint32_t scan_id;
	uint8_t bd_addr[6];
	uint8_t name_length;
	char name[16];
	int8_t rssi;
	int8_t is_private;
	int8_t already_verified;
} PACKED EvtAdvertisementPacket;

typedef struct {
	uint8_t opcode;
	uint32_t conn_id;
} PACKED ConnectionEventBase;

#define EVT_CREATE_CONNECTION_CHANNEL_RESPONSE_OPCODE 1
typedef struct {
	ConnectionEventBase base;
	enum CreateConnectionChannelError error;
	enum ConnectionStatus connection_status;
} PACKED EvtCreateConnectionChannelResponse;

#define EVT_CONNECTION_STATUS_CHANGED_OPCODE 2
typedef struct {
	ConnectionEventBase base;
	enum ConnectionStatus connection_status;
	enum DisconnectReason disconnect_reason;
} PACKED EvtConnectionStatusChanged;

#define EVT_CONNECTION_CHANNEL_REMOVED_OPCODE 3
typedef struct {
	ConnectionEventBase base;
	enum RemovedReason removed_reason;
} PACKED EvtConnectionChannelRemoved;

#define EVT_BUTTON_UP_OR_DOWN_OPCODE 4
#define EVT_BUTTON_CLICK_OR_HOLD_OPCODE 5
#define EVT_BUTTON_SINGLE_OR_DOUBLE_CLICK_OPCODE 6
#define EVT_BUTTON_SINGLE_OR_DOUBLE_CLICK_OR_HOLD_OPCODE 7
typedef struct {
	ConnectionEventBase base;
	enum ClickType click_type;
	uint8_t was_queued;
	uint32_t time_diff;
} PACKED EvtButtonEvent;

#define EVT_NEW_VERIFIED_BUTTON_OPCODE 8
typedef struct {
	uint8_t opcode;
	uint8_t bd_addr[6];
} PACKED EvtNewVerifiedButton;

#define EVT_GET_INFO_RESPONSE_OPCODE 9
typedef struct {
	uint8_t opcode;
	enum BluetoothControllerState bluetooth_controller_state;
	uint8_t my_bd_addr[6];
	enum BdAddrType my_bd_addr_type;
	uint8_t max_pending_connections;
	int16_t max_concurrently_connected_buttons;
	uint8_t current_pending_connections;
	uint8_t currently_no_space_for_new_connection;
	uint16_t nb_verified_buttons;
	uint8_t bd_addr_of_verified_buttons[0][6];
} PACKED EvtGetInfoResponse;

#define EVT_NO_SPACE_FOR_NEW_CONNECTION_OPCODE 10
typedef struct {
	uint8_t opcode;
	uint8_t max_concurrently_connected_buttons;
} PACKED EvtNoSpaceForNewConnection;

#define EVT_GOT_SPACE_FOR_NEW_CONNECTION_OPCODE 11
typedef struct {
	uint8_t opcode;
	uint8_t max_concurrently_connected_buttons;
} PACKED EvtGotSpaceForNewConnection;

#define EVT_BLUETOOTH_CONTROLLER_STATE_CHANGE_OPCODE 12
typedef struct {
	uint8_t opcode;
	enum BluetoothControllerState state;
} PACKED EvtBluetoothControllerStateChange;

#define EVT_PING_RESPONSE_OPCODE 13
typedef struct {
	uint8_t opcode;
	uint32_t ping_id;
} PACKED EvtPingResponse;

#define EVT_GET_BUTTON_INFO_RESPONSE_OPCODE 14
typedef struct {
	uint8_t opcode;
	uint8_t bd_addr[6];
	uint8_t uuid[16];
	uint8_t color_length;
	char color[16];
} PACKED EvtGetButtonInfoResponse;

typedef struct {
	uint8_t opcode;
	uint32_t scan_wizard_id;
} PACKED EvtScanWizardBase;

#define EVT_SCAN_WIZARD_FOUND_PRIVATE_BUTTON_OPCODE 15
typedef struct {
	EvtScanWizardBase base;
} PACKED EvtScanWizardFoundPrivateButton;

#define EVT_SCAN_WIZARD_FOUND_PUBLIC_BUTTON_OPCODE 16
typedef struct {
	EvtScanWizardBase base;
	uint8_t bd_addr[6];
	uint8_t name_length;
	char name[16];
} PACKED EvtScanWizardFoundPublicButton;

#define EVT_SCAN_WIZARD_BUTTON_CONNECTED_OPCODE 17
typedef struct {
	EvtScanWizardBase base;
} PACKED EvtScanWizardButtonConnected;

#define EVT_SCAN_WIZARD_COMPLETED_OPCODE 18
typedef struct {
	EvtScanWizardBase base;
	enum ScanWizardResult result;
} PACKED EvtScanWizardCompleted;

#define EVT_BUTTON_DELETED_OPCODE 19
typedef struct {
	uint8_t opcode;
	uint8_t bd_addr[6];
	uint8_t deleted_by_this_client;
} PACKED EvtButtonDeleted;

#define EVT_BATTERY_STATUS_OPCODE 20
typedef struct {
	uint8_t opcode;
	uint32_t listener_id;
	int8_t battery_percentage;
	int64_t timestamp;
} PACKED EvtBatteryStatus;

#ifdef __cplusplus
}
#endif

#endif
