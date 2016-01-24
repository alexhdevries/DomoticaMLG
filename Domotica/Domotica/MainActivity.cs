﻿// Xamarin/C# app voor de besturing van een Arduino (Uno with Ethernet Shield) m.b.v. een socket-interface.
// Arduino server: DomoticaServer.ino
// De besturing heeft betrekking op het aan- en uitschakelen van een Arduino pin, 
// waar een led aan kan hangen of, t.b.v. het Domotica project, een RF-zender waarmee een 
// klik-aan-klik-uit apparaat bestuurd kan worden.
//
// De app heeft twee modes die betrekking hebben op de afhandeling van de socket-communicatie: "simple-mode" en "threaded-mode" 
// Wanneer het statement    //connector = new Connector(this);    wordt uitgecommentarieerd draait de app in "simple-mode",
// Het opvragen van gegevens van de Arduino (server) wordt dan met een Timer gerealisseerd. (De extra classes Connector.cs, 
// Receiver.cs en Sender.cs worden dan niet gebruikt.) 
// Als er een connector wordt aangemaakt draait de app in "threaded mode". De socket-communicatie wordt dan afgehandeld
// via een Sender- en een Receiver klasse, die worden aangemaakt in de Connector klasse. Deze threaded mode 
// biedt een generiekere en ook robuustere manier van communicatie, maar is ook moeilijker te begrijpen. 
// Aanbeveling: start in ieder geval met de simple-mode
//
// Werking: De communicatie met de (Arduino) server is gebaseerd op een socket-interface. Het IP- en Port-nummer
// is instelbaar. Na verbinding kunnen, middels een eenvoudig commando-protocol, opdrachten gegeven worden aan 
// de server (bijv. pin aan/uit). Indien de server om een response wordt gevraagd (bijv. led-status of een
// sensorwaarde), wordt deze in een 4-bytes ASCII-buffer ontvangen, en op het scherm geplaatst. Alle commando's naar 
// de server zijn gecodeerd met 1 char. Bestudeer het protocol in samenhang met de code van de Arduino server.
// Het default IP- en Port-nummer (zoals dat in het GUI verschijnt) kan aangepast worden in de file "Strings.xml". De
// ingestelde waarde is gebaseerd op je eigen netwerkomgeving, hier, en in de Arduino-code, is dat een router, die via DHCP
// in het segment 192.168.1.x vanaf systeemnummer 100 IP-adressen uitgeeft.
// 
// Resource files:
//   Main.axml (voor het grafisch design, in de map Resources->layout)
//   Strings.xml (voor alle statische strings in het interface, in de map Resources->values)
// 
// De software is verder gedocumenteerd in de code. Tijdens de colleges wordt er nadere uitleg over gegeven.
// 
// Versie 1.0, 12/12/2015
// D. Bruin
// S. Oosterhaven
// W. Dalof (voor de basis van het Threaded interface)
//
using System;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Timers;
using Android.App;
using Android.Content;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Android.OS;
using Android.Content.PM;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using Android.Graphics;
using System.Threading.Tasks;

namespace Domotica
{
    [Activity(Label = "@string/application_name", MainLauncher = true, Icon = "@drawable/icon", ScreenOrientation = Android.Content.PM.ScreenOrientation.Portrait)]

    public class MainActivity : Activity
    {
        // Variables (components/controls)
        // Controls on GUI
		Button buttonConnect, buttonSwitch1, buttonSwitch2;
        public Button kakuOne, kakuTwo, kakuThree;
        public TextView valOne, valTwo, valThree;
        public EditText thresholdOne, thresholdTwo, thresholdThree, thresholdFour;
        public Button toggleOne, toggleTwo, toggleThree;
        TextView textViewServerConnect, textViewTimerStateValue;
        //public TextView textViewChangePinStateValue, textViewSensorValue, textViewDebugValue;
        EditText editTextIPAddress, editTextIPPort;

        Timer timerClock, timerSockets;             // Timers   
        Socket socket = null;                       // Socket   
        Connector connector = null;                 // Connector (simple-mode or threaded-mode)
        List<Tuple<string, TextView>> commandList = new List<Tuple<string, TextView>>();  // List for commands and response places on UI
        public int listIndex = 0;
		int i = 0;
		int j = 0;
		int k = 0;
		int l = 0;

