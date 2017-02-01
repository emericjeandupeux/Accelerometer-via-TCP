using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
using Windows.Networking;
using Microsoft.Phone.Controls;
using Microsoft.Phone.Shell;
using Juandup.Resources;
using Windows.Phone.Devices.Notification;
using Microsoft.Phone.Net.NetworkInformation;


namespace Juandup
{
    public partial class MainPage : PhoneApplicationPage
    {
        // Constructeur
        public MainPage()
        {
            InitializeComponent();
            DeviceNetworkInformation.NetworkAvailabilityChanged += new EventHandler<NetworkNotificationEventArgs>(ChangeDetected);
            accel = Windows.Devices.Sensors.Accelerometer.GetDefault();
            Wifi_Click(null,null);
        }

        Windows.Devices.Sensors.Accelerometer accel;
        private Socket m_socket = null;
        public static ManualResetEvent _clientDone = new ManualResetEvent(false);

        private void BtnSend(object sender, RoutedEventArgs e)
        {
            Send(TbSend.Text + "\n");
            TbSend.Text = " ";
        }

        private void Wifi_Click(object sender, RoutedEventArgs e)
        {
            /*VibrationDevice vibr = VibrationDevice.GetDefault();
            vibr.Vibrate(new TimeSpan(0, 0, 0, 0, 200));*/
            NetworkInterfaceList interf = new NetworkInterfaceList();
            TextBluet.Text = "";
            //
            foreach (NetworkInterfaceInfo value in interf)
            {
                if (value.InterfaceType == NetworkInterfaceType.Wireless80211)
                {
                    if (value.InterfaceState == ConnectState.Connected) TextBluet.Text += "Connecté au réseau ";
                    TextBluet.Text += value.InterfaceName.ToString() + "\n"; //interf.Current.InterfaceName.ToString(); 
                    if (value.InterfaceName == TbWifiName.Text)
                    {
                        TextBluet.Text += "Bienvenue a la maison ! \n";
                        CbWifi.IsChecked = true;
                        var result = Connect(TbHost.Text.ToString(), 1234);
                        if (result != "Success")
                            TextBluet.Text += "Serveur TCP non disponible sur " + TbHost.Text.ToString() + "\n";
                    }
                }
            }
        }

        public void GetAndSendAccelero(Windows.Devices.Sensors.Accelerometer accel)
        {
            var read = accel.GetCurrentReading();
            var str = "IMU: " + read.Timestamp.TimeOfDay.ToString() + " " /*+ "X "*/ + read.AccelerationX.ToString("0.0") + " " +/* "Y " +*/ read.AccelerationY.ToString("0.0") + " "/* + "Z " */+ read.AccelerationZ.ToString("0.0") + "\n";
            Send(str);
        }

        private void accel_ReadingChanged(Windows.Devices.Sensors.Accelerometer sender, Windows.Devices.Sensors.AccelerometerReadingChangedEventArgs args)
        {
            GetAndSendAccelero(sender);
        }


        public void ChangeDetected(object sender, NetworkNotificationEventArgs e)
        {
            TextBluet.Text = e.NetworkInterface.InterfaceName.ToString();
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            Connect(TbHost.Text.ToString(), 1234);
        }

        public string Connect(string hostName, int portNumber)
        {
            string result = string.Empty;

            // Create DnsEndPoint. The hostName and port are passed in to this method.
            DnsEndPoint hostEntry = new DnsEndPoint(hostName, portNumber);

            // Create a stream-based, TCP socket using the InterNetwork Address Family. 
            m_socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            // Create a SocketAsyncEventArgs object to be used in the connection request
            SocketAsyncEventArgs socketEventArg = new SocketAsyncEventArgs();
            socketEventArg.RemoteEndPoint = hostEntry;

            // Inline event handler for the Completed event.
            // Note: This event handler was implemented inline in order to make this method self-contained.
            socketEventArg.Completed += new EventHandler<SocketAsyncEventArgs>(delegate (object s, SocketAsyncEventArgs e)
            {
                // Retrieve the result of this request
                result = e.SocketError.ToString();
                
                // Signal that the request is complete, unblocking the UI thread
                _clientDone.Set();
            });

            // Sets the state of the event to nonsignaled, causing threads to block
            _clientDone.Reset();
   
            // Make an asynchronous Connect request over the socket
            m_socket.ConnectAsync(socketEventArg);

            // Block the UI thread for a maximum of TIMEOUT_MILLISECONDS milliseconds.
            // If no response comes back within this time then proceed
            _clientDone.WaitOne(5000);

            return result;
        }

