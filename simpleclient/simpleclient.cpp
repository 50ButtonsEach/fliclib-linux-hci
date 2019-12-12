#include <stdio.h>
#include <errno.h>
#include <unistd.h>
#include <stdlib.h>
#include <string.h>
#include <poll.h>
#include <stdint.h>
#include <assert.h>

#include <string>

#include <sys/param.h>
#include <sys/uio.h>
#include <sys/types.h>
#include <sys/ioctl.h>
#include <sys/socket.h>

#include <arpa/inet.h>
#include <netdb.h>

#include "client_protocol_packets.h"

using namespace std;
using namespace FlicClientProtocol;

static const char* CreateConnectionChannelErrorStrings[] = {
	"NoError",
	"MaxPendingConnectionsReached"
};

static const char* ConnectionStatusStrings[] = {
	"Disconnected",
	"Connected",
	"Ready"
};

static const char* DisconnectReasonStrings[] = {
	"Unspecified",
	"ConnectionEstablishmentFailed",
	"TimedOut",
	"BondingKeysMismatch"
};

static const char* RemovedReasonStrings[] = {
	"RemovedByThisClient",
	"ForceDisconnectedByThisClient",
	"ForceDisconnectedByOtherClient",
	
	"ButtonIsPrivate",
	"VerifyTimeout",
	"InternetBackendError",
	"InvalidData",
	
	"CouldntLoadDevice",
	
	"DeletedByThisClient",
	"DeletedByOtherClient",
	"ButtonBelongsToOtherPartner",
	"DeletedFromButton"
};

static const char* ClickTypeStrings[] = {
	"ButtonDown",
	"ButtonUp",
	"ButtonClick",
	"ButtonSingleClick",
	"ButtonDoubleClick",
	"ButtonHold"
};

static const char* BdAddrTypeStrings[] = {
	"PublicBdAddrType",
	"RandomBdAddrType"
};

static const char* LatencyModeStrings[] = {
	"NormalLatency",
	"LowLatency",
	"HighLatency"
};

static const char* ScanWizardResultStrings[] = {
	"WizardSuccess",
	"WizardCancelledByUser",
	"WizardFailedTimeout",
	"WizardButtonIsPrivate",
	"WizardBluetoothUnavailable",
	"WizardInternetBackendError",
	"WizardInvalidData",
	"WizardButtonBelongsToOtherPartner",
	"WizardButtonAlreadyConnectedToOtherDevice"
};

static const char* BluetoothControllerStateStrings[] = {
	"Detached",
	"Resetting",
	"Attached"
};

static uint8_t hex_digit_to_int(char hex) {
	if (hex >= '0' && hex <= '9')
		return hex - '0';
	if (hex >= 'a' && hex <= 'f')
		return hex - 'a' + 10;
	if (hex >= 'A' && hex <= 'F')
		return hex - 'A' + 10;
	return 0;
}

static uint8_t hex_to_byte(const char* hex) {
	return (hex_digit_to_int(hex[0]) << 4) | hex_digit_to_int(hex[1]);
}

static string bytes_to_hex_string(const uint8_t* data, int len) {
	string str(len * 2, '\0');
	for (int i = 0; i < len; i++) {
		static const char tbl[] = {'0', '1', '2', '3', '4', '5', '6', '7', '8', '9', 'a', 'b', 'c', 'd', 'e', 'f'};
		str[i * 2] = tbl[data[i] >> 4];
		str[i * 2 + 1] = tbl[data[i] & 0xf];
	}
	return str;
}

struct Bdaddr {
	uint8_t addr[6];
	
	Bdaddr() {
		memset(addr, 0, 6);
	}
	
	Bdaddr(const Bdaddr& o) {
		*this = o;
	}
	Bdaddr(const uint8_t* a) {
		*this = a;
	}
	Bdaddr(const char* a) {
		*this = a;
	}
	Bdaddr& operator=(const Bdaddr& o) {
		memcpy(addr, o.addr, 6);
		return *this;
	}
	Bdaddr& operator=(const uint8_t* a) {
		memcpy(addr, a, 6);
		return *this;
	}
	Bdaddr& operator=(const char* a) {
		for (int i = 0, pos = 15; i < 6; i++, pos -= 3) {
			addr[i] = hex_to_byte(&a[pos]);
		}
		return *this;
	}
	
	string to_string() const {
		string str;
		for (int i = 5; i >= 0; i--) {
			str += bytes_to_hex_string(addr + i, 1);
			if (i != 0) {
				str += ':';
			}
		}
		return str;
	}
	
