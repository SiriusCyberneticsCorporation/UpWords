using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;
using System.Xml;
//using System.Xml.Serialization;
using Windows.Storage;


namespace UpWords
{
    public class GameSettings
    {
		public int Fred;
		public GameDetails GameCreated = new GameDetails();
		public GameDetails GameJoined = new GameDetails();
		//public List<string> SomeList = new List<string>();
		//public Dictionary<string, string> SomeDictionary = new Dictionary<string, string>();

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
			ApplicationData.Current.LocalSettings.Values[name] = SerializeListToXml(value);
		}

		private static T GetSetting<T>(string name) where T : new()
		{
			return DeserializeXmlToList<T>(ApplicationData.Current.LocalSettings.Values[name] as string);
		}

		private static string SerializeListToXml<T>(T value)
		{
			try
			{
				using (MemoryStream memoryStream = new MemoryStream())
				{
					using (StreamReader reader = new StreamReader(memoryStream))
					{
						DataContractSerializer serializer = new DataContractSerializer(value.GetType());
						serializer.WriteObject(memoryStream, value);
						memoryStream.Position = 0;
						return reader.ReadToEnd();
					}
				}
			}

			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine(ex);
				return String.Empty;
			}
		}

		private static T DeserializeXmlToList<T>(string itemAsXml) where T : new()
		{
			try
			{
				if (itemAsXml == null)
				{
					return new T();
				}
				else
				{
					T item = default(T);

					using (Stream stream = new MemoryStream())
					{
						byte[] data = System.Text.Encoding.UTF8.GetBytes(itemAsXml);
						stream.Write(data, 0, data.Length);
						stream.Position = 0;
						DataContractSerializer deserializer = new DataContractSerializer(typeof(T));
						item = (T)deserializer.ReadObject(stream);
					}

					if (item == null)
					{
						item = new T();
					}

					return item;
				}
			}

			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine(ex);
				return default(T);
			}
		}

    }
}
