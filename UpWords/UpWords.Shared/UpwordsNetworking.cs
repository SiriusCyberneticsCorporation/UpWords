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

		public delegate void ActivePlayerHandler(bool active);
		public event ActivePlayerHandler OnSetActivePlayerReceived;

		public delegate void SimpleIPHandler(string senderIpAddress);
		public event SimpleIPHandler OnReadyToStartReceivedReceived;
		public event SimpleIPHandler OnStartGameReceived;

		public delegate void NewLettersHandler(string serverIP, List<string> letters);
		public event NewLettersHandler OnLettersReceived;

		public delegate void GameJoinedHandler(string playersIpAddress, string playersDetails);
		public event GameJoinedHandler OnGameJoinedReceived;

		public delegate void TurnDetailsHandler(string playersIpAddress, PlayersTurnDetails iPlayersTurnDetails);
		public event TurnDetailsHandler OnPlayersTurnDetailsReceived;

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

		public void SetActivePlayer(string playerIP, bool active)
		{
			string messageText = Serialiser.SerializeToXml<bool>(active);
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

		public void SendLetters(string playerIP, List<string> letters)
		{
			string messageText = Serialiser.SerializeToXml<List<string>>(letters);
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

		public void JoinGame(string creatorsIpAddress)
		{
			if (!QueueContainsMessage(creatorsIpAddress, eProtocol.GameJoined))
			{
				NetworkMessage message = new NetworkMessage()
				{
					RecipientsIP = creatorsIpAddress,
					MessagePacket = new NetworkMessagePacket()
					{
						Command = eProtocol.GameJoined,
						MessageText = Username + " on " + MachineName
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
			if (OnGameJoinedReceived != null)
			{
				OnGameJoinedReceived(remoteAddress.CanonicalName, message.MessageText);
			}
			/*
			Acknowledgement iAcknowledgement = new Acknowledgement()
			{
				Command = eProtocol.GameJoined,
				Message = "Waiting for other players"
			};

			NetworkMessagePacket iNetworkMessagePacket = new NetworkMessagePacket()
			{
				Command = eProtocol.Acknowledge,
				MessageText = Serialiser.SerializeToXml<Acknowledgement>(iAcknowledgement)
			};

			SendMessage(remoteAddress.CanonicalName, iNetworkMessagePacket);
			*/
		}

		private void ReadyToStartReceived(HostName remoteAddress, NetworkMessagePacket message)
		{
			if (OnReadyToStartReceivedReceived != null)
			{
				OnReadyToStartReceivedReceived(remoteAddress.CanonicalName);
			}
		}

		private void LettersReceived(HostName remoteAddress, NetworkMessagePacket message)
		{
			List<string> letters = Serialiser.DeserializeFromXml<List<string>>(message.MessageText);

			if (OnLettersReceived != null)
			{
				OnLettersReceived(remoteAddress.CanonicalName, letters);
			}
		}

		private void SetActivePlayerReceived(HostName remoteAddress, NetworkMessagePacket message)
		{
			bool active = Serialiser.DeserializeFromXml<bool>(message.MessageText);

			if (OnSetActivePlayerReceived != null)
			{
				OnSetActivePlayerReceived(active);
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
