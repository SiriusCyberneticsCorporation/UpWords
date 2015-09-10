using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Networking;
using Windows.Networking.Connectivity;
using Windows.Networking.Sockets;
using Windows.Storage.Streams;

using Windows.ApplicationModel.Core;
using Windows.UI.Core;

namespace UpWords
{
    public abstract class LANBase
    {
		public delegate void ErrorMessageHandler(string message);
		public event ErrorMessageHandler OnErrorMessage;

		public delegate void PingResponseHandler(string pingedIpAddress);
		public event PingResponseHandler OnNoPingResponse;

		public string IpAddress { get { return m_ipAddress; } }
		public string MachineName { get { return m_machineName; } }
		public string Username { get { return m_username; } }

		public bool Paused;

		private const int MESSAGE_RETRY_SECONDS = 3;
		private const int MESSAGE_RETRY_LIMIT = 2;
		private const int PING_CYCLE_SECONDS = 10;
		private const string GAME_PORT = "4321";
		protected const string BROADCAST_IP = "255.255.255.255";

		private Stack<Guid> m_recentMessages = new Stack<Guid>(10);
		private bool m_started = false;
		private string m_username = string.Empty;
		private string m_ipAddress = string.Empty;
		private string m_machineName = string.Empty;
		private DateTime m_lastPingTime = DateTime.MinValue;
		private List<string> m_ipAddressesToPing = new List<string>();
		private DatagramSocket m_broadcastSocket = new DatagramSocket();

		private object m_messageQueueLock = new object();
		private List<NetworkMessage> m_outstandingMessages = new List<NetworkMessage>();

		protected abstract void DecodeMessage(HostName remoteAddress, NetworkMessagePacket message);

		public LANBase()
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

					SendOutstandingMessages();
				}
				catch (Exception ex)
				{
					PostMessage(ex.Message);
				}
				m_started = true;
			}
		}

		private async void SendOutstandingMessages()
		{
			try
			{
				if (!Paused)
				{
					lock (m_messageQueueLock)
					{
						List<NetworkMessage> messagesToRemove = new List<NetworkMessage>();

						foreach (NetworkMessage message in m_outstandingMessages)
						{
							// If the message has already been tried the appropriate number of times then delete it from the queue.
							if(message.SendAttempts > MESSAGE_RETRY_LIMIT)
							{
								messagesToRemove.Add(message);
							}
							// If this is a retry of a ping then assume the remote connection has terminated.
							else if(message.MessagePacket.Command == eProtocol.Ping && message.SendAttempts > 0)
							{
								messagesToRemove.Add(message);

								if (OnNoPingResponse != null)
								{				
									OnNoPingResponse(message.RecipientsIP);
								}
							}
							// If sufficient time has passed since the last send then send the message now.
							else if (DateTime.Now.Subtract(message.TimeStamp).TotalSeconds > MESSAGE_RETRY_SECONDS)
							{
								SendMessage(message);

								// If this is an acknowledge message then remove it from the queue immediately.
								if (message.MessagePacket.Command == eProtocol.Acknowledge)
								{
									messagesToRemove.Add(message);
								}
							}
						}

						foreach (NetworkMessage messageToRemove in messagesToRemove)
						{
							m_outstandingMessages.Remove(messageToRemove);
						}
					}

					if (DateTime.Now.Subtract(m_lastPingTime).TotalSeconds > PING_CYCLE_SECONDS)
					foreach (string ipAddress in m_ipAddressesToPing)
					{
						PingAddress(ipAddress);
					}

					await System.Threading.Tasks.Task.Delay(TimeSpan.FromSeconds(1));
				}
				else
				{
					await System.Threading.Tasks.Task.Delay(TimeSpan.FromSeconds(5));
				}
			}
			catch (Exception ex)
			{
				PostMessage(ex.Message);

			}
			SendOutstandingMessages();
		}

		protected void AddMessageToQueue(NetworkMessage message)
		{
			if (!Paused)
			{
				lock (m_messageQueueLock)
				{
					m_outstandingMessages.Add(message);
				}
			}
		}

		protected bool QueueContainsMessage(string destinationIP, eProtocol command)
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

		protected void RemoveMessageFromQueue(Guid messageID)
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

		protected void RemoveMessageFromQueue(string destinationIP, eProtocol command)
		{
			lock (m_messageQueueLock)
			{
				NetworkMessage messageToRemove = null;
				foreach (NetworkMessage message in m_outstandingMessages)
				{
					if (message.RecipientsIP == destinationIP && message.MessagePacket.Command == command)
					{
						messageToRemove = message;
						break;
					}
				}
				m_outstandingMessages.Remove(messageToRemove);
			}
		}

		private async void SendMessage(NetworkMessage message)
		{
			await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync
				(CoreDispatcherPriority.Normal, () => { SendMessageAsync(message); });
		}

		public async void SendMessageAsync(NetworkMessage message)
		{
			try
			{
				message.SendAttempts++;
				message.TimeStamp = DateTime.Now;
				using (IOutputStream stream = await m_broadcastSocket.GetOutputStreamAsync(new HostName(message.RecipientsIP), GAME_PORT))
				{
					using (DataWriter writer = new DataWriter(stream))
					{
						var data = System.Text.Encoding.UTF8.GetBytes(Serialiser.SerializeToXml<NetworkMessagePacket>(message.MessagePacket));

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
					string messageText = reader.ReadToEnd();

					if (args.RemoteAddress.CanonicalName != CurrentIPAddress())
					{
						NetworkMessagePacket message = Serialiser.DeserializeFromXml<NetworkMessagePacket>(messageText);

						if (message.Command == eProtocol.Acknowledge || message.Command == eProtocol.Ping)
						{
							RemoveMessageFromQueue(message.ID);
						}
						else if (!m_recentMessages.Contains(message.ID))
						{
							m_recentMessages.Push(message.ID);

							await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync
								(CoreDispatcherPriority.Normal, () =>
								{
									DecodeMessage(args.RemoteAddress, message);
								});
						}
						AcknowledgedMessage(args.RemoteAddress.CanonicalName, message);
					}
				}
			}
			catch (Exception ex)
			{
				PostMessage(ex.Message);
			}
		}

		private void PingAddress(string ipAddressToPing)
		{
			NetworkMessage message = new NetworkMessage()
			{
				RecipientsIP = ipAddressToPing,
				MessagePacket = new NetworkMessagePacket()
				{
					Command = eProtocol.Ping
				}
			};

			AddMessageToQueue(message);
		}

		private void AcknowledgedMessage(string recipientsIP, NetworkMessagePacket messagePacket)
		{
			if (messagePacket.Command != eProtocol.Acknowledge)
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
