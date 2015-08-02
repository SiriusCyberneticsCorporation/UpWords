using System;
using System.Collections.Generic;
//using System.IO;
using System.Linq;
using System.Text;

using Windows.Networking;
//using Windows.Networking.Connectivity;
//using Windows.Networking.Sockets;
//using Windows.Storage.Streams;

namespace UpWords
{
    public class UpwordsNetworking : LANBase
    {
		public delegate void GamePostHandler(GameDetails gameInformation);
		public event GamePostHandler OnGameCreatedReceived;
		public event GamePostHandler OnGameCancelledReceived;
		public event GamePostHandler OnGameStartedReceived;

		public delegate void GameJoinedHandler(string playersIpAddress, string playersDetails);
		public event GameJoinedHandler OnGameJoinedReceived;

		private bool m_continueRebroadcast = false;
		private Dictionary<string, eProtocol> m_sentMessages = new Dictionary<string, eProtocol>();

		public enum eProtocol
		{
			Acknowledge,
			GameCreated,
			GameCancelled,
			GameJoined,
			GameStarted,
			WaitingOnPlayers,
		}

		public UpwordsNetworking()
		{
		}

		public void CreateGame(string gameTitle)
		{
			string message = eProtocol.GameCreated.ToString() + "," + gameTitle;

			m_continueRebroadcast = true;

			RebroardcastGame(message);
		}

		private async void RebroardcastGame(string message)
		{
			BroadcastMessage(message);

			await System.Threading.Tasks.Task.Delay(TimeSpan.FromSeconds(15));

			if (m_continueRebroadcast)
			{
				RebroardcastGame(message);
			}
		}

		public void StartGame(string gameTitle)
		{
			string message = eProtocol.GameStarted.ToString() + "," + gameTitle;
			
			m_continueRebroadcast = false;

			BroadcastMessage(message);
		}

		public void JoinGame(GameDetails gameInformation)
		{
			string message = eProtocol.GameJoined.ToString() + "," + Username + " on " + MachineName;;

			if (!m_sentMessages.ContainsKey(gameInformation.CreatorsIpAddress))
			{
				m_sentMessages.Add(gameInformation.CreatorsIpAddress, eProtocol.GameJoined);
			}
			SendMessage(gameInformation.CreatorsIpAddress, message);
		}

		public void CancelGame(string gameTitle)
		{
			string message = eProtocol.GameCancelled.ToString() + "," + gameTitle;

			m_continueRebroadcast = false;

			BroadcastMessage(message);
		}

		protected override void DecodeMessage(HostName remoteAddress, string message)
		{
			try
			{
				int firstCommaIndex = message.IndexOf(',');

				if (firstCommaIndex > 0)
				{
					string commandText = message.Substring(0, firstCommaIndex);
					string messageData = message.Substring(firstCommaIndex + 1);
					eProtocol command = EnumHelper.GetEnum<eProtocol>(commandText);

					switch (command)
					{
						case eProtocol.Acknowledge:
							MessageAcknowledged(remoteAddress, messageData);
							break;
						case eProtocol.GameCancelled:
							GameCancelled(remoteAddress, messageData);
							break;
						case eProtocol.GameCreated:
							GameCreated(remoteAddress, messageData);
							break;
						case eProtocol.GameJoined:
							GameJoined(remoteAddress, messageData);
							break;
						case eProtocol.GameStarted:
							GameStarted(remoteAddress, messageData);
							break;
						case eProtocol.WaitingOnPlayers:
							break;
					}
				}
			}
			catch (Exception ex)
			{
				PostMessage(ex.Message);
			}
		}

		private void MessageAcknowledged(HostName remoteAddress, string messageData)
		{

		}

		private void GameCreated(HostName remoteAddress, string messageData)
		{
			if (remoteAddress.CanonicalName != CurrentIPAddress())
			{
				GameDetails gameInformation = new GameDetails() { CreatorsIpAddress = remoteAddress.CanonicalName, GameTitle = messageData };
				if (OnGameCreatedReceived != null)
				{
					OnGameCreatedReceived(gameInformation);
				}
			}
		}

		private void GameCancelled(HostName remoteAddress, string messageData)
		{
			if (remoteAddress.CanonicalName != CurrentIPAddress())
			{
				GameDetails gameInformation = new GameDetails() { CreatorsIpAddress = remoteAddress.CanonicalName, GameTitle = messageData };
				if (OnGameCancelledReceived != null)
				{
					OnGameCancelledReceived(gameInformation);
				}
			}
		}

		private void GameJoined(HostName remoteAddress, string messageData)
		{
			if (OnGameJoinedReceived != null)
			{
				OnGameJoinedReceived(remoteAddress.CanonicalName, messageData);
			}
		}

		private void GameStarted(HostName remoteAddress, string messageData)
		{
			GameDetails gameInformation = new GameDetails() { CreatorsIpAddress = remoteAddress.CanonicalName, GameTitle = messageData };
			if (OnGameStartedReceived != null)
			{
				OnGameStartedReceived(gameInformation);
			}
		}
	}
}
