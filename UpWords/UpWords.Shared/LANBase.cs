﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Windows.Networking;
using Windows.Networking.Connectivity;
using Windows.Networking.Sockets;
using Windows.Storage.Streams;

namespace UpWords
{
    public abstract class LANBase
    {
		public delegate void ErrorMessageHandler(string message);
		public event ErrorMessageHandler OnErrorMessage;

		public string IpAddress { get { return m_ipAddress; } }
		public string MachineName { get { return m_machineName; } }
		public string Username { get { return m_username; } }

		private const string GAME_PORT = "4321";
		private const string BROADCAST_IP = "255.255.255.255";

		private bool m_started = false;
		private string m_username = string.Empty;
		private string m_ipAddress = string.Empty;
		private string m_machineName = string.Empty;
		private DatagramSocket m_broadcastSocket = new DatagramSocket();

		protected abstract void DecodeMessage(HostName remoteAddress, string message);

		public async void Start()
		{
			if (!m_started)
			{
				try
				{
					m_username = await Windows.System.UserProfile.UserInformation.GetDisplayNameAsync();

					if (m_username.Length == 0)
					{
						m_username = CurrentMachineName();
					}

					m_ipAddress = CurrentIPAddress();
					m_machineName = CurrentMachineName();

					m_broadcastSocket.MessageReceived += SocketOnMessageReceived;
					await m_broadcastSocket.BindServiceNameAsync(GAME_PORT);
				}
				catch (Exception ex)
				{
					PostMessage(ex.Message);
				}
				m_started = true;
			}
		}

		public void BroadcastMessage(string message)
		{
			SendMessage(BROADCAST_IP, message);
		}

		public async void SendMessage(string ipAddress, string message)
		{
			try
			{
				using (IOutputStream stream = await m_broadcastSocket.GetOutputStreamAsync(new HostName(ipAddress), GAME_PORT))
				{
					using (DataWriter writer = new DataWriter(stream))
					{
						var data = System.Text.Encoding.UTF8.GetBytes(message);

						writer.WriteBytes(data);
						await writer.StoreAsync();
					}
				}
			}
			catch (Exception ex)
			{
				PostMessage(ex.Message);
			}
		}

		private async void SocketOnMessageReceived(DatagramSocket sender, DatagramSocketMessageReceivedEventArgs args)
		{
			try
			{
				IInputStream result = args.GetDataStream();
				Stream resultStream = result.AsStreamForRead(1024);				
				
				using (StreamReader reader = new StreamReader(resultStream))
				{
					string message = reader.ReadToEnd();

					await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync
						(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
						{
							DecodeMessage(args.RemoteAddress, message);
						});
				}
			}
			catch (Exception ex)
			{
				PostMessage(ex.Message);
			}
		}

		public string CurrentIPAddress()
		{
			var icp = NetworkInformation.GetInternetConnectionProfile();

			if (icp != null && icp.NetworkAdapter != null)
			{
				var hostname =
					NetworkInformation.GetHostNames()
						.SingleOrDefault(
							hn =>
							hn.IPInformation != null && hn.IPInformation.NetworkAdapter != null
							&& hn.IPInformation.NetworkAdapter.NetworkAdapterId
							== icp.NetworkAdapter.NetworkAdapterId);

				if (hostname != null)
				{
					// the ip address
					return hostname.CanonicalName;
				}
			}

			return string.Empty;
		}

		public string CurrentMachineName()
		{
			var hostNames = NetworkInformation.GetHostNames();

			var localName = hostNames.FirstOrDefault(name => name.DisplayName.Contains(".local"));

			return localName.DisplayName.Replace(".local", "");
		}

		internal void PostMessage(string message)
		{
			if (OnErrorMessage != null)
			{
				OnErrorMessage(message);
			}
		}
	}
}