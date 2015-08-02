using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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
		JustPlayed,
		Played,
	}

	public enum eTurnState
	{
		Unknown,
		PlayersTurn,
		ComputersTurn,
		GameOver,
	}

	public class EnumHelper
	{
		/// <summary>
		/// Returns the element of the Enumeration specified by the passed string.
		/// </summary>
		/// <remarks>If the string does not evaluate to a valid element of the Enumeration then the
		/// first element of the enumeration is returned.</remarks>
		/// <typeparam name="T">The Enumeration that is to be returned.</typeparam>
		/// <param name="enumString">The string representation of an Enumeration element.</param>
		/// <returns>Returns the element of the Enumeration specified by the passed string.</returns>
		public static T GetEnum<T>(string enumString)
		{
			if (Enum.IsDefined(typeof(T), enumString))
			{
				return (T)Enum.Parse(typeof(T), enumString);
			}
			else
			{
				return (T)(Enum.GetValues(typeof(T)).GetValue(0));
			}
		}

		/// <summary>
		/// Returns the element of the Enumeration equivalent to the integer value passed.
		/// </summary>
		/// <remarks>If the integer value does not evaluate to a valid element of the Enumeration then the
		/// first element of the enumeration is returned.</remarks>
		/// <typeparam name="T">The Enumeration that is to be returned.</typeparam>
		/// <param name="integerValue">The integer representation of an Enumeration element.</param>
		/// <returns>Returns the element of the Enumeration specified by the integer value passed.</returns>
		public static T GetEnum<T>(int integerValue)
		{
			if (Enum.IsDefined(typeof(T), integerValue))
			{
				return (T)Enum.ToObject(typeof(T), integerValue);
			}
			else
			{
				return (T)(Enum.GetValues(typeof(T)).GetValue(0));
			}
		}
	}
}
