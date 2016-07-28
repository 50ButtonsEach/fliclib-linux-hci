using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using FliclibDotNetClient;

/*
 * Example client of FlicLib.
 * 
 * Consists of a GUI where a user can scan for new buttons as well as connect and see button up/down events.
 * 
 * Note that the Invoke((MethodInvoker) delegate { ... }) calls are made in order to run code on the UI thread which is needed to update the GUI.
 */

namespace FlicLibTest
{
    public partial class MainForm : Form
    {
        private FlicClient _flicClient;
        private ScanWizard _currentScanWizard;

        public MainForm()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            lblScanWizardStatus.Text = "";
        }

        private async void btnConnectDisconnect_Click(object sender, EventArgs e)
        {
            if (_flicClient == null)
            {
                btnConnectDisconnect.Enabled = false;
                try
                {
                    _flicClient = await FlicClient.CreateAsync(txtServer.Text);
                } catch (Exception ex)
                {
                    MessageBox.Show("Connect failed: " + ex.Message);
                    btnConnectDisconnect.Enabled = true;
                    return;
                }

                btnConnectDisconnect.Text = "Disconnect";
                btnConnectDisconnect.Enabled = true;

                btnAddNewFlic.Text = "Add new Flic";
                btnAddNewFlic.Enabled = true;

                _flicClient.BluetoothControllerStateChange += (o, args) => Invoke((MethodInvoker)delegate
                {
                    lblBluetoothStatus.Text = "Bluetooth controller status: " + args.State.ToString();
                });

                _flicClient.NewVerifiedButton += (o, args) => Invoke((MethodInvoker)delegate
                {
                    GotButton(args.BdAddr);
                });

                _flicClient.GetInfo((bluetoothControllerState, myBdAddr, myBdAddrType, maxPendingConnections, maxConcurrentlyConnectedButtons, currentPendingConnections, currentlyNoSpaceForNewConnection, verifiedButtons) => Invoke((MethodInvoker)delegate
                {
                    lblBluetoothStatus.Text = "Bluetooth controller status: " + bluetoothControllerState.ToString();

                    foreach (var bdAddr in verifiedButtons)
                    {
                        GotButton(bdAddr);
                    }
                }));

                await Task.Run(() => _flicClient.HandleEvents());

                // HandleEvents returns when the socket has disconnected for any reason

                buttonsList.Controls.Clear();
                btnAddNewFlic.Enabled = false;

                _flicClient = null;
                lblConnectionStatus.Text = "Connection status: Disconnected";
                lblBluetoothStatus.Text = "Bluetooth controller status:";
                btnConnectDisconnect.Text = "Connect";
                btnConnectDisconnect.Enabled = true;

                _currentScanWizard = null;
                lblScanWizardStatus.Text = "";
            }
            else
            {
                _flicClient.Disconnect();
                btnConnectDisconnect.Enabled = false;
            }
        }

        private void GotButton(Bdaddr bdAddr)
        {
            var control = new FlicButtonControl();
            control.lblBdAddr.Text = bdAddr.ToString();
            control.btnListen.Click += (o, args) =>
            {
                if (!control.Listens)
                {
                    control.Listens = true;
                    control.btnListen.Text = "Stop";

                    control.Channel = new ButtonConnectionChannel(bdAddr);
                    control.Channel.CreateConnectionChannelResponse += (sender1, eventArgs) => Invoke((MethodInvoker)delegate
                    {
                        if (eventArgs.Error != CreateConnectionChannelError.NoError)
                        {
                            control.Listens = false;
                            control.btnListen.Text = "Listen";
                        }
                        else
                        {
                            control.lblStatus.Text = eventArgs.ConnectionStatus.ToString();
                        }
                    });
                    control.Channel.Removed += (sender1, eventArgs) => Invoke((MethodInvoker)delegate
                    {
                        control.lblStatus.Text = "Disconnected";
                        control.Listens = false;
                        control.btnListen.Text = "Listen";
                    });
                    control.Channel.ConnectionStatusChanged += (sender1, eventArgs) => Invoke((MethodInvoker)delegate
                    {
                        control.lblStatus.Text = eventArgs.ConnectionStatus.ToString();
                    });
                    control.Channel.ButtonUpOrDown += (sender1, eventArgs) => Invoke((MethodInvoker)delegate
                    {
                        control.pictureBox.BackColor = eventArgs.ClickType == ClickType.ButtonDown ? Color.LimeGreen : Color.Red;
                    });
                    _flicClient.AddConnectionChannel(control.Channel);
                }
                else
                {
                    _flicClient.RemoveConnectionChannel(control.Channel);
                }
            };
            buttonsList.Controls.Add(control);
        }

        private void btnAddNewFlic_Click(object sender, EventArgs e)
        {
            if (_currentScanWizard == null)
            {
                lblScanWizardStatus.Text = "Press your Flic button";

                var scanWizard = new ScanWizard();
                scanWizard.FoundPrivateButton += (o, args) => Invoke((MethodInvoker)delegate
                {
                    lblScanWizardStatus.Text = "Hold down your Flic button for 7 seconds";
                });
                scanWizard.FoundPublicButton += (o, args) => Invoke((MethodInvoker)delegate
                {
                    lblScanWizardStatus.Text = "Found button " + args.BdAddr.ToString() + ", now connecting...";
                });
                scanWizard.ButtonConnected += (o, args) => Invoke((MethodInvoker)delegate
                {
                    lblScanWizardStatus.Text = "Connected to " + args.BdAddr.ToString() + ", now pairing...";
                });
                scanWizard.Completed += (o, args) => Invoke((MethodInvoker)delegate
                {
                    lblScanWizardStatus.Text = "Result: " + args.Result;
                    _currentScanWizard = null;
                    btnAddNewFlic.Text = "Add new Flic";
                });

                _flicClient.AddScanWizard(scanWizard);

                _currentScanWizard = scanWizard;
                btnAddNewFlic.Text = "Cancel";
            }
            else
            {
                _flicClient.CancelScanWizard(_currentScanWizard);
            }
        }
    }
}
