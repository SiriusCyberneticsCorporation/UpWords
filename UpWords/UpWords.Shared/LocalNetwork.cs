using System;
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
    public class LocalNetwork
    {
		public delegate void GamePostHandler(string ipAddress, string playerName);
		public event GamePostHandler OnGamePostReceived;

		private const string GAME_PORT = "4321";
		private const string BROADCAST_IP = "255.255.255.255";

		private bool m_started = false;
		private string m_username = string.Empty;
		private DatagramSocket m_broadcastSocket = new DatagramSocket();

		public enum eProtocol
		{
			GamePost,
		}
		public LocalNetwork()
		{
		}

		public async void Start()
		{
			if (!m_started)
			{
				m_username = await Windows.System.UserProfile.UserInformation.GetDisplayNameAsync();

				if (m_username.Length == 0)
				{
					m_username = CurrentMachineName();
				}

				m_broadcastSocket.MessageReceived += SocketOnMessageReceived;
				await m_broadcastSocket.BindServiceNameAsync(GAME_PORT);

				m_started = true;
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

		public void PostGame(string ipAddress, string playerName)
		{
			string message = eProtocol.GamePost.ToString() + "," + ipAddress + "," + playerName;

			RebroardcastGame(message);
		}

		private async void RebroardcastGame(string message)
		{
			SendMessage(BROADCAST_IP, message);

			await System.Threading.Tasks.Task.Delay(TimeSpan.FromSeconds(15));

			RebroardcastGame(message);
		}

		private async void SendMessage(string ipAddress, string message)
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
				//
			}
		}

		private async void SocketOnMessageReceived(DatagramSocket sender, DatagramSocketMessageReceivedEventArgs args)
		{
			IInputStream result = args.GetDataStream();
			Stream resultStream = result.AsStreamForRead(1024);

			using (StreamReader reader = new StreamReader(resultStream))
			{
				string message = await reader.ReadToEndAsync();

				await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync
					(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
					{
						DecodeMessage(message);
					});
			}
		}

		private void DecodeMessage(string message)
		{
			int firstCommaIndex = message.IndexOf(',');

			if (firstCommaIndex > 0)
			{
				string command = message.Substring(0, firstCommaIndex);
				string messageData = message.Substring(firstCommaIndex + 1);

				if (eProtocol.GamePost.ToString().Equals(command))
				{
					AddGame(messageData);
				}
			}
		}

		private void AddGame(string messageData)
		{
			int commaIndex = messageData.IndexOf(',');

			if (commaIndex > 0)
			{
				string ipAddress = messageData.Substring(0, commaIndex);
				string playerName = messageData.Substring(commaIndex + 1);

				if (ipAddress != CurrentIPAddress())
				{
					if(OnGamePostReceived != null)
					{
						OnGamePostReceived(ipAddress, playerName);
					}
				}
			}
		}
	}
}
