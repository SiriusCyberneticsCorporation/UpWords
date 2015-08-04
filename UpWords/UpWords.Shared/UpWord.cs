using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UpWords
{
	public class UpWord
	{
		public int Score;
		public string Word;

		private int m_singleLayerScore = 0;
		private int m_multiLayerScore = 0;
		private bool m_allTilesOneHigh = true;

		public void AppendLetter(TileControl tile)
		{
			Word += tile.Letter;
			UpdateScore(tile);
		}

		public void PrependLetter(TileControl tile)
		{
			Word = tile.Letter + Word;
			UpdateScore(tile);
		}

		private void UpdateScore(TileControl tile)
		{
			m_multiLayerScore += tile.Layer + 1;

			if (tile.Layer == 0)
			{
				m_singleLayerScore += 2;
			}
			else
			{
				m_allTilesOneHigh = false;
			}

			Score = m_allTilesOneHigh ? m_singleLayerScore : m_multiLayerScore;
		}
	}
}
