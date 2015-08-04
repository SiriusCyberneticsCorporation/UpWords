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
		public bool GameCreated = false;
		public string CreatorsIpAddress;
		public string GameTitle;
		public Dictionary<string, string> PlayersJoined = new Dictionary<string, string>();

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