	bool operator==(const Bdaddr& o) const { return memcmp(addr, o.addr, 6) == 0; }
	bool operator!=(const Bdaddr& o) const { return memcmp(addr, o.addr, 6) != 0; }
	bool operator<(const Bdaddr& o) const { return memcmp(addr, o.addr, 6) < 0; }
};

static void write_packet(int fd, void* buf, int len) {
	uint8_t new_buf[2 + len];
	new_buf[0] = len & 0xff;
	new_buf[1] = len >> 8;
	memcpy(new_buf + 2, buf, len);
	
	int pos = 0;
	int left = 2 + len;
	while(left) {
		int res = write(fd, new_buf + pos, left);
		if (res < 0) {
			if (errno == EINTR) {
				continue;
			}
			perror("write");
			exit(1);
		}
		pos += res;
		left -= res;
	}
}

static Bdaddr read_bdaddr() {
	char addr[32];
	scanf("%s", addr);
	if (strlen(addr) != 17) {
		fprintf(stderr, "Warning: Invalid length of bd addr\n");
	}
	return Bdaddr(addr);
}

static void print_help() {
	static const char help_text[] =
	"Available commands:\n"
	"getInfo - get various info about the server state and previously verified buttons\n"
	"startScanWizard - start scan wizard\n"
	"cancelScanWizard - cancel scan wizard\n"
	"startScan - start a raw scanning of Flic buttons\n"
	"stopScan - stop raw scanning\n"
	"connect xx:xx:xx:xx:xx:xx id - first parameter is the bluetooth address of the button, second is an integer identifier you set to identify this connection\n"
	"disconnect id - disconnect or abort pending connection\n"
	"changeModeParameters id latency_mode auto_disconnect_time - change latency mode (NormalLatency/LowLatency/HighLatency) and auto disconnect time for this connection\n"
	"forceDisconnect xx:xx:xx:xx:xx:xx - disconnect this button, even if other client program are connected\n"
	"getButtonInfo xx:xx:xx:xx:xx:xx - get button info for a verified button\n"
	"createBatteryStatusListener xx:xx:xx:xx:xx:xx id - first parameter is the bluetooth address of the button, second is an integer you set to identify this listener\n"
	"removeBatteryStatusListener id - removes a battery listener\n"
	"delete xx:xx:xx:xx:xx:xx - delete button\n"
	"help - prints this help text\n"
	"\n";
	fprintf(stderr, help_text);
}

