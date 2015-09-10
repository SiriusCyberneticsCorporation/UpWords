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

		public delegate void ExchangeLetterHandler(string senderIP, TileDetails letter);
		public event ExchangeLetterHandler OnLetterExchange;

		public delegate void ActivePlayerHandler(PlayerDetails playersDetails);
		public event ActivePlayerHandler OnSetActivePlayerReceived;

		public delegate void SimpleIPHandler(string senderIpAddress);
		public event SimpleIPHandler OnReadyToStartReceived;
		public event SimpleIPHandler OnStartGameReceived;

		public delegate void NewLettersHandler(string serverIP, GameLetters letters);
		public event NewLettersHandler OnLettersReceived;

		public delegate void GameJoinedHandler(string playersIpAddress, PlayerDetails playersDetails);
		public event GameJoinedHandler OnGameJoinedReceived;

		public delegate void TurnDetailsHandler(string playersIpAddress, PlayersTurnDetails iPlayersTurnDetails);
		public event TurnDetailsHandler OnPlayersTurnDetailsReceived;

		private DateTime m_lastCommunicationsCheck = DateTime.Now;
		private List<PlayerDetails> m_activePlayers = new List<PlayerDetails>();

		public UpwordsNetworking()
		{
		}

		public void CreateGame(string gameTitle)
		{
			NetworkMessage message = new NetworkMessage()
			{
				RecipientsIP = BROADCAST_IP,
				MessagePacket = new NetworkMessagePacket()
				{
					Command = eProtocol.GameCreated,
					MessageText = gameTitle
				}
			};

			AddMessageToQueue(message);
		}

		public void ExchangeLetter(TileDetails letter)
		{
			string messageText = Serialiser.SerializeToXml<TileDetails>(letter);
			NetworkMessage message = new NetworkMessage()
			{
				RecipientsIP = IpAddress,
				MessagePacket = new NetworkMessagePacket()
				{
					Command = eProtocol.ExchangeLetter,
					MessageText = messageText
				}
			};

			AddMessageToQueue(message);
		}

		public void SetActivePlayer(string playerIP, PlayerDetails activePlayer)
		{
			string messageText = Serialiser.SerializeToXml<PlayerDetails>(activePlayer);
			NetworkMessage message = new NetworkMessage()
			{
				RecipientsIP = playerIP,
				MessagePacket = new NetworkMessagePacket()
				{
					Command = eProtocol.SetActivePlayer,
					MessageText = messageText
				}
			};

			AddMessageToQueue(message);
		}

		public void SendReadyToStart(string serverIP)
		{
			NetworkMessage message = new NetworkMessage()
			{
				RecipientsIP = serverIP,
				MessagePacket = new NetworkMessagePacket()
				{
					Command = eProtocol.ReadyToStart
				}
			};

			AddMessageToQueue(message);
		}

		public void StartGame(string playerIP)
		{
			RemoveMessageFromQueue(BROADCAST_IP, eProtocol.GameCreated);

			NetworkMessage message = new NetworkMessage()
			{
				RecipientsIP = playerIP,
				MessagePacket = new NetworkMessagePacket()
				{
					Command = eProtocol.StartGame
				}
			};

			AddMessageToQueue(message);
		}

		public void SendLetters(string playerIP, GameLetters letters)
		{
			string messageText = Serialiser.SerializeToXml<GameLetters>(letters);
			NetworkMessage message = new NetworkMessage()
			{
				RecipientsIP = playerIP,
				MessagePacket = new NetworkMessagePacket()
				{
					Command = eProtocol.SendLetters,
					MessageText = messageText
				}
			};

			AddMessageToQueue(message);
		}

		public void JoinGame(string creatorsIpAddress, PlayerDetails activePlayer)
		{
			if (!QueueContainsMessage(creatorsIpAddress, eProtocol.GameJoined))
			{
				string messageText = Serialiser.SerializeToXml<PlayerDetails>(activePlayer);
				NetworkMessage message = new NetworkMessage()
				{
					RecipientsIP = creatorsIpAddress,
					MessagePacket = new NetworkMessagePacket()
					{
						Command = eProtocol.GameJoined,
						MessageText = messageText
					}
				};

				AddMessageToQueue(message);
			}
		}

		public void CancelGame(string gameTitle)
		{
			RemoveMessageFromQueue(BROADCAST_IP, eProtocol.GameCreated);

			NetworkMessage message = new NetworkMessage()
			{
				RecipientsIP = BROADCAST_IP,
				MessagePacket = new NetworkMessagePacket()
				{
					Command = eProtocol.GameCancelled,
					MessageText = gameTitle
				}
			};

			AddMessageToQueue(message);
		}

		public void SendPlayersTurnDetails(string serverIP, PlayersTurnDetails iPlayersTurnDetails)
		{
			string messageText = Serialiser.SerializeToXml<PlayersTurnDetails>(iPlayersTurnDetails);

			NetworkMessage message = new NetworkMessage()
			{
				RecipientsIP = serverIP,
				MessagePacket = new NetworkMessagePacket()
				{
					Command = eProtocol.PlayersTurnDetails,
					MessageText = messageText
				}
			};

			AddMessageToQueue(message);
		}

		/*
		public void CheckActivePlayers()
		{			
			if (DateTime.Now.Subtract(m_lastCommunicationsCheck).TotalSeconds > 5)
			{
				m_lastCommunicationsCheck = DateTime.Now;
				if (m_activePlayers.Count > 0)
				{
					foreach (PlayerDetails iPlayerDetails in m_activePlayers)
					{
					}
				}
			}
		}
		*/

		protected override void DecodeMessage(HostName remoteAddress, NetworkMessagePacket message)
		{
			try
			{
				switch (message.Command)
				{
					case eProtocol.Acknowledge:
						MessageAcknowledged(remoteAddress, message);
						break;
					case eProtocol.ExchangeLetter:
						ExchangeLetterReceived(remoteAddress, message);
						break;
					case eProtocol.GameCancelled:
						GameCancelledReceived(remoteAddress, message);
						break;
					case eProtocol.GameCreated:
						GameCreatedReceived(remoteAddress, message);
						break;
					case eProtocol.GameJoined:
						GameJoinedReceived(remoteAddress, message);
						break;
					case eProtocol.ReadyToStart:
						ReadyToStartReceived(remoteAddress, message);
						break;
					case eProtocol.SendLetters:
						LettersReceived(remoteAddress, message);
						break;
					case eProtocol.SetActivePlayer:
						SetActivePlayerReceived(remoteAddress, message);
						break;
					case eProtocol.StartGame:
						StartGameReceived(remoteAddress, message);
						break;
					case eProtocol.PlayersTurnDetails:
						PlayersTurnDetailsReceived(remoteAddress, message);
						break;
				}
			}
			catch (Exception ex)
			{
				PostMessage(ex.Message);
			}
		}

		private void MessageAcknowledged(HostName remoteAddress, NetworkMessagePacket message)
		{
			RemoveMessageFromQueue(message.ID);
		}

		private void GameCreatedReceived(HostName remoteAddress, NetworkMessagePacket message)
		{
			if (remoteAddress.CanonicalName != CurrentIPAddress())
			{
				GameDetails gameInformation = new GameDetails() { CreatorsIpAddress = remoteAddress.CanonicalName, GameTitle = message.MessageText };
				if (OnGameCreatedReceived != null)
				{
					OnGameCreatedReceived(gameInformation);
				}
			}
		}

		private void ExchangeLetterReceived(HostName remoteAddress, NetworkMessagePacket message)
		{
			TileDetails letter = Serialiser.DeserializeFromXml<TileDetails>(message.MessageText);

			if (OnLetterExchange != null)
			{
				OnLetterExchange(remoteAddress.CanonicalName, letter);
			}
		}

		private void GameCancelledReceived(HostName remoteAddress, NetworkMessagePacket message)
		{
			if (remoteAddress.CanonicalName != CurrentIPAddress())
			{
				GameDetails gameInformation = new GameDetails() { CreatorsIpAddress = remoteAddress.CanonicalName, GameTitle = message.MessageText };
				if (OnGameCancelledReceived != null)
				{
					OnGameCancelledReceived(gameInformation);
				}
			}
		}

		private void GameJoinedReceived(HostName remoteAddress, NetworkMessagePacket message)
		{
			PlayerDetails activePlayer = Serialiser.DeserializeFromXml<PlayerDetails>(message.MessageText);

			if (OnGameJoinedReceived != null)
			{
				OnGameJoinedReceived(remoteAddress.CanonicalName, activePlayer);
			}
		}

		private void ReadyToStartReceived(HostName remoteAddress, NetworkMessagePacket message)
		{
			if (OnReadyToStartReceived != null)
			{
				OnReadyToStartReceived(remoteAddress.CanonicalName);
			}
		}

		private void LettersReceived(HostName remoteAddress, NetworkMessagePacket message)
		{
			GameLetters letters = Serialiser.DeserializeFromXml<GameLetters>(message.MessageText);

			if (OnLettersReceived != null)
			{
				OnLettersReceived(remoteAddress.CanonicalName, letters);
			}
		}

		private void SetActivePlayerReceived(HostName remoteAddress, NetworkMessagePacket message)
		{
			PlayerDetails activePlayer = Serialiser.DeserializeFromXml<PlayerDetails>(message.MessageText);

			if(!m_activePlayers.Contains(activePlayer))
			{
				m_activePlayers.Add(activePlayer);
			}

			if (OnSetActivePlayerReceived != null)
			{
				OnSetActivePlayerReceived(activePlayer);
			}
		}

		private void StartGameReceived(HostName remoteAddress, NetworkMessagePacket message)
		{
			if (OnStartGameReceived != null)
			{
				OnStartGameReceived(remoteAddress.CanonicalName);
			}
		}

		private void PlayersTurnDetailsReceived(HostName remoteAddress, NetworkMessagePacket message)
		{
			PlayersTurnDetails iPlayersTurnDetails = Serialiser.DeserializeFromXml<PlayersTurnDetails>(message.MessageText);

			if(OnPlayersTurnDetailsReceived != null)
			{
				OnPlayersTurnDetailsReceived(remoteAddress.CanonicalName, iPlayersTurnDetails);
			}
		}
		
	}
}
