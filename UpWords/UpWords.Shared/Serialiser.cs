using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Text;

namespace UpWords
{
    public class Serialiser
    {
		public static string SerializeToXml<T>(T value)
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

		public static T DeserializeFromXml<T>(string itemAsXml) where T : new()
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
