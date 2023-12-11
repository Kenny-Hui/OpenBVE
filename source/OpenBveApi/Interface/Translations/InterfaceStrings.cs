using OpenBveApi.Hosts;

namespace OpenBveApi.Interface
{
	public static partial class Translations
	{
		/// <summary>Stores the current language-code</summary>
		public static string CurrentLanguageCode = "en-GB";

		internal struct InterfaceString
		{
			/// <summary>The name of the string</summary>
			internal string Name;
			/// <summary>The translated string text</summary>
			internal string Text;
		}
		
		/// <summary>Sets the in-game language</summary>
		/// <param name="Language">The language string to set</param>
		public static void SetInGameLanguage(string Language)
		{
			//Set command infos to the translated strings
			for (int i = 0; i < AvailableLanguages.Count; i++)
			{
				//This is a hack, but the commandinfos are used in too many places to twiddle with easily
				if (AvailableLanguages[i].LanguageCode == Language)
				{
					CommandInfos = AvailableLanguages[i].myCommandInfos;
					TranslatedKeys = AvailableLanguages[i].KeyInfos;
					QuickReferences = AvailableNewLanguages[Language].QuickReferences;
					break;
				}
			}
		}

		/// <summary>Fetches a translated user interface string</summary>
		/// <param name="Application">The application string database to search</param>
		/// <param name="parameters">A string array containing the group tree to retrieve the string</param>
		/// <returns>The translated string</returns>
		public static string GetInterfaceString(HostApplication Application, string[] parameters)
		{
			// note: languages may be zero at startup before things have spun up- winforms....
			if (AvailableNewLanguages.Count != 0)
			{
				return AvailableNewLanguages[CurrentLanguageCode].GetInterfaceString(Application, parameters);
			}

			return string.Empty;
		}
		
		/// <summary>Holds the current set of interface quick reference strings</summary>
		public static InterfaceQuickReference QuickReferences;
		/// <summary>The number of score events to be displayed</summary>
		/// TODO: Appears to remain constant, investigate exact usages and whether we can dump
		public static int RatingsCount = 10;

	}
}
