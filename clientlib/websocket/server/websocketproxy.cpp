/*

WebSocket proxy

This program is a WebSocket proxy for the Flic Protocol.
All events and commands are sent as binary WebSocket messages, without the length prefix (since WebSocket frames already are message based and not stream based).
Each incoming WebSocket client becomes a separate Flic Protocol client.

*/

#include <cstdlib>
#include <cstring>
#include <cstdio>
#include <stdint.h>

#include <string>
#include <vector>

#include <pthread.h>
#include <unistd.h>
#include <errno.h>
#include <arpa/inet.h>
#include <netdb.h>

#include "sha1.h"

using namespace std;

string flic_hostname;
int flic_port;

struct SocketClosedException {};

size_t read_or_throw(int socket, uint8_t buf[], size_t len) {
	while (true) {
		int nread = read(socket, buf, len);
		if (nread < 0) {
			if (errno == EINTR) {
				continue;
			}
			throw SocketClosedException();
		}
		if (nread == 0) {
			throw SocketClosedException();
		}
		return nread;
	}
}

uint8_t read_byte(int socket) {
	uint8_t buf[1];
	read_or_throw(socket, buf, 1);
	return buf[0];
}

string read_line(int socket) {
	string line;
	while (true) {
		char c = read_byte(socket);
		if (c == '\n') {
			if (line.size() > 0 && line[line.size() - 1] == '\r') {
				line = line.substr(0, line.size() - 1);
			}
			return line;
		} else {
			line += c;
		}
	}
}

string string_to_lower(const string& str) {
	string copy = str;
	for (size_t i = 0; i < copy.size(); i++) {
		copy[i] = tolower(copy[i]);
	}
	return copy;
}

bool string_starts_with(const string& str, const string& prefix) {
	return string_to_lower(str).find(string_to_lower(prefix)) == 0;
}

void extract_header(const string& line, const string& header_name, string& output) {
	if (string_starts_with(line, header_name)) {
		size_t pos = header_name.size();
		for (; pos < line.size(); pos++) {
			if (line[pos] != ' ' && line[pos] != '\t') {
				break;
			}
		}
		output = line.substr(pos);
	}
}

string encode_base64(uint8_t hash[20]) {
	uint8_t copy[21];
	memcpy(copy, hash, 20);
	copy[20] = 0;
	
	string out;
	
	static const char chars[] = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/";
	for (int i = 0; i < 7; i++) {
		uint32_t a = (hash[i * 3] << 16) | (hash[i * 3 + 1] << 8) | hash[i * 3 + 2];
		
		out += chars[a >> 18];
		out += chars[(a >> 12) & 0x3f];
		out += chars[(a >> 6) & 0x3f];
		out += chars[a & 0x3f];
	}
	out[out.size() - 1] = '=';
	
	return out;
}

void write_or_throw(int socket, const uint8_t bytes[], size_t len) {
	size_t pos = 0;
	while (pos < len) {
		int res = write(socket, bytes + pos, len - pos);
		if (res < 0) {
			if (errno == EINTR) {
				continue;
			}
			throw SocketClosedException();
		}
		pos += res;
	}
}