int main(int argc, char* argv[]) {
	// Disable buffering for stdin to be able to select on both the server socket and stdin
	setvbuf(stdin, NULL, _IONBF, 0);
	
	if (argc < 2) {
		fprintf(stderr, "usage: %s host [port]\n", argv[0]);
		return 1;
	}
	int sockfd = socket(AF_INET, SOCK_STREAM, 0);
	if (sockfd < 0) {
		perror("socket");
		return 1;
	}
	struct hostent* server = gethostbyname(argv[1]);
	if (server == NULL) {
		fprintf(stderr, "ERROR, no such host\n");
		return 1;
	}
	struct sockaddr_in serv_addr;
	memset(&serv_addr, 0, sizeof(serv_addr));
	serv_addr.sin_family = AF_INET;
	memcpy(&serv_addr.sin_addr.s_addr, server->h_addr, server->h_length);
	serv_addr.sin_port = htons(argc >= 3 ? atoi(argv[2]) : 5551);
	
	if (connect(sockfd, (struct sockaddr*)&serv_addr, sizeof(serv_addr)) < 0) {
		perror("connect");
		return 1;
	}
	
	print_help();
	
	while(1) {
		unsigned char readbuf[65537];
		char cmd[128];
		
		fd_set fdread;
		FD_ZERO(&fdread);
		FD_SET(STDIN_FILENO, &fdread);
		FD_SET(sockfd, &fdread);
		
		while(true) {
			int select_res = select(max(STDIN_FILENO, sockfd) + 1, &fdread, NULL, NULL, NULL);
			if (select_res < 0) {
				if (errno == EINTR) {
					continue;
				} else {
					perror("select");
					return 1;
				}
			} else {
				break;
			}
		}
		if (FD_ISSET(STDIN_FILENO, &fdread)) {
			scanf("%s", cmd);
			if (strcmp("startScanWizard", cmd) == 0) {
				CmdCreateScanWizard cmd;
				cmd.opcode = CMD_CREATE_SCAN_WIZARD_OPCODE;
				cmd.scan_wizard_id = 0;
				write_packet(sockfd, &cmd, sizeof(cmd));
				
				printf("Please click and hold down your Flic button!\n");
			}
			if (strcmp("cancelScanWizard", cmd) == 0) {
				CmdCancelScanWizard cmd;
				cmd.opcode = CMD_CANCEL_SCAN_WIZARD_OPCODE;
				cmd.scan_wizard_id = 0;
				write_packet(sockfd, &cmd, sizeof(cmd));
			}
			if (strcmp("startScan", cmd) == 0) {
				CmdCreateScanner cmd;
				cmd.opcode = CMD_CREATE_SCANNER_OPCODE;
				cmd.scan_id = 0;
				write_packet(sockfd, &cmd, sizeof(cmd));
			}
			if (strcmp("stopScan", cmd) == 0) {
				CmdRemoveScanner cmd;
				cmd.opcode = CMD_REMOVE_SCANNER_OPCODE;
				cmd.scan_id = 0;
				write_packet(sockfd, &cmd, sizeof(cmd));
			}
			if (strcmp("connect", cmd) == 0) {
				CmdCreateConnectionChannel cmd;
				cmd.opcode = CMD_CREATE_CONNECTION_CHANNEL_OPCODE;
				memcpy(cmd.bd_addr, read_bdaddr().addr, 6);
				scanf("%u", &cmd.conn_id);
				cmd.latency_mode = NormalLatency;
				cmd.auto_disconnect_time = 0x1ff;
				write_packet(sockfd, &cmd, sizeof(cmd));
			}
			if (strcmp("disconnect", cmd) == 0) {
				CmdRemoveConnectionChannel cmd;
				cmd.opcode = CMD_REMOVE_CONNECTION_CHANNEL_OPCODE;
				scanf("%u", &cmd.conn_id);
				write_packet(sockfd, &cmd, sizeof(cmd));
			}
			if (strcmp("forceDisconnect", cmd) == 0) {
				CmdForceDisconnect cmd;
				cmd.opcode = CMD_FORCE_DISCONNECT_OPCODE;
				memcpy(cmd.bd_addr, read_bdaddr().addr, 6);
				write_packet(sockfd, &cmd, sizeof(cmd));
			}
			if (strcmp("changeModeParameters", cmd) == 0) {
				CmdChangeModeParameters cmd;
				cmd.opcode = CMD_CHANGE_MODE_PARAMETERS_OPCODE;
				scanf("%u", &cmd.conn_id);
				char latency_mode[32];
				uint32_t auto_disconnect_time;
				scanf("%s %u", latency_mode, &auto_disconnect_time);
				int mode = 0;
				for (int i = 0; i < 3; i++) {
					if (strcmp(latency_mode, LatencyModeStrings[i]) == 0) {
						mode = i;
					}
				}
				cmd.latency_mode = (enum LatencyMode)mode;
				cmd.auto_disconnect_time = auto_disconnect_time;
				write_packet(sockfd, &cmd, sizeof(cmd));
			}
			if (strcmp("getButtonInfo", cmd) == 0) {
				CmdGetButtonInfo cmd;
				cmd.opcode = CMD_GET_BUTTON_INFO_OPCODE;
				memcpy(cmd.bd_addr, read_bdaddr().addr, 6);
				write_packet(sockfd, &cmd, sizeof(cmd));
			}
			if (strcmp("getInfo", cmd) == 0) {
				CmdGetInfo cmd;
				cmd.opcode = CMD_GET_INFO_OPCODE;
				write_packet(sockfd, &cmd, sizeof(cmd));
			}
			if (strcmp("createBatteryStatusListener", cmd) == 0) {
				CmdCreateBatteryStatusListener cmd;
				cmd.opcode = CMD_CREATE_BATTERY_STATUS_LISTENER_OPCODE;
				memcpy(cmd.bd_addr, read_bdaddr().addr, 6);
				scanf("%u", &cmd.listener_id);
				write_packet(sockfd, &cmd, sizeof(cmd));
			}
			if (strcmp("removeBatteryStatusListener", cmd) == 0) {
				CmdRemoveBatteryStatusListener cmd;
				cmd.opcode = CMD_REMOVE_BATTERY_STATUS_LISTENER_OPCODE;
				scanf("%u", &cmd.listener_id);
				write_packet(sockfd, &cmd, sizeof(cmd));
			}
			if (strcmp("delete", cmd) == 0) {
				CmdDeleteButton cmd;
				cmd.opcode = CMD_DELETE_BUTTON_OPCODE;
				memcpy(cmd.bd_addr, read_bdaddr().addr, 6);
				write_packet(sockfd, &cmd, sizeof(cmd));
			}
			if (strcmp("help", cmd) == 0) {
				print_help();
			}
		}
		if (!FD_ISSET(sockfd, &fdread)) {
			continue;
		}
		int bytes_available;
		if (ioctl(sockfd, FIONREAD, &bytes_available) >= 0) {
			if (bytes_available == 0) {
				puts("server closed");
				return 0;
			}
			if (bytes_available < 2) {
				continue;
			}
			int nbytes = read(sockfd, readbuf, 2);
			if (nbytes < 0) {
				perror("read sockfd header");
				return 1;
			}
			int packet_len = readbuf[0] | (readbuf[1] << 8);
			int read_pos = 0;
			int bytes_left = packet_len;
			
			while (bytes_left > 0) {
				nbytes = read(sockfd, readbuf + read_pos, bytes_left);
				if (nbytes < 0) {
					perror("read sockfd data");
					return 1;
				}
				read_pos += nbytes;
				bytes_left -= nbytes;
			}
			
			void* pkt = (void*)readbuf;
			switch (readbuf[0]) {
				case EVT_ADVERTISEMENT_PACKET_OPCODE: {
					EvtAdvertisementPacket* evt = (EvtAdvertisementPacket*)pkt;
					printf("ADV: %s %s %d %s %s%s%s\n",
					       Bdaddr(evt->bd_addr).to_string().c_str(),
					       string(evt->name, (size_t)evt->name_length).c_str(),
					       evt->rssi,
					       (evt->is_private ? "private" : "public"),
					       (evt->already_verified ? "verified" : "unverified"),
					       (evt->already_connected_to_this_device ? " already connected to this device" : ""),
					       (evt->already_connected_to_other_device ? " already connected to other device" : "")
					);
					break;
				}
				case EVT_CREATE_CONNECTION_CHANNEL_RESPONSE_OPCODE: {
					EvtCreateConnectionChannelResponse* evt = (EvtCreateConnectionChannelResponse*)pkt;
					printf("Create conn: %d %s %s\n", evt->base.conn_id, CreateConnectionChannelErrorStrings[evt->error], ConnectionStatusStrings[evt->connection_status]);
					break;
				}
				case EVT_CONNECTION_STATUS_CHANGED_OPCODE: {
					EvtConnectionStatusChanged* evt = (EvtConnectionStatusChanged*)pkt;
					printf("Connection status changed: %d %s", evt->base.conn_id, ConnectionStatusStrings[evt->connection_status]);
					if (evt->connection_status == Disconnected) {
						printf(" %s\n", DisconnectReasonStrings[evt->disconnect_reason]);
					} else {
						printf("\n");
					}
					break;
				}
				case EVT_CONNECTION_CHANNEL_REMOVED_OPCODE: {
					EvtConnectionChannelRemoved* evt = (EvtConnectionChannelRemoved*)pkt;
					printf("Connection removed: %d %s\n", evt->base.conn_id, RemovedReasonStrings[evt->removed_reason]);
					break;
				}
				case EVT_BUTTON_UP_OR_DOWN_OPCODE:
				case EVT_BUTTON_CLICK_OR_HOLD_OPCODE:
				case EVT_BUTTON_SINGLE_OR_DOUBLE_CLICK_OPCODE:
				case EVT_BUTTON_SINGLE_OR_DOUBLE_CLICK_OR_HOLD_OPCODE: {
					EvtButtonEvent* evt = (EvtButtonEvent*)pkt;
					static const char* types[] = {"Button up/down", "Button click/hold", "Button single/double click", "Button single/double click/hold"};
					printf("%s: %d, %s, %s, %d seconds ago\n", types[readbuf[0]-EVT_BUTTON_UP_OR_DOWN_OPCODE], evt->base.conn_id, ClickTypeStrings[evt->click_type], (evt->was_queued ? "queued" : "not queued"), evt->time_diff);
					break;
				}
				case EVT_NEW_VERIFIED_BUTTON_OPCODE: {
					EvtNewVerifiedButton* evt = (EvtNewVerifiedButton*)pkt;
					printf("New verified button: %s\n", Bdaddr(evt->bd_addr).to_string().c_str());
					break;
				}
				case EVT_GET_INFO_RESPONSE_OPCODE: {
					EvtGetInfoResponse* evt = (EvtGetInfoResponse*)pkt;
					printf("Got info: %s, %s (%s), max pending connections: %d, max conns: %d, current pending conns: %d, currently no space: %c\n",
					       BluetoothControllerStateStrings[evt->bluetooth_controller_state],
					       Bdaddr(evt->my_bd_addr).to_string().c_str(),
					       BdAddrTypeStrings[evt->my_bd_addr_type],
					       evt->max_pending_connections,
					       evt->max_concurrently_connected_buttons,
					       evt->current_pending_connections,
					       evt->currently_no_space_for_new_connection ? 'y' : 'n');
					puts(evt->nb_verified_buttons > 0 ? "Verified buttons:" : "No verified buttons yet");
					for(int i = 0; i < evt->nb_verified_buttons; i++) {
						printf("%s\n", Bdaddr(evt->bd_addr_of_verified_buttons[i]).to_string().c_str());
					}
					break;
				}
				case EVT_NO_SPACE_FOR_NEW_CONNECTION_OPCODE: {
					EvtNoSpaceForNewConnection* evt = (EvtNoSpaceForNewConnection*)pkt;
					printf("No space for new connection, max: %d\n", evt->max_concurrently_connected_buttons);
					break;
				}
				case EVT_GOT_SPACE_FOR_NEW_CONNECTION_OPCODE: {
					EvtGotSpaceForNewConnection* evt = (EvtGotSpaceForNewConnection*)pkt;
					printf("Got space for new connection, max: %d\n", evt->max_concurrently_connected_buttons);
					break;
				}
				case EVT_BLUETOOTH_CONTROLLER_STATE_CHANGE_OPCODE: {
					EvtBluetoothControllerStateChange* evt = (EvtBluetoothControllerStateChange*)pkt;
					printf("Bluetooth state change: %d\n", evt->state);
					break;
				}
				case EVT_GET_BUTTON_INFO_RESPONSE_OPCODE: {
					EvtGetButtonInfoResponse* evt = (EvtGetButtonInfoResponse*)pkt;
					printf("Button info response: %s %s %s %s\n",
					       Bdaddr(evt->bd_addr).to_string().c_str(),
					       bytes_to_hex_string(evt->uuid, sizeof(evt->uuid)).c_str(),
					       string(evt->color, (size_t)evt->color_length).c_str(),
					       string(evt->serial_number, (size_t)evt->serial_number_length).c_str()
					);
					break;
				}
				case EVT_SCAN_WIZARD_FOUND_PRIVATE_BUTTON_OPCODE: {
					printf("Found private button. Please hold down it for 7 seconds to make it public.\n");
					break;
				}
				case EVT_SCAN_WIZARD_FOUND_PUBLIC_BUTTON_OPCODE: {
					EvtScanWizardFoundPublicButton* evt = (EvtScanWizardFoundPublicButton*)pkt;
					printf("Found public button %s %s, connecting...\n", Bdaddr(evt->bd_addr).to_string().c_str(), string(evt->name, (size_t)evt->name_length).c_str());
					break;
				}
				case EVT_SCAN_WIZARD_BUTTON_CONNECTED_OPCODE: {
					printf("Connected, now pairing and verifying...\n");
					break;
				}
				case EVT_SCAN_WIZARD_COMPLETED_OPCODE: {
					EvtScanWizardCompleted* evt = (EvtScanWizardCompleted*)pkt;
					printf("Scan wizard done with status %s\n", ScanWizardResultStrings[evt->result]);
					break;
				}
				case EVT_BATTERY_STATUS_OPCODE: {
					EvtBatteryStatus* evt = (EvtBatteryStatus*)pkt;
					printf("Battery status report for id %d, percentage: %d%%, timestamp: %s\n", evt->listener_id, evt->battery_percentage, ctime((time_t*)&evt->timestamp));
					break;
				}
				case EVT_BUTTON_DELETED_OPCODE: {
					EvtButtonDeleted* evt = (EvtButtonDeleted*)pkt;
					printf("Button %s deleted %s\n", Bdaddr(evt->bd_addr).to_string().c_str(), evt->deleted_by_this_client ? "by this client" : "not by this client");
					break;
				}
			}
		} else {
			perror("ioctl");
			return 1;
		}
	}
}
