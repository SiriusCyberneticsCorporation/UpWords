using System;
using System.Collections.Generic;
using System.Text;

namespace UpWords
{
    public class NetworkMessage
    {
		public int SendAttempts = 0;
		public string RecipientsIP;
		public DateTime TimeStamp = DateTime.MinValue;
		public NetworkMessagePacket MessagePacket;
    }

	public class NetworkMessagePacket
	{
		public Guid ID = Guid.NewGuid();
		public eProtocol Command;
		public string MessageText;
	}
}
