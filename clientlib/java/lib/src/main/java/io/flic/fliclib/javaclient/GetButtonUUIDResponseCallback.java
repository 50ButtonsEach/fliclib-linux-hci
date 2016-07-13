package io.flic.fliclib.javaclient;

/**
 * GetButtonUUIDResponseCallback.
 *
 * Used in {@link FlicClient#getButtonUUID(Bdaddr, GetButtonUUIDResponseCallback)}.
 */
public abstract class GetButtonUUIDResponseCallback {
    /**
     * Called upon response.
     *
     * @param bdaddr Bluetooth address
     * @param uuid Uuid of button, might be null if unknown
     */
    public abstract void onGetButtonUUIDResponse(Bdaddr bdaddr, String uuid);
}