        protected override void OnCreate(Bundle bundle)
        {
            base.OnCreate(bundle);

            // Set our view from the "main" layout resource (strings are loaded from Recources -> values -> Strings.xml)
            SetContentView(Resource.Layout.Main);

            // find and set the controls, so it can be used in the code
            buttonConnect = FindViewById<Button>(Resource.Id.buttonConnect);
			buttonSwitch1 = FindViewById<Button> (Resource.Id.buttonSwitch1);
			buttonSwitch2 = FindViewById<Button> (Resource.Id.buttonSwitch2);
            kakuOne = FindViewById<Button>(Resource.Id.buttonOnOff1);
            kakuTwo = FindViewById<Button>(Resource.Id.buttonOnOff2);
            kakuThree = FindViewById<Button>(Resource.Id.buttonOnOff3);
            valOne = FindViewById<TextView>(Resource.Id.textViewValue1);
            valTwo = FindViewById<TextView>(Resource.Id.textViewValue2);
            valThree = FindViewById<TextView>(Resource.Id.textViewValue3);
            thresholdOne = FindViewById<EditText>(Resource.Id.editTextThreshold1);
            thresholdTwo = FindViewById<EditText>(Resource.Id.editTextThreshold2);
            thresholdThree = FindViewById<EditText>(Resource.Id.editTextThreshold3);
            thresholdFour = FindViewById<EditText>(Resource.Id.editTextThreshold4);
            toggleOne = FindViewById<Button>(Resource.Id.buttonToggle1);
            toggleTwo = FindViewById<Button>(Resource.Id.buttonToggle2);
            toggleThree = FindViewById<Button>(Resource.Id.buttonToggle3);
            textViewTimerStateValue = FindViewById<TextView>(Resource.Id.textViewTimerStateValue);
            textViewServerConnect = FindViewById<TextView>(Resource.Id.textViewServerConnect);
            editTextIPAddress = FindViewById<EditText>(Resource.Id.editTextIPAddress);
            editTextIPPort = FindViewById<EditText>(Resource.Id.editTextIPPort);
			kakuOne.SetTextColor (Color.Red);
			kakuTwo.SetTextColor (Color.Red);
			kakuThree.SetTextColor (Color.Red);
			toggleOne.SetTextColor (Color.Red);
			toggleTwo.SetTextColor (Color.Red);
			toggleThree.SetTextColor (Color.Red);
			buttonSwitch1.SetTextColor (Color.Red);
			buttonSwitch2.SetTextColor (Color.Red);

            UpdateConnectionState(4, "Disconnected");

            // Init commandlist, scheduled by socket timer
            commandList.Add(new Tuple<string, TextView>("a", valOne));
            commandList.Add(new Tuple<string, TextView>("b", valTwo));

            // activation of connector -> threaded sockets otherwise -> simple sockets 
            connector = new Connector(this);

            this.Title = (connector == null) ? this.Title + " (simple sockets)" : this.Title + " (thread sockets)";

            // timer object, running clock
            timerClock = new System.Timers.Timer() { Interval = 2000, Enabled = true }; // Interval >= 1000
            timerClock.Elapsed += (obj, args) =>
            {
                RunOnUiThread(() => { textViewTimerStateValue.Text = DateTime.Now.ToString("h:mm:ss"); });
				RunOnUiThread(() => { valThree.Text = DateTime.Now.ToString("HH:mm"); });
            };

            // timer object, check Arduino state
            // Only one command can be serviced in an timer tick, schedule from list
            timerSockets = new System.Timers.Timer() { Interval = 1000, Enabled = true }; // Interval >= 750
            timerSockets.Elapsed += (obj, args) =>
            { RunOnUiThread(
				() =>
                {
					if (connector.socket != null) // only if socket exists
                    {
                    // Send a command to the Arduino server on every tick (loop though list)
						UpdateValue();
						l++;
						if (l > 2)
						{
							l = 0;
						}
                    }
                });
            };

			 //Add the "Connect" button handler.
            if (buttonConnect != null)  // if button exists
            {
                buttonConnect.Click += (sender, e) =>
                {
                    //Validate the user input (IP address and port)
                    if (CheckValidIpAddress(editTextIPAddress.Text) && CheckValidPort(editTextIPPort.Text))
                    {
                        if (connector == null) // -> simple sockets
                        {
                            ConnectSocket(editTextIPAddress.Text, editTextIPPort.Text);
                        }
                        else // -> threaded sockets
                        {
                            //Stop the thread If the Connector thread is already started.
                            if (connector.CheckStarted()) connector.StopConnector();
                               connector.StartConnector(editTextIPAddress.Text, editTextIPPort.Text);
                        }
                    }
                    else UpdateConnectionState(3, "Please check IP");
                };
            }

            //Add the "Change pin state" button handler.
			if (buttonSwitch1 != null)
			{
				buttonSwitch1.Click += (sender, e) =>
				{
					if (toggleOne.CurrentTextColor == Color.Green)
					{
						if (connector.CheckStarted()) connector.SendMessage("1");
					}
					if (toggleTwo.CurrentTextColor == Color.Green)
					{
						if (connector.CheckStarted()) connector.SendMessage("2");
					}
					if (toggleThree.CurrentTextColor == Color.Green)
					{
						if (connector.CheckStarted()) connector.SendMessage("3");
					}
					if (connector == null) // -> simple sockets
					{
						socket.Send(Encoding.ASCII.GetBytes("c"));                 // Send toggle-command to the Arduino
					}
					else // -> threaded sockets
					{
						if (connector.CheckStarted()) connector.SendMessage("c");  // Send toggle-command to the Arduino
					}
				};
			}
			if (buttonSwitch2 != null)
			{
				buttonSwitch2.Click += (sender, e) =>
				{
					if (toggleOne.CurrentTextColor == Color.Green)
					{
						if (connector.CheckStarted()) connector.SendMessage("1");
					}
					if (toggleTwo.CurrentTextColor == Color.Green)
					{
						if (connector.CheckStarted()) connector.SendMessage("2");
					}
					if (toggleThree.CurrentTextColor == Color.Green)
					{
						if (connector.CheckStarted()) connector.SendMessage("3");
					}
					if (connector == null) // -> simple sockets
					{
						socket.Send(Encoding.ASCII.GetBytes("d"));                 // Send toggle-command to the Arduino
					}
					else // -> threaded sockets
					{
						if (connector.CheckStarted()) connector.SendMessage("d");  // Send toggle-command to the Arduino
					}
				};
			}
            if (kakuOne != null)
            {
                kakuOne.Click += (sender, e) =>
                {
                    if (connector == null) // -> simple sockets
                    {
                        socket.Send(Encoding.ASCII.GetBytes("1"));                 // Send toggle-command to the Arduino
						UpdateGUITime(kakuOne);
					}
                    else // -> threaded sockets
                    {
                        if (connector.CheckStarted()) connector.SendMessage("1");  // Send toggle-command to the Arduino
						UpdateGUITime(kakuOne);
                    }
                };
            }
            if (kakuTwo != null)
            {
                kakuTwo.Click += (sender, e) =>
                {
                    if (connector == null) // -> simple sockets
                    {
                        socket.Send(Encoding.ASCII.GetBytes("2"));                 // Send toggle-command to the Arduino
						UpdateGUITime(kakuTwo);
					}
                    else // -> threaded sockets
                    {
                        if (connector.CheckStarted()) connector.SendMessage("2");  // Send toggle-command to the Arduino
						UpdateGUITime(kakuTwo);
                    }
                };
            }
            if (kakuThree != null)
            {
                kakuThree.Click += (sender, e) =>
                {
                    if (connector == null) // -> simple sockets
                    {
                        socket.Send(Encoding.ASCII.GetBytes("3"));                 // Send toggle-command to the Arduino
						UpdateGUITime(kakuThree);
					}
                    else // -> threaded sockets
                    {
                        if (connector.CheckStarted()) connector.SendMessage("3");  // Send toggle-command to the Arduino
						UpdateGUITime(kakuThree);
                    }
                };
            }
            if (toggleOne != null)
            {
                toggleOne.Click += (sender, e) =>
                {
					UpdateGUITime(toggleOne);
                };
            }
            if (toggleTwo != null)
            {
                toggleTwo.Click += (sender, e) =>
                {
					UpdateGUITime(toggleTwo);
                };
            }

            if (toggleThree != null)
            {
                toggleThree.Click += (sender, e) =>
                {
                    UpdateGUITime(toggleThree);
                };
            }


		}

