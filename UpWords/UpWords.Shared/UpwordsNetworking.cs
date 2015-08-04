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

		public delegate void ActivePlayerHandler(bool active);
		public event ActivePlayerHandler OnSetActivePlayerReceived;

		public delegate void StartGameHandler(string serverIP, List<string> letters);
		public event StartGameHandler OnStartGameReceived;
		public event StartGameHandler OnLettersReceived;

		public delegate void GameJoinedHandler(string playersIpAddress, string playersDetails);
		public event GameJoinedHandler OnGameJoinedReceived;

		public delegate void TurnDetailsHandler(string playersIpAddress, PlayersTurnDetails iPlayersTurnDetails);
		public event TurnDetailsHandler OnPlayersTurnDetailsReceived;

		private object m_messageQueueLock = new object();
		private List<NetworkMessage> m_outstandingMessages = new List<NetworkMessage>();

		public UpwordsNetworking()
		{
			SendOutstandingMessages();
		}

		private async void SendOutstandingMessages()
		{
			try
			{
				await System.Threading.Tasks.Task.Delay(TimeSpan.FromSeconds(1));

				lock (m_messageQueueLock)
				{
					NetworkMessage messageToRemove = null;

					foreach (NetworkMessage message in m_outstandingMessages)
					{
						if (DateTime.Now.Subtract(message.TimeStamp).TotalSeconds > 3)
						{
							SendMessage(message);

							if(message.MessagePacket.Command == eProtocol.Acknowledge)
							{
								messageToRemove = message;
							}
						}
					}

					if (messageToRemove != null)
					{
						m_outstandingMessages.Remove(messageToRemove);
					}
				}
			}
			catch (Exception ex)
			{
				PostMessage(ex.Message);

			}
			SendOutstandingMessages();
		}

		private void AddMessageToQueue(NetworkMessage message)
		{
			lock(m_messageQueueLock)
			{
				m_outstandingMessages.Add(message);
			}
		}

		private bool QueueContainsMessage(string destinationIP, eProtocol command)
		{
			bool containsMessage = false;
			
			lock (m_messageQueueLock)
			{
				foreach (NetworkMessage message in m_outstandingMessages)
				{
					if (message.RecipientsIP == destinationIP && message.MessagePacket.Command == command)
					{
						containsMessage = true;
						break;
					}
				}
			}

			return containsMessage;
		}

		private void RemoveMessageFromQueue(Guid messageID)
		{
			lock (m_messageQueueLock)
			{
				NetworkMessage messageToRemove = null;
				foreach (NetworkMessage message in m_outstandingMessages)
				{
					if (message.MessagePacket.ID == messageID)
					{
						messageToRemove = message;
						break;
					}
				}
				if (messageToRemove != null)
				{
					m_outstandingMessages.Remove(messageToRemove);
				}
			}
		}

		private void RemoveMessageFromQueue(string destinationIP, eProtocol command)
		{
			lock (m_messageQueueLock)
			{
				NetworkMessage messageToRemove = null;
				foreach (NetworkMessage message in m_outstandingMessages)
				{
					if(message.RecipientsIP == destinationIP && message.MessagePacket.Command == command)
					{
						messageToRemove = message;
						break;
					}
				}
				m_outstandingMessages.Remove(messageToRemove);
			}
		}

		private void AcknowledgedMessage(string recipientsIP, NetworkMessagePacket messagePacket)
		{
			NetworkMessage message = new NetworkMessage()
			{
				RecipientsIP = recipientsIP,
				MessagePacket = new NetworkMessagePacket()
				{
					ID = messagePacket.ID,
					Command = eProtocol.Acknowledge
				}
			};

			AddMessageToQueue(message);
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

		public void StartGame(string playerIP, List<string> startingLetters)
		{
			RemoveMessageFromQueue(BROADCAST_IP, eProtocol.GameCreated);

			string messageText = Serialiser.SerializeToXml<List<string>>(startingLetters);
			NetworkMessage message = new NetworkMessage()
			{
				RecipientsIP = playerIP,
				MessagePacket = new NetworkMessagePacket()
				{
					Command = eProtocol.StartGame,
					MessageText = messageText
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
					case eProtocol.GameCancelled:
						GameCancelledReceived(remoteAddress, message);
						break;
					case eProtocol.GameCreated:
						GameCreatedReceived(remoteAddress, message);
						break;
					case eProtocol.GameJoined:
						GameJoinedReceived(remoteAddress, message);
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
			AcknowledgedMessage(remoteAddress.CanonicalName, message);
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
			AcknowledgedMessage(remoteAddress.CanonicalName, message);
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
			AcknowledgedMessage(remoteAddress.CanonicalName, message);
		}

		private void LettersReceived(HostName remoteAddress, NetworkMessagePacket message)
		{
			List<string> letters = Serialiser.DeserializeFromXml<List<string>>(message.MessageText);

			if (OnLettersReceived != null)
			{
				OnLettersReceived(remoteAddress.CanonicalName, letters);
			}
			AcknowledgedMessage(remoteAddress.CanonicalName, message);
		}

		private void SetActivePlayerReceived(HostName remoteAddress, NetworkMessagePacket message)
		{
			bool active = Serialiser.DeserializeFromXml<bool>(message.MessageText);

			if (OnSetActivePlayerReceived != null)
			{
				OnSetActivePlayerReceived(active);
			}
			AcknowledgedMessage(remoteAddress.CanonicalName, message);
		}

		private void StartGameReceived(HostName remoteAddress, NetworkMessagePacket message)
		{
			List<string> letters = Serialiser.DeserializeFromXml<List<string>>(message.MessageText);

			if (OnStartGameReceived != null)
			{
				OnStartGameReceived(remoteAddress.CanonicalName, letters);
			}
			AcknowledgedMessage(remoteAddress.CanonicalName, message);
		}

		private void PlayersTurnDetailsReceived(HostName remoteAddress, NetworkMessagePacket message)
		{
			PlayersTurnDetails iPlayersTurnDetails = Serialiser.DeserializeFromXml<PlayersTurnDetails>(message.MessageText);

			if(OnPlayersTurnDetailsReceived != null)
			{
				OnPlayersTurnDetailsReceived(remoteAddress.CanonicalName, iPlayersTurnDetails);
			}
			AcknowledgedMessage(remoteAddress.CanonicalName, message);
		}
		
	}
}
