import io.flic.fliclib.javaclient.*;
import io.flic.fliclib.javaclient.enums.*;

import java.io.IOException;

/**
 * Test Client application.
 *
 * This program attempts to connect to all previously verified Flic buttons by this server.
 * Once connected, it prints Down and Up when a button is pressed or released.
 * It also monitors when new buttons are verified and connects to them as well. For example, run this program and at the same time the ScanWizard program.
 */
public class TestClient {

    private static ButtonConnectionChannel.Callbacks buttonCallbacks = new ButtonConnectionChannel.Callbacks() {
        @Override
        public void onCreateConnectionChannelResponse(ButtonConnectionChannel channel, CreateConnectionChannelError createConnectionChannelError, ConnectionStatus connectionStatus) {
            System.out.println("Create response " + channel.getBdaddr() + ": " + createConnectionChannelError + ", " + connectionStatus);
        }

        @Override
        public void onRemoved(ButtonConnectionChannel channel, RemovedReason removedReason) {
            System.out.println("Channel removed for " + channel.getBdaddr() + ": " + removedReason);
        }

        @Override
        public void onConnectionStatusChanged(ButtonConnectionChannel channel, ConnectionStatus connectionStatus, DisconnectReason disconnectReason) {
            System.out.println("New status for " + channel.getBdaddr() + ": " + connectionStatus + (connectionStatus == ConnectionStatus.Disconnected ? ", " + disconnectReason : ""));
        }

        @Override
        public void onButtonUpOrDown(ButtonConnectionChannel channel, ClickType clickType, boolean wasQueued, int timeDiff) throws IOException {
            System.out.println(channel.getBdaddr() + " " + (clickType == ClickType.ButtonUp ? "Up" : "Down"));
        }
    };

    public static void main(String[] args) throws IOException {
        final FlicClient client = new FlicClient("localhost");
        client.getInfo(new GetInfoResponseCallback() {
            @Override
            public void onGetInfoResponse(BluetoothControllerState bluetoothControllerState, Bdaddr myBdAddr,
                                          BdAddrType myBdAddrType, int maxPendingConnections, int maxConcurrentlyConnectedButtons,
                                          int currentPendingConnections, boolean currentlyNoSpaceForNewConnection, Bdaddr[] verifiedButtons) throws IOException {

                for (final Bdaddr bdaddr : verifiedButtons) {
                    client.addConnectionChannel(new ButtonConnectionChannel(bdaddr, buttonCallbacks));
                }
            }
        });
        client.setGeneralCallbacks(new GeneralCallbacks() {
            @Override
            public void onNewVerifiedButton(Bdaddr bdaddr) throws IOException {
                System.out.println("Another client added a new button: " + bdaddr + ". Now connecting to it...");
                client.addConnectionChannel(new ButtonConnectionChannel(bdaddr, buttonCallbacks));
            }
        });
        client.handleEvents();
    }
}