void* client_function(void *thread_arg) {
	int clifd = (int)(long)thread_arg;
	int flicfd = -1;
	puts("client_function");
	
	try {
		string websocket_key;
		while (true) {
			string line = read_line(clifd);
			if (line.size() == 0) {
				break;
			}
			extract_header(line, "Sec-WebSocket-Key:", websocket_key);
		}
		puts("read request");
		
		if (websocket_key.empty()) {
			string response = "HTTP/1.1 404 Not Found\r\n"
			"Content-Type: text/html\r\n"
			"Connection: close\r\n"
			"Content-Length: 9\r\n"
			"\r\n"
			"Not Found";
			write_or_throw(clifd, (const uint8_t*)response.c_str(), response.size());
		} else {
			uint8_t hash[20];
			SHA1_CTX ctx;
			sha1_init(&ctx);
			sha1_update(&ctx, (const uint8_t*)websocket_key.c_str(), websocket_key.size());
			sha1_update(&ctx, (const uint8_t*)"258EAFA5-E914-47DA-95CA-C5AB0DC85B11", 36);
			sha1_final(&ctx, hash);
			
			string response = "HTTP/1.1 101 Switching Protocols\r\n"
			"Upgrade: websocket\r\n"
			"Connection: Upgrade\r\n"
			"Sec-WebSocket-Accept: " + encode_base64(hash) + "\r\n"
			"\r\n";
			write_or_throw(clifd, (const uint8_t*)response.c_str(), response.size());
			
			flicfd = socket(AF_INET, SOCK_STREAM, 0);
			if (flicfd < 0) {
				perror("socket");
				throw SocketClosedException();
			}
			
			struct hostent* server = gethostbyname(flic_hostname.c_str());
			if (server == NULL) {
				fprintf(stderr, "ERROR, no such host\n");
				throw SocketClosedException();
			}
			
			struct sockaddr_in serv_addr;
			memset(&serv_addr, 0, sizeof(serv_addr));
			serv_addr.sin_family = AF_INET;
			memcpy(&serv_addr.sin_addr.s_addr, server->h_addr, server->h_length);
			serv_addr.sin_port = htons(flic_port);
			
			if (connect(flicfd, (struct sockaddr*)&serv_addr, sizeof(serv_addr)) < 0) {
				perror("connect");
				throw SocketClosedException();
			}
			
			uint8_t readbuf[128];
			
			int ws_header_len = 0;
			int flic_header_len = 0;
			
			uint8_t ws_header[14];
			uint8_t flic_header[2];
			
			int saved_opcode = -1;
			vector<uint8_t> current_ws_packet;
			vector<uint8_t> current_ws_frame;
			
			size_t ws_payload_read = 0;
			size_t flic_payload_read = 0;
			
			fd_set fdread;
			FD_ZERO(&fdread);
			
			while (true) {
				FD_SET(clifd, &fdread);
				FD_SET(flicfd, &fdread);
				int maxfd = clifd;
				if (maxfd < flicfd) {
					maxfd = flicfd;
				}
				int select_res = select(maxfd + 1, &fdread, NULL, NULL, NULL);
				if (select_res < 0) {
					if (errno == EINTR) {
						continue;
					} else {
						throw SocketClosedException();
					}
				}
				if (FD_ISSET(clifd, &fdread)) {
					bool read_once = false;
					if (ws_header_len < 2) {
						ws_header_len += read_or_throw(clifd, ws_header + ws_header_len, 2 - ws_header_len);
						if (ws_header_len < 2) {
							continue;
						}
						read_once = true;
					}
					bool mask = ws_header[1] >> 7;
					size_t payload_len = ws_header[1] & 0x7f;
					size_t full_header_len = 2 + (payload_len == 126 ? 2 : payload_len == 127 ? 8 : 0) + (mask ? 4 : 0);
					if (ws_header_len < full_header_len) {
						if (read_once) {
							continue;
						}
						ws_header_len += read_or_throw(clifd, ws_header + ws_header_len, full_header_len - ws_header_len);
						if (ws_header_len < full_header_len) {
							continue;
						}
						read_once = true;
					}
					if (payload_len == 126) {
						payload_len = (ws_header[2] << 8) | ws_header[3];
					} else if (payload_len == 127) {
						// We don't support such large packets
						throw SocketClosedException();
					}
					if (ws_payload_read < payload_len) {
						if (read_once) {
							continue;
						}
						size_t nbytes = read_or_throw(clifd, readbuf, min((size_t)128, payload_len - ws_payload_read));
						if (mask) {
							for (size_t i = ws_payload_read; i < ws_payload_read + nbytes; i++) {
								readbuf[i] ^= ws_header[full_header_len - 4 + (i & 3)];
							}
						}
						current_ws_frame.insert(current_ws_frame.end(), readbuf, readbuf + nbytes);
						ws_payload_read += nbytes;
						if (ws_payload_read < payload_len) {
							continue;
						}
					}
					
					// Now we've got the full payload
					bool fin = ws_header[0] >> 7;
					int opcode = ws_header[0] & 0xf;
					if (opcode >= 0 && opcode < 8) {
						// Data
						puts("data frame");
						current_ws_packet.insert(current_ws_packet.end(), current_ws_frame.begin(), current_ws_frame.end());
						if (!fin) {
							if (opcode > 0) {
								saved_opcode = opcode;
							}
						} else {
							// Handle packet
							uint8_t outbuf[2];
							outbuf[0] = payload_len;
							outbuf[1] = payload_len >> 8;
							write_or_throw(flicfd, outbuf, 2);
							if (current_ws_packet.size() > 0) {
								write_or_throw(flicfd, &current_ws_packet[0], current_ws_packet.size());
							}
							
							// Clean up
							current_ws_packet.clear();
						}
					} else if (opcode == 0x8) {
						// Close
						throw SocketClosedException();
					} else if (opcode == 0x9) {
						// Ping
						uint8_t outbuf[2];
						outbuf[0] = (1 << 7) | 0xa;
						outbuf[1] = current_ws_frame.size() & 0x7f;
						write_or_throw(clifd, outbuf, 2);
						if (current_ws_frame.size() > 0) {
							write_or_throw(clifd, &current_ws_frame[0], outbuf[1]);
						}
					}
					current_ws_frame.clear();
					ws_header_len = 0;
					ws_payload_read = 0;
				}
				if (FD_ISSET(flicfd, &fdread)) {
					if (flic_header_len < 2) {
						flic_header_len += read_or_throw(flicfd, flic_header, 2 - flic_header_len);
						continue;
					}
					size_t payload_len = flic_header[0] | (flic_header[1] << 8);
					if (payload_len == 0) {
						flic_header_len = 0;
						continue;
					}
					
					size_t nbytes = read_or_throw(flicfd, readbuf, min((size_t)125, payload_len - flic_payload_read));
					bool has_all = flic_payload_read + nbytes == payload_len;
					uint8_t outbuf[2];
					outbuf[0] = (has_all << 7) | (flic_payload_read == 0 ? 0x2 : 0x0);
					outbuf[1] = nbytes;
					write_or_throw(clifd, outbuf, 2);
					write_or_throw(clifd, readbuf, nbytes);
					flic_payload_read += nbytes;
					
					if (has_all) {
						flic_header_len = 0;
						flic_payload_read = 0;
					}
				}
			}
		}
	} catch (SocketClosedException& e) {
	}
	close(clifd);
	if (flicfd != 1) {
		close(flicfd);
	}
	
	puts("client_function stopped");
}

