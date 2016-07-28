using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FliclibDotNetClient
{
    public enum CreateConnectionChannelError : byte
    {
        NoError,
        MaxPendingConnectionsReached
    };

    public enum ConnectionStatus : byte
    {
        Disconnected,
        Connected,
        Ready
    };

    public enum DisconnectReason : byte
    {
        Unspecified,
        ConnectionEstablishmentFailed,
        TimedOut,
        BondingKeysMismatch
    };

    public enum RemovedReason : byte
    {
        RemovedByThisClient,
        ForceDisconnectedByThisClient,
        ForceDisconnectedByOtherClient,

        ButtonIsPrivate,
        VerifyTimeout,
        InternetBackendError,
        InvalidData,

        CouldntLoadDevice
    };

    public enum ClickType : byte
    {
        ButtonDown,
        ButtonUp,
        ButtonClick,
        ButtonSingleClick,
        ButtonDoubleClick,
        ButtonHold
    };

    public enum BdAddrType : byte
    {
        PublicBdAddrType,
        RandomBdAddrType
    };

    public enum LatencyMode : byte
    {
        NormalLatency,
        LowLatency,
        HighLatency
    };

    public enum ScanWizardResult : byte
    {
        WizardSuccess,
        WizardCancelledByUser,
        WizardFailedTimeout,
        WizardButtonIsPrivate,
        WizardBluetoothUnavailable,
        WizardInternetBackendError,
        WizardInvalidData
    };

    public enum BluetoothControllerState : byte
    {
        Detached,
        Resetting,
        Attached
    };
}
