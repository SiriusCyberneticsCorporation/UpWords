using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Xml;
using Windows.Storage;


namespace UpWords
{
    public class GameSettings
    {
		public int TotalScore = 0;
		public bool GameStarted = false;
		public bool IamGameCreator = false;
		public string ActivePlayer;
		public string CreatorsIpAddress;
		public string GameTitle;
		public List<string> LetterBag = new List<string>();
		public PlayerDetails MyDetails = new PlayerDetails();
		public Dictionary<string, PlayerDetails> PlayersJoined = new Dictionary<string, PlayerDetails>();

		private static GameSettings m_settings = null;

		public static GameSettings Settings
		{
			get
			{
				if(m_settings == null)
				{
					m_settings = ReadSettings();
					if (m_settings == null)
					{
						m_settings = new GameSettings();
					}
				}
				return m_settings;
			}
		}

		public static void ClearSettings()
		{
			GameSettings.Settings.TotalScore = 0;
			GameSettings.Settings.GameStarted = false;
			GameSettings.Settings.IamGameCreator = false;
			GameSettings.Settings.ActivePlayer = "";
			GameSettings.Settings.CreatorsIpAddress = "";
			GameSettings.Settings.GameTitle = "";
			GameSettings.Settings.LetterBag = new List<string>();
			GameSettings.Settings.MyDetails = new PlayerDetails();
			GameSettings.Settings.PlayersJoined.Clear();
			GameSettings.SaveSettings();
		}

		public static void SaveSettings()
		{
			SaveSetting("GameSettings", m_settings);
		}

		private static GameSettings ReadSettings()
		{
			return GetSetting< GameSettings>("GameSettings");
		}

		private static string GetMemberName<T>(System.Linq.Expressions.Expression<Func<T>> memberExpression)
		{
			System.Linq.Expressions.MemberExpression expressionBody = (System.Linq.Expressions.MemberExpression)memberExpression.Body;
			return expressionBody.Member.Name;
		}

		private static void SaveSetting<T>(string name, T value)
		{
			ApplicationData.Current.LocalSettings.Values[name] = Serialiser.SerializeToXml(value);
		}

		private static T GetSetting<T>(string name) where T : new()
		{
			return Serialiser.DeserializeFromXml<T>(ApplicationData.Current.LocalSettings.Values[name] as string);
		}

		

    }
}
