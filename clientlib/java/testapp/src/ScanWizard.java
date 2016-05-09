import io.flic.fliclib.javaclient.*;
import io.flic.fliclib.javaclient.enums.ConnectionStatus;
import io.flic.fliclib.javaclient.enums.CreateConnectionChannelError;
import io.flic.fliclib.javaclient.enums.DisconnectReason;
import io.flic.fliclib.javaclient.enums.RemovedReason;

import java.io.IOException;

/**
 * Scan Wizard application.
 *
 * This program starts scanning of new Flic buttons that have not previously been verified by the server.
 * Once it finds a button that is in private mode, it shows a message that the user should hold it down for 7 seconds to make it public.
 * Once it finds a button that is in public mode, it attempts to connect to it.
 * If it could be successfully connected and verified, the bluetooth address is printed and the program exits.
 * If it could not be verified within 15 seconds, the scan is restarted.
 */
public class ScanWizard {
    private static FlicClient client;

    private static void done(Bdaddr bdaddr) throws IOException {
        System.out.println("Button " + bdaddr + " was successfully added!");
        client.close();
    }

    public static void main(String[] args) throws IOException {
        client = new FlicClient("localhost");

        System.out.println("Welcome to Flic scan wizard!");
        System.out.println("Press a new Flic button you want to pair.");

        client.addScanner(new ButtonScanner() {
            @Override
            public void onAdvertisementPacket(final Bdaddr bdaddr, String name, int rssi, boolean isPrivate, boolean alreadyVerified) throws IOException {
                final ButtonScanner thisButtonScanner = this;

                if (alreadyVerified) {
                    return;
                }
                if (isPrivate) {
                    System.out.println("Button " + bdaddr + " is currently private. Hold it down for 7 seconds to make it public.");
                } else {
                    System.out.println("Found public button " + bdaddr + ", now connecting...");

                    client.removeScanner(this);

                    client.addConnectionChannel(new ButtonConnectionChannel(bdaddr, new ButtonConnectionChannel.Callbacks() {
                        @Override
                        public void onCreateConnectionChannelResponse(final ButtonConnectionChannel channel, CreateConnectionChannelError createConnectionChannelError, ConnectionStatus connectionStatus) throws IOException {
                            if (connectionStatus == ConnectionStatus.Ready) {
                                done(bdaddr);
                            } else if (createConnectionChannelError != CreateConnectionChannelError.NoError) {
                                System.out.println("Failed: " + createConnectionChannelError);
                                restartScan();
                            } else {
                                client.setTimer(30 * 1000, new TimerTask() {
                                    @Override
                                    public void run() throws IOException {
                                        client.removeConnectionChannel(channel);
                                    }
                                });
                            }
                        }

                        @Override
                        public void onRemoved(ButtonConnectionChannel channel, RemovedReason removedReason) throws IOException {
                            System.out.println("Failed: " + removedReason);
                            restartScan();
                        }

                        @Override
                        public void onConnectionStatusChanged(ButtonConnectionChannel channel, ConnectionStatus connectionStatus, DisconnectReason disconnectReason) throws IOException {
                            if (connectionStatus == ConnectionStatus.Ready) {
                                done(bdaddr);
                            }
                        }

                        private void restartScan() throws IOException {
                            System.out.println("Restarting scan");
                            client.addScanner(thisButtonScanner);
                        }
                    }));
                }
            }
        });

        client.handleEvents();
    }
}