int main(int argc, char* argv[]) {
	if (argc < 5) {
		fprintf(stderr, "Usage: %s flicd-host flicd-port webserver-bind-addr webserver-bind-port\n", argv[0]);
		fprintf(stderr, "Example 1: %s localhost 5551 127.0.0.1 5553\n", argv[0]);
		fprintf(stderr, "Example 2: %s localhost 5551 0.0.0.0 5553\n", argv[0]);
		return 1;
	}
	
	flic_hostname = argv[1];
	flic_port = atoi(argv[2]);
	string webserver_addr = argv[3];
	int webserver_port = atoi(argv[4]);
	
	
	int server_socket = socket(AF_INET, SOCK_STREAM, 0);
	if (server_socket < 0) {
		perror("open server socket");
		return 1;
	}
	sockaddr_in serv_addr;
	memset(&serv_addr, 0, sizeof(serv_addr));
	serv_addr.sin_family = AF_INET;
	serv_addr.sin_addr.s_addr = inet_addr(webserver_addr.c_str());
	serv_addr.sin_port = htons(webserver_port);
	int yes = 1;
	if (setsockopt(server_socket, SOL_SOCKET, SO_REUSEADDR, &yes, sizeof(yes)) < 0) {
		perror("setsockopt");
		return 1;
	}
	if (bind(server_socket, (sockaddr*)&serv_addr, sizeof(serv_addr))) {
		perror("bind server socket");
		return 1;
	}
	if (listen(server_socket, 100)) {
		perror("listen server socket");
		return 1;
	}
	
	while (true) {
		sockaddr_in cli_addr;
		socklen_t clilen = sizeof(cli_addr);
		puts("waiting for client");
		int clifd = accept(server_socket, (sockaddr*)&cli_addr, &clilen);
		puts("accept done");
		if (clifd < 0) {
			if (errno == EINTR) {
				continue;
			}
			perror("accept");
			return 1;
		}
		
		pthread_t thread_handle;
		pthread_create(&thread_handle, NULL, client_function, (void*)(long)clifd);
		puts("started thread");
	}
}