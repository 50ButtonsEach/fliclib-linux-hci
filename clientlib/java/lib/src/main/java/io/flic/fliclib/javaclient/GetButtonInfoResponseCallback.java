package io.flic.fliclib.javaclient;

/**
 * GetButtonInfoResponseCallback.
 *
 * Used in {@link FlicClient#getButtonInfo(Bdaddr, GetButtonInfoResponseCallback)}.
 */
public abstract class GetButtonInfoResponseCallback {
    /**
     * Called upon response.
     *
     * @param bdaddr Bluetooth address
     * @param uuid Uuid of button, might be null if unknown
     * @param color Color of button, might be null if unknown
     * @param serialNumber Serial number of the button, will be null if the button is not found
     * @param flicVersion Flic version (1 or 2)
     * @param firmwareVersion Firmware version
     */
    public abstract void onGetButtonInfoResponse(Bdaddr bdaddr, String uuid, String color, String serialNumber, int flicVersion, int firmwareVersion);
}
