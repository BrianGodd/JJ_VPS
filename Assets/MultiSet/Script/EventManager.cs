using System;
using System.Collections.Generic;

namespace MultiSet
{
	public class EventManager<T>
	{
		private static Dictionary<string, Action<T>> eventDictionary = new Dictionary<string, Action<T>>();

		public static void StartListening(string eventName, Action<T> listener)
		{
			Action<T> value = null;
			if (eventDictionary.TryGetValue(eventName, out value))
			{
				value = (Action<T>)Delegate.Combine(value, listener);
				return;
			}
			value = (Action<T>)Delegate.Combine(value, listener);
			eventDictionary.Add(eventName, value);
		}

		public static void StopListening(string eventName, Action<T> listener)
		{
			if (eventDictionary.TryGetValue(eventName, out Action<T> value))
			{
				value = (Action<T>)Delegate.Remove(value, listener);
				eventDictionary.Remove(eventName);
			}
		}

		public static void TriggerEvent(string eventName, T data)
		{
			Action<T> value = null;
			if (eventDictionary.TryGetValue(eventName, out value))
			{
				value(data);
			}
		}
	}
}