		public void UpdateValue()
		{
			if (l == 0)
			{
				if (toggleOne.CurrentTextColor != Color.Red)
				{
					int value = -1;
					int max = -1;

					bool successParse = Int32.TryParse (valOne.Text, out value) && Int32.TryParse(thresholdOne.Text, out max);

					if (!successParse) {
						return;
					}

					if (value > max) {
						if (connector.CheckStarted ())
							connector.SendMessage ("1");
						valOne.SetTextColor (Color.Green);
						i = 1;
					} else {
						if (connector.CheckStarted ())
							connector.SendMessage ("1");
						valOne.SetTextColor (Color.Red);
						i = 0;
					}
				}
			}
			if (l == 1)
			{
				if (toggleTwo.CurrentTextColor != Color.Red)
				{
					int value = -1;
					int max = -1;

					bool successParse = Int32.TryParse (valTwo.Text, out value) && Int32.TryParse(thresholdTwo.Text, out max);

					if (!successParse) {
						return;
					}

					if (value > max) {
						if (connector.CheckStarted ())
							connector.SendMessage ("2");
						valTwo.SetTextColor (Color.Green);
						i = 1;
					} else {
						if (connector.CheckStarted ())
							connector.SendMessage ("2");
						valTwo.SetTextColor (Color.Red);
						i = 0;
					}
				}
			}
			if (l == 2)
			{
				if (toggleThree.CurrentTextColor != Color.Red)
				{
					if (valThree.Text == thresholdThree.Text && k == 1)
					{
						k = 0;
						if (connector.CheckStarted ())
							connector.SendMessage ("3");
						valThree.SetTextColor (Color.Green);
					}
					if (valThree.Text == thresholdFour.Text && k == 0)
					{
						k = 1;
						if (connector.CheckStarted ())
							connector.SendMessage ("3");
						valThree.SetTextColor (Color.Red);
					}
				}
			}
		}

