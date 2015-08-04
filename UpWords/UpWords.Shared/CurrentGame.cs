using System;
using System.Collections.Generic;
using System.Text;
using Windows.UI.Xaml.Documents;

namespace UpWords
{
    public class CurrentGame
    {
		public int Score = 0;
		public string GameServer = string.Empty;
		public List<Paragraph> PlayDetails = new List<Paragraph>();
    }
}