        public string Send(string data)
        {
            string response = "Operation Timeout";

            // We are re-using the _socket object initialized in the Connect method
            if (m_socket != null)
            {
                // Create SocketAsyncEventArgs context object
                SocketAsyncEventArgs socketEventArg = new SocketAsyncEventArgs();

                // Set properties on context object
                socketEventArg.RemoteEndPoint = m_socket.RemoteEndPoint;
                socketEventArg.UserToken = null;

                // Inline event handler for the Completed event.
                // Note: This event handler was implemented inline in order 
                // to make this method self-contained.
                socketEventArg.Completed += new EventHandler<SocketAsyncEventArgs>(delegate (object s, SocketAsyncEventArgs e)
                {
                    response = e.SocketError.ToString();

                    // Unblock the UI thread
                    _clientDone.Set();
                });

                // Add the data to be sent into the buffer
                byte[] payload = Encoding.UTF8.GetBytes(data);
                socketEventArg.SetBuffer(payload, 0, payload.Length);

                // Sets the state of the event to nonsignaled, causing threads to block
                _clientDone.Reset();

                // Make an asynchronous Send request over the socket
                m_socket.SendAsync(socketEventArg);

                // Block the UI thread for a maximum of TIMEOUT_MILLISECONDS milliseconds.
                // If no response comes back within this time then proceed
                _clientDone.WaitOne(1000);
            }
            else
            {
                response = "Socket is not initialized";
            }

            return response;
        }


        public string Receive()
        {
            string response = "Operation Timeout";

            // We are receiving over an established socket connection
            if (m_socket != null)
            {
                // Create SocketAsyncEventArgs context object
                SocketAsyncEventArgs socketEventArg = new SocketAsyncEventArgs();
                socketEventArg.RemoteEndPoint = m_socket.RemoteEndPoint;

                // Setup the buffer to receive the data
                socketEventArg.SetBuffer(new Byte[2048], 0, 2048);

                // Inline event handler for the Completed event.
                // Note: This even handler was implemented inline in order to make 
                // this method self-contained.
                socketEventArg.Completed += new EventHandler<SocketAsyncEventArgs>(delegate (object s, SocketAsyncEventArgs e)
                {
                    if (e.SocketError == SocketError.Success)
                    {
                        // Retrieve the data from the buffer
                        response = Encoding.UTF8.GetString(e.Buffer, e.Offset, e.BytesTransferred);
                        response = response.Trim('\0');
                    }
                    else
                    {
                        response = e.SocketError.ToString();
                    }

                    _clientDone.Set();
                });

                // Sets the state of the event to nonsignaled, causing threads to block
                _clientDone.Reset();

                // Make an asynchronous Receive request over the socket
                m_socket.ReceiveAsync(socketEventArg);

                // Block the UI thread for a maximum of TIMEOUT_MILLISECONDS milliseconds.
                // If no response comes back within this time then proceed
                _clientDone.WaitOne(1000);
            }
            else
            {
                response = "Socket is not initialized";
            }

            return response;
        }

        /// <summary>
        /// Closes the Socket connection and releases all associated resources
        /// </summary>
        public void Close()
        {
            if (m_socket != null)
            {
                m_socket.Close();
            }
        }

        private void AcceleroClick(object sender, RoutedEventArgs e)
        {
            Receive();
        }

        private void LaunchPT(object sender, RoutedEventArgs e)
        {
            Send("-POPCORNTIME");
        }

        private void LaunchD(object sender, RoutedEventArgs e)
        {
            Send("-DEEZER");
        }

        private void CbAccelero_Click(object sender, RoutedEventArgs e)
        {
            GetAndSendAccelero(accel);
            accel.ReportInterval = 30;
            if (CbAccelero.IsChecked.Value)
                accel.ReadingChanged += accel_ReadingChanged;
            else
                accel.ReadingChanged -= accel_ReadingChanged;
        }
    }
}