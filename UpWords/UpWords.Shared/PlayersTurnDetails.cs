using System;
using System.Collections.Generic;
using System.Text;

namespace UpWords
{
    public class PlayersTurnDetails
    {
		public int Score = 0;
		public int TotalScore = 0;
		public string PlayerName;
		public string PlayersIP;
		public List<UpWord> PlayedWords = new List<UpWord>();
		public List<TileDetails> LettersPlayed = new List<TileDetails>();
    }
}