        //Send command to server and wait for response (blocking)
        //Method should only be called when socket existst
        public string executeCommand(string cmd)
        {
			buttonConnect.Enabled = false;
			buttonSwitch1.Enabled = false;
			buttonSwitch2.Enabled = false;
			kakuOne.Enabled = false;
			kakuTwo.Enabled = false;
			kakuThree.Enabled = false;
			toggleOne.Enabled = false;
			toggleTwo.Enabled = false;
			toggleThree.Enabled = false;
            byte[] buffer = new byte[4]; // response is always 4 bytes
            int bytesRead = 0;
            string result = "---";

            if (socket != null)
            {
                //Send command to server
                socket.Send(Encoding.ASCII.GetBytes(cmd));

                try //Get response from server
                {
                    //Store received bytes (always 4 bytes, ends with \n)
                    bytesRead = socket.Receive(buffer);  // If no data is available for reading, the Receive method will block until data is available,
                    //Read available bytes.              // socket.Available gets the amount of data that has been received from the network and is available to be read
                    while (socket.Available > 0) bytesRead = socket.Receive(buffer);
                    if (bytesRead == 4)
                        result = Encoding.ASCII.GetString(buffer, 0, bytesRead - 1); // skip \n
                    else result = "err";
                }
                catch (Exception exception) {
                    result = exception.ToString();
                    if (socket != null) {
                        socket.Close();
                        socket = null;
                    }
                    UpdateConnectionState(3, result);
                }
            }
			buttonConnect.Enabled = true;
			buttonSwitch1.Enabled = true;
			buttonSwitch2.Enabled = true;
			kakuOne.Enabled = true;
			kakuTwo.Enabled = true;
			kakuThree.Enabled = true;
			toggleOne.Enabled = true;
			toggleTwo.Enabled = true;
			toggleThree.Enabled = true;
            return result;
        }

        //Update connection state label (GUI).
        public void UpdateConnectionState(int state, string text)
        {
            // connectButton
            string butConText = "Connect";  // default text
            bool butConEnabled = true;      // default state
            Color color = Color.Red;        // default color
            // pinButton
            bool butPinEnabled = false;     // default state 

            //Set "Connect" button label according to connection state.
            if (state == 1)
            {
                butConText = "Please wait";
                color = Color.Orange;
                butConEnabled = false;
            } else
            if (state == 2)
            {
                butConText = "Disconnect";
                color = Color.Green;
                butPinEnabled = true;
            }
            //Edit the control's properties on the UI thread
            RunOnUiThread(() =>
            {
                textViewServerConnect.Text = text;
                if (butConText != null)  // text existst
                {
                    buttonConnect.Text = butConText;
                    textViewServerConnect.SetTextColor(color);
                    buttonConnect.Enabled = butConEnabled;
                }
                kakuOne.Enabled = butPinEnabled;
                kakuTwo.Enabled = butPinEnabled;
                kakuThree.Enabled = butPinEnabled;
				buttonSwitch1.Enabled = butPinEnabled;
				buttonSwitch2.Enabled = butPinEnabled;
				toggleOne.Enabled = butPinEnabled;
				toggleTwo.Enabled = butPinEnabled;
				toggleThree.Enabled = butPinEnabled;
            });
        }
		
