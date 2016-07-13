import io.flic.fliclib.javaclient.*;
import io.flic.fliclib.javaclient.ScanWizard;
import io.flic.fliclib.javaclient.enums.*;

import java.io.IOException;

/**
 * Scan Wizard application.
 *
 * This program starts scanning of new Flic buttons that have not previously been verified by the server.
 * Once it finds a button that is in private mode, it shows a message that the user should hold it down for 7 seconds to make it public.
 * Once it finds a button that is in public mode, it attempts to connect to it.
 * If it could be successfully connected and verified, the bluetooth address is printed and the program exits.
 * It will automatically time out if it doesn't make any progress after a while.
 */
public class NewScanWizard {
    public static void main(String[] args) throws IOException {
        FlicClient client = new FlicClient("localhost");

        System.out.println("Welcome to Flic scan wizard!");
        System.out.println("Press a new Flic button you want to pair.");

        client.addScanWizard(new ScanWizard() {
            @Override
            public void onFoundPrivateButton() throws IOException {
                System.out.println("Found a private button. Please hold it down for 7 seconds to make it public.");
            }

            @Override
            public void onFoundPublicButton(Bdaddr bdaddr, String name) throws IOException {
                System.out.println("Found public button " + bdaddr + " (" + name + "). Now connecting...");
            }

            @Override
            public void onButtonConnected(Bdaddr bdaddr, String name) throws IOException {
                System.out.println("Connected. Now verifying and pairing...");
            }

            @Override
            public void onCompleted(ScanWizardResult result, Bdaddr bdaddr, String name) throws IOException {
                System.out.println("Completed with result " + result);
                if (result == ScanWizardResult.WizardSuccess) {
                    System.out.println("Your new button is: " + bdaddr);
                }
                client.close();
            }
        });

        client.handleEvents();
    }
}
