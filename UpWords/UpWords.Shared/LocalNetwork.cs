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
		public delegate void ErrorMessageHandler(string message);
		public event ErrorMessageHandler OnErrorMessage;

		public delegate void GamePostHandler(GameDetails gameInformation);
		public event GamePostHandler OnGameCreatedReceived;
		public event GamePostHandler OnGameCancelledReceived;
		public event GamePostHandler OnGameStartedReceived;

		public delegate void GameJoinedHandler(string playersIpAddress, string playersDetails);
		public event GameJoinedHandler OnGameJoinedReceived;

		public string IpAddress { get { return m_ipAddress; } }
		public string MachineName { get { return m_machineName; } }
		public string Username { get { return m_username; } }

		private const string GAME_PORT = "4321";
		private const string BROADCAST_IP = "255.255.255.255";

		private bool m_started = false;
		private bool m_continueRebroadcast = false;
		private string m_username = string.Empty;
		private string m_ipAddress = string.Empty;
		private string m_machineName = string.Empty;
		private DatagramSocket m_broadcastSocket = new DatagramSocket();

		public enum eProtocol
		{
			GameCreated,
			GameCancelled,
			GameJoined,
			GameStarted,
		}

		public LocalNetwork()
		{
		}

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
					if (OnErrorMessage != null)
					{
						OnErrorMessage(ex.Message);
					}
				}
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

		public void CreateGame(string gameTitle)
		{
			string message = eProtocol.GameCreated.ToString() + "," + m_ipAddress + "," + gameTitle;

			m_continueRebroadcast = true;

			RebroardcastGame(message);
		}

		private async void RebroardcastGame(string message)
		{
			SendMessage(BROADCAST_IP, message);

			await System.Threading.Tasks.Task.Delay(TimeSpan.FromSeconds(15));

			if (m_continueRebroadcast)
			{
				RebroardcastGame(message);
			}
		}

		public void StartGame(string gameTitle)
		{
			string message = eProtocol.GameStarted.ToString() + "," + m_ipAddress + "," + gameTitle;
			
			m_continueRebroadcast = false;
			
			SendMessage(BROADCAST_IP, message);
		}

		public void JoinGame(GameDetails gameInformation)
		{
			string message = eProtocol.GameJoined.ToString() + "," + m_ipAddress + "," + m_username + " on " + m_machineName;;

			SendMessage(gameInformation.CreatorsIpAddress, message);
		}

		public void CancelGame(string gameTitle)
		{
			string message = eProtocol.GameCancelled.ToString() + "," + m_ipAddress + "," + gameTitle;

			m_continueRebroadcast = false;

			SendMessage(BROADCAST_IP, message);
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
				if (OnErrorMessage != null)
				{
					OnErrorMessage(ex.Message);
				}
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
							DecodeMessage(message);
						});
				}
			}
			catch (Exception ex)
			{
				if (OnErrorMessage != null)
				{
					OnErrorMessage(ex.Message);
				}
			}
		}

		private void DecodeMessage(string message)
		{
			try
			{
				int firstCommaIndex = message.IndexOf(',');

				if (firstCommaIndex > 0)
				{
					string command = message.Substring(0, firstCommaIndex);
					string messageData = message.Substring(firstCommaIndex + 1);

					if (eProtocol.GameCreated.ToString().Equals(command))
					{
						GameCreated(messageData);
					}
					else if (eProtocol.GameCancelled.ToString().Equals(command))
					{
						GameCancelled(messageData);
					}
					else if (eProtocol.GameJoined.ToString().Equals(command))
					{
						GameJoined(messageData);
					}
					else if (eProtocol.GameStarted.ToString().Equals(command))
					{
						GameStarted(messageData);
					}
				}
			}
			catch (Exception ex)
			{
				if (OnErrorMessage != null)
				{
					OnErrorMessage(ex.Message);
				}
			}
		}

		private void GameCreated(string messageData)
		{
			int commaIndex = messageData.IndexOf(',');

			if (commaIndex > 0)
			{
				string ipAddress = messageData.Substring(0, commaIndex);
				string title = messageData.Substring(commaIndex + 1);

				if (ipAddress != CurrentIPAddress())
				{
					GameDetails gameInformation = new GameDetails() { CreatorsIpAddress = ipAddress, GameTitle = title };
					if (OnGameCreatedReceived != null)
					{
						OnGameCreatedReceived(gameInformation);
					}
				}
			}
		}

		private void GameCancelled(string messageData)
		{
			int commaIndex = messageData.IndexOf(',');

			if (commaIndex > 0)
			{
				string ipAddress = messageData.Substring(0, commaIndex);
				string title = messageData.Substring(commaIndex + 1);

				if (ipAddress != CurrentIPAddress())
				{
					GameDetails gameInformation = new GameDetails() { CreatorsIpAddress = ipAddress, GameTitle = title };
					if (OnGameCancelledReceived != null)
					{
						OnGameCancelledReceived(gameInformation);
					}
				}
			}
		}

		private void GameJoined(string messageData)
		{
			int commaIndex = messageData.IndexOf(',');

			if (commaIndex > 0)
			{
				string playersIpAddress = messageData.Substring(0, commaIndex);
				string playersDetails = messageData.Substring(commaIndex + 1);

				if (OnGameJoinedReceived != null)
				{
					OnGameJoinedReceived(playersIpAddress, playersDetails);
				}
			}
		}

		private void GameStarted(string messageData)
		{
			int commaIndex = messageData.IndexOf(',');

			if (commaIndex > 0)
			{
				string ipAddress = messageData.Substring(0, commaIndex);
				string title = messageData.Substring(commaIndex + 1);

				GameDetails gameInformation = new GameDetails() { CreatorsIpAddress = ipAddress, GameTitle = title };
				if (OnGameStartedReceived != null)
				{
					OnGameStartedReceived(gameInformation);
				}
			}
		}
	}
}
