using System;

namespace DarkLight
{
	/// <summary>
	/// Contains Status Output functions.
	/// </summary>
	public class StatusOutput
	{
		private StatusOutput()
		{
		}

		/// <summary>
		/// Should the writing functions beep with the speaker when used.
		/// </summary>
		public static bool useSounds = false;

		/// <summary>
		/// Writes a error message to the screen. The message will be printed in the
		/// color that represents an error.
		/// </summary>
		/// <param name="text">The text to print.</param>
		public static void WriteError(string text)
		{
			ConsoleColor.SetForeGroundColor(ConsoleColor.ForeGroundColor.Red,true);
			WriteLn(text);
			ConsoleColor.SetForeGroundColor();
		}

		/// <summary>
		/// Writes a status message to the screen. The message will be printed in the
		/// color that represents an status report.
		/// </summary>
		/// <param name="text">The text to print.</param>
		public static void WriteStatus(string text)
		{
			ConsoleColor.SetForeGroundColor(ConsoleColor.ForeGroundColor.Blue,true);
			WriteLn(text);
			ConsoleColor.SetForeGroundColor();
		}

		/// <summary>
		/// Writes a server status message to the screen. The message will be printed in the
		/// color that represents an server status report.
		/// </summary>
		/// <param name="text">The text to print.</param>
		public static void WriteServerStatus(string text)
		{
			ConsoleColor.SetForeGroundColor(ConsoleColor.ForeGroundColor.Green,true);
			WriteLn(text);
			ConsoleColor.SetForeGroundColor();
		}

		/// <summary>
		/// Writes a server error message to the screen. The message will be printed in the
		/// color that represents an server error report.
		/// </summary>
		/// <param name="text">The text to print.</param>
		public static void WriteServerError(string text)
		{
			ConsoleColor.SetForeGroundColor(ConsoleColor.ForeGroundColor.Magenta,true);
			WriteLn(text);
			ConsoleColor.SetForeGroundColor();
		}

		/// <summary>
		/// Writes a info message to the screen. The message will be printed in the
		/// color that represents an info report.
		/// </summary>
		/// <param name="text">The text to print.</param>
		public static void WriteInfo(string text)
		{
			ConsoleColor.SetForeGroundColor(ConsoleColor.ForeGroundColor.Cyan,false);
			WriteLn(text);
			ConsoleColor.SetForeGroundColor();
		}

		public static void WriteLn(string text)
		{
			Write(text+"\n");
		}

		public static void Write(string text)
		{
			for(int x = 0; x < text.Length; x++)
			{
				System.Threading.Thread.Sleep(10);
				if((text[x] == '\n' || x == text.Length) && useSounds)
					Beep(1111,5);
				else if(useSounds)
					Beep(444,1);
				Console.Write(text[x]);
			}
		}

		[System.Runtime.InteropServices.DllImport("Kernel32.dll")]
		public static extern bool Beep(int dwFreq,int dwDuration);
	}
}
