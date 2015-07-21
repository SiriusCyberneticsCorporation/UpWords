using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UpWords
{
	public enum eTileState
	{
		Unknown,
		InBag,
		OnPlayerPanel,
		OnComputerPanel,
		ComposingNewWord,
		Played,
	}

	public enum eTurnState
	{
		Unknown,
		PlayersTurn,
		ComputersTurn,
		GameOver,
	}

}