        //Update GUI based on Arduino response
        public void UpdateGUI(string result, TextView textview)
        {
            RunOnUiThread(() =>
            {
                if (result == "OFF") textview.SetTextColor(Color.Red);
                else if (result == " ON") textview.SetTextColor(Color.Green);
                else textview.SetTextColor(Color.White);
            });
        }

        public void UpdateGUIStringOnly(string result, TextView textview)
        {
            RunOnUiThread(() =>
            {
                textview.Text = result;
            });
        }

        public void UpdateGUITime(TextView textview)
        {
            RunOnUiThread(() =>
            {
                if (textview.CurrentTextColor != Color.Red) textview.SetTextColor(Color.Red);
                else if (textview.CurrentTextColor != Color.Green) textview.SetTextColor(Color.Green);
            });
        }

        // Connect to socket ip/prt (simple sockets)
        public void ConnectSocket(string ip, string prt)
        {
            RunOnUiThread(() =>
            {
                if (socket == null)                                       // create new socket
                {
                    UpdateConnectionState(1, "Connecting...");
                    try  // to connect to the server (Arduino).
                    {
                        socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                        socket.Connect(new IPEndPoint(IPAddress.Parse(ip), Convert.ToInt32(prt)));
                        if (socket.Connected)
                        {
                            UpdateConnectionState(2, "Connected");
                            timerSockets.Enabled = true;                //Activate timer for communication with Arduino
							valTwo.SetTextColor(Color.Purple);
                        }
                    } catch (Exception exception) {
                        timerSockets.Enabled = false;
                        if (socket != null)
                        {
                            socket.Close();
                            socket = null;
                        }
                        UpdateConnectionState(4, exception.Message);
                    }
	            }
                else // disconnect socket
                {
                    socket.Close(); socket = null;
                    timerSockets.Enabled = false;
                    UpdateConnectionState(4, "Disconnected");
                }
            });
        }

        //Close the connection (stop the threads) if the application stops.
        protected override void OnStop()
        {
            base.OnStop();

            if (connector != null)
            {
                if (connector.CheckStarted())
                {
                    connector.StopConnector();
                }
            }
        }

        //Close the connection (stop the threads) if the application is destroyed.
        protected override void OnDestroy()
        {
            base.OnDestroy();

            if (connector != null)
            {
                if (connector.CheckStarted())
                {
                    connector.StopConnector();
                }
            }
        }

        //Prepare the Screen's standard options menu to be displayed.
        public override bool OnPrepareOptionsMenu(IMenu menu)
        {
            //Prevent menu items from being duplicated.
            menu.Clear();

            MenuInflater.Inflate(Resource.Menu.menu, menu);
            return base.OnPrepareOptionsMenu(menu);
        }

        //Executes an action when a menu button is pressed.
        public override bool OnOptionsItemSelected(IMenuItem item)
        {
            switch (item.ItemId)
            {
                case Resource.Id.exit:
                    //Force quit the application.
                    System.Environment.Exit(0);
                    return true;
                case Resource.Id.abort:

                    //Stop threads forcibly (for debugging only).
                    if (connector != null)
                    {
                        if (connector.CheckStarted()) connector.Abort();
                    }
                    return true;
            }
            return base.OnOptionsItemSelected(item);
        }

        //Check if the entered IP address is valid.
        private bool CheckValidIpAddress(string ip)
        {
            if (ip != "") {
                //Check user input against regex (check if IP address is not empty).
                Regex regex = new Regex("\\b((25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)(\\.|$)){4}\\b");
                Match match = regex.Match(ip);
                return match.Success;
            } else return false;
        }

        //Check if the entered port is valid.
        private bool CheckValidPort(string port)
        {
            //Check if a value is entered.
            if (port != "")
            {
                Regex regex = new Regex("[0-9]+");
                Match match = regex.Match(port);

                if (match.Success)
                {
                    int portAsInteger = Int32.Parse(port);
                    //Check if port is in range.
                    return ((portAsInteger >= 0) && (portAsInteger <= 65535));
                }
                else return false;
            } else return false;
        }
    }
}
