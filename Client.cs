using System;
using System.Collections;
using System.ComponentModel;
using System.Data;
using System.Net;
using System.Net.Sockets;
using System.IO;
using System.Text;
using System.Security.Permissions;
using System.Runtime.InteropServices;

namespace DarkLight
{
	/// <summary>
	/// Klasa za spajanje na FTP servere
	/// </summary>
	public class FTP_Client
	{
		public TcpClient      Server            = null;
		public NetworkStream  NetStrm           = null;
		public StreamReader   RdStrm            = null;
		public string         str_Data          = null;
		public string         serverName        = null;
		public string         lastReply         = null;
		public byte[]         byte_Data         = null;
		public bool           isConnected       = false;
		public int            serverPort        = 21;
		public string         defaultUser       = "anonymous";
		public string         defaultPass       = "anonymous@something.com";
		public string		  defaultDnloadPath = "C:\\";
		private bool          keepalive         = false;	// not used
		private System.Timers.Timer timer1;
		public string		  lastPassword      = "anonymous";
		public string		  lastUserName      = "anonymous@something.com";

		public FTP_Client()
		{
			StatusOutput.WriteLn("- Welcome to FTP Client v0.9.8.8");
			StatusOutput.WriteLn("- Type \"help\" for a list of commands\n");
		}

		public bool KeepAlive	// broken
		{
			get
			{
				return keepalive;
			}
			set
			{
				keepalive = value;
				SetTimer(value);
			}
		}

		private void SetTimer(bool state)
		{
			timer1.Dispose();
			timer1 = null;
			
			if(state)
				return;

			timer1 = new System.Timers.Timer();
			timer1.Interval = 3000;
			timer1.Elapsed += new System.Timers.ElapsedEventHandler(Timer1Tick);
			timer1.Enabled = true;
		}

		private void Timer1Tick(object sender,System.Timers.ElapsedEventArgs e)
		{
			if(isConnected)
				SendCommand("NOOP","");
		}

		public void ResetVars()
		{
			Server          = null;
			NetStrm         = null;
			RdStrm          = null;
			str_Data        = null;
			serverName      = null;
			byte_Data       = null;
		}

		public bool ConnectServer(string server)
		{
		retry:
			try
			{
				serverName = server;

				Server = new TcpClient(serverName,serverPort);

				NetStrm = Server.GetStream();
				RdStrm = new StreamReader(Server.GetStream());
				
				if(!GetServerStatus())
					return false;
			}
			catch(System.Exception e)
			{
				string mess = e.Message;
				if(!InternetGetConnectedState(0,0))
				{
					if(InternetAutodial(0,IntPtr.Zero))
					{
						ResetVars();
						isConnected = false;

						goto retry;
					}
					else
						StatusOutput.WriteError("Error: Please connect to the Internet");
				}
				else
					StatusOutput.WriteError("Error: "+e.Message);

				ResetVars();

				isConnected = false;
				return false;
			}

			isConnected = true;

			return true;
		}

		#region File Transfers

		private Socket transConn, dataConn;

		public string ReadTransportConnectionText()
		{
			StringBuilder data = new StringBuilder();

			long transferedBytes = 0;
			byte[] reciveBuffer = new byte[4096];

			int recivedBytes = transConn.Receive(reciveBuffer, 0, 4096, SocketFlags.None);
			transferedBytes += recivedBytes;

			while(recivedBytes != 0)
			{			
				data.Append(Encoding.Default.GetString(reciveBuffer, 0, recivedBytes));
				reciveBuffer = new byte[4096];
				recivedBytes = transConn.Receive(reciveBuffer, 0, 4096, SocketFlags.None);
				transferedBytes += recivedBytes;
			}
			return data.ToString();
		}

		public bool ReadTransportConnectionFile(string filePath,long startIndex)
		{
			try
			{
				FileStream data;

				if(!File.Exists(filePath))
					data = new FileStream(filePath,FileMode.Create);
				else
					data = new FileStream(filePath,FileMode.Append);

				DateTime start = DateTime.Now;

				long totalBytes = GetFileSize(lastReply);
				long transferedBytes = 0;
				if(startIndex != 0)
					transferedBytes = startIndex;
				
				byte[] reciveBuffer = new byte[4096];

				int recivedBytes = transConn.Receive(reciveBuffer, 0, 4096, SocketFlags.None);
				transferedBytes += recivedBytes;

				ConsoleColor.SetForeGroundColor(ConsoleColor.ForeGroundColor.Blue,true);
				StatusOutput.Write("\nStatus: Downloading...   ");
				float progress2 = 0;
				string progressText = "";

				while(recivedBytes != 0)
				{
					
					data.Write(reciveBuffer, 0, recivedBytes);
					reciveBuffer = new byte[4096];
					recivedBytes = transConn.Receive(reciveBuffer, 0, 4096, SocketFlags.None);
					transferedBytes += recivedBytes;

					#region Progress

					for(uint i = 0; i < progressText.Length; i++)
						Console.Write("\b");

					try
					{
						progress2 = (float)transferedBytes/totalBytes;
					}
					catch
					{
						progress2 = 1;
					}
					progress2 *= 100;
					progress2 = (int)progress2;

					progressText = (transferedBytes/1024).ToString()+"/"+(totalBytes/1024).ToString()+" KB   "+progress2.ToString()+"%";
					Console.Write(progressText);
					SetConsoleTitle(progressText);

					#endregion
				}

				data.Flush();
				data.Close();
				
				DateTime results = new DateTime(DateTime.Now.Subtract(start).Ticks);

				int seconds = results.Second;
				seconds += results.Minute*60;
				seconds += results.Hour*3600;

				if(seconds == 0)
					seconds = 1;

				float speed = transferedBytes/seconds;
				speed /= 1000;

				long localFileSize = new FileInfo(filePath).Length;

				SetConsoleTitle("DarkLight");

				ConsoleColor.SetForeGroundColor();

				StatusOutput.WriteInfo("\n\nInfo: Download Info ---------------------------------------------------");
				StatusOutput.WriteInfo("Info: - Download completed in "+results.ToLongTimeString()+" at speed of "+speed.ToString()+" KB/Sec");
				StatusOutput.WriteInfo("Info: - Recived "+transferedBytes.ToString()+", Server "+totalBytes.ToString()+", Local "+localFileSize.ToString()+" bytes\n");

				return true;
			}
			catch(Exception ex)
			{
				SetConsoleTitle("DarkLight");
				StatusOutput.WriteError("Error: "+ex.Message);
				return false;
			}
		}

		public bool WriteTransportConnectionFile(string localfile,string remotefile)
		{
			try
			{
				FileStream data = new FileStream(localfile,FileMode.Open);

				DateTime start = DateTime.Now;

				long totalBytes = data.Length;
				long transferedBytes = 0;
				byte[] sendBuffer = new byte[4096];

				int readBytes = data.Read(sendBuffer,0,4096);
				transferedBytes += readBytes;

				ConsoleColor.SetForeGroundColor(ConsoleColor.ForeGroundColor.Blue,true);
				StatusOutput.Write("Status: Uploading...     ");
				float progress2 = 0;
				string progressText = "";

				while(readBytes != 0)
				{
					transConn.Send(sendBuffer, 0, readBytes, SocketFlags.None);
					sendBuffer = new byte[4096];
					readBytes = data.Read(sendBuffer, 0, 4096);
					transferedBytes += readBytes;

					#region Progress

					for(uint i = 0; i < progressText.Length; i++)
						Console.Write("\b");

					try
					{
						progress2 = (float)transferedBytes/totalBytes;
					}
					catch
					{
						progress2 = 1;
					}
					progress2 *= 100;
					progress2 = (int)progress2;

					progressText = (transferedBytes/1024).ToString()+"/"+(totalBytes/1024).ToString()+" KB   "+progress2.ToString()+"%";
					Console.Write(progressText);
					SetConsoleTitle(progressText);

					#endregion
				}

				data.Flush();
				data.Close();
				
				DateTime results = new DateTime(DateTime.Now.Subtract(start).Ticks);

				int seconds = results.Second;
				seconds += results.Minute*60;
				seconds += results.Hour*3600;

				if(seconds == 0)
					seconds = 1;

				float speed = transferedBytes/seconds;
				speed /= 1000;

				SetConsoleTitle("DarkLight");

				ConsoleColor.SetForeGroundColor();

				StatusOutput.WriteInfo("\n\nInfo: Upload Info ---------------------------------------------");
				StatusOutput.WriteInfo("Info: - Upload Completed in "+results.ToLongTimeString()+" at speed of "+speed.ToString()+" KB/Sec");
				StatusOutput.WriteInfo("Info: - Sent file size is "+transferedBytes.ToString()+" bytes, Local is "+totalBytes.ToString()+" bytes\n");

				return true;
			}
			catch(Exception ex)
			{
				SetConsoleTitle("DarkLight");
				StatusOutput.WriteError("Error: "+ex.Message);
				return false;
			}
		}

		public long GetFileSize(string text)
		{
			long res = 0;

			int index = text.IndexOf("(");

			string sub = text.Substring(index+1);

			string[] sub2 = sub.Split(' ');

			res = long.Parse(sub2[0]);

			return res;
		}

		public void CreateDataConn()
		{
			IPHostEntry he = Dns.Resolve(Dns.GetHostName());
			IPEndPoint ep = new IPEndPoint(he.AddressList[0], 0);
			dataConn = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
			
			dataConn.Bind(ep);

			dataConn.Listen(5);
		}

		public string CreatePortParam()
		{
			string ip = Dns.GetHostName();
			IPHostEntry iphe = Dns.GetHostByName(ip);
			ip = iphe.AddressList[0].ToString();
			int index = 0;
			IEnumerator ie = ip.GetEnumerator();
			string[] addr = new string[4];
			while(index < 4)
			{
				ie.MoveNext();
				while(ie.Current.ToString() != ".")
				{
					addr[index] = addr[index] + ie.Current.ToString();
					if(ie.MoveNext()){}
					else break;
				}
				index++;
			}
			int port = ((IPEndPoint) dataConn.LocalEndPoint).Port;
			int portA = port/256;
			int portB = port - (portA * 256);
			return addr[0] + "," + addr[1] + "," + addr[2] + "," + addr[3] + "," + portA + "," + portB;
		}


		public void AcceptDataConnection()
		{
			transConn = dataConn.Accept();
		}

		public void CloseDataTransConn()
		{
			try{dataConn.Shutdown(SocketShutdown.Both); }
			catch(System.Exception){}

			try{dataConn.Shutdown(SocketShutdown.Both); }
			catch(System.Exception){}

			try{transConn.Close();}
			catch(System.Exception){}

			try{dataConn.Close();}
			catch(System.Exception){}

			transConn = null;
			dataConn  = null;
		}

		#endregion

		public bool LoginUser(string inUser)
		{
			int x = 0;

			while(true)
			{
				if(inUser == string.Empty || x >  0)
					inUser = AskUserName();

				x = 1;

				StatusOutput.WriteStatus("Status: Loging user "+inUser);

				if(!SendCommand(FtpCmds.USER.ToString(),inUser))
				{
					StatusOutput.WriteError("Login user faled, try again?");

					if(!Console.ReadLine().ToLower().Equals("y"))
						return false;
				}
				else
					break;
			}
			return true;
		}

		public bool LoginPass(string inPass)
		{
			int x = 0;

			while(true)
			{
				if(inPass == string.Empty || x >  0)
					inPass = AskUserPass();

				x = 1;

				StatusOutput.WriteStatus("Status: Sending password "+inPass);

				if(!SendCommand(FtpCmds.PASS.ToString(),inPass))
				{
					StatusOutput.WriteError("Login password incorrect, try again?");

					if(!Console.ReadLine().ToLower().Equals("y"))
						return false;
				}
				else
					break;
			}
			return true;
		}

		public bool GetServerList()
		{
			if(SendCommand(FtpCmds.LIST.ToString(),""))
				return true;
			else
				return false;
		}

		public void GetServerSystem()
		{
			SendCommand(FtpCmds.SYST.ToString(),"");
		}

		public bool SendCommand(string command, string param)
		{
			command = command.ToUpper();

			if(param != string.Empty)
				str_Data = command+" "+param+"\r\n";
			else
				str_Data = command+"\r\n";

			try
			{				
				byte_Data = System.Text.Encoding.ASCII.GetBytes(str_Data.ToCharArray());
				NetStrm.Write(byte_Data,0,byte_Data.Length);

				if(!GetServerStatus())
					return false;
			}
			catch(System.Exception e)
			{
				StatusOutput.WriteError("Error: "+e.Message);

				return false;
			}
			return true;
		}

		public bool DisconnectServer()
		{
			SendCommand(FtpCmds.QUIT.ToString(),"");

			NetStrm.Close();
			RdStrm.Close();

			ResetVars();

			isConnected = false;
			return true;
		}

		public bool RemoveDirectory(string path)
		{
			return SendCommand(FtpCmds.RMD.ToString(),path);
		}

		public bool CreateDirectory(string dirname)
		{
			return SendCommand(FtpCmds.MKD.ToString(),dirname);
		}

		public bool DeleteFile(string filename)
		{
			return SendCommand(FtpCmds.DELE.ToString(),filename);
		}

		public bool GetServerStatus()
		{
			return GetServerStatus(true);
		}

		public bool GetServerStatus(bool writeToScreen)
		{
			string newline = "";

			bool ret;

			while(true)
			{
				lastReply = RdStrm.ReadLine();

				if((int.Parse(lastReply.Substring(0,3)) > 400))
				{
					if(writeToScreen)
						StatusOutput.WriteServerError(serverName+": "+lastReply);

					ret = false;
				}
				else
				{
					if(writeToScreen)
						StatusOutput.WriteServerStatus(serverName+": "+lastReply);

					ret = true;
				}

				if(lastReply.Substring(3,1).Equals("-") && newline.Length == 0)
				{
					newline = lastReply.Substring(0,3);
					newline += " ";
				}
				else if(newline.Length > 0 && newline.CompareTo(lastReply.Substring(0,4)) == 0)
					break;
				else if(newline.Length == 0)
					break;
			}

			return ret;
		}

		public string GetTimeStamp()
		{
			System.DateTime time = System.DateTime.Now;

			return time.ToLongTimeString();
		}

		public void GetCurrentDir()
		{
			SendCommand(FtpCmds.PWD.ToString(),"");
		}

		public bool SetCurrentDir(string path)
		{
			return SendCommand(FtpCmds.CWD.ToString(),path);
		}

		#region Asking

		public bool AskFileContinue(string file)
		{
			StatusOutput.WriteInfo("A file of the same name was found on the system, do you want to continue the previus transfer? (Y/N)");
			if(Console.ReadLine().ToLower().Equals("y"))
				return true;
			else
				return false;
		}

		public string AskServerName()
		{
			StatusOutput.Write("Enter Server Name: ");
			return Console.ReadLine();
		}

		public string AskUserName()
		{
			StatusOutput.Write("Enter User Name: ");
			string str = Console.ReadLine();

			if(str == string.Empty)
				str = defaultUser;

			lastUserName = str;

			return str;
		}

		public string AskUserPass()
		{
			StatusOutput.Write("Enter Password: ");

			string str = Console.ReadLine();

			if(str == string.Empty)
				str = defaultPass;

			lastPassword = str;

			return str;
		}

		public string AskMessageNumber()
		{
			StatusOutput.Write("Enter Message Number: ");
			return Console.ReadLine();
		}

		public string AskFilePath()
		{
			StatusOutput.Write("Enter File Path: ");
			return Console.ReadLine();
		}

		public int AskForPort()
		{
			StatusOutput.Write("Enter Port: ");

			int ret = -1;

			string str_ret = Console.ReadLine();;
			try
			{
				ret = Convert.ToInt32(str_ret);
			}
			catch(System.Exception e)
			{
				StatusOutput.WriteLn("Error: "+e.Message);
			}

			return ret;
		}

		#endregion

		#region Show Legand

		public void show_Legand()
		{
			ConsoleColor.SetForeGroundColor(ConsoleColor.ForeGroundColor.Cyan);
			Console.WriteLine("\n\"connect <server name>\"           - connects to specified FTP server");
			System.Threading.Thread.Sleep(10);
			Console.WriteLine("\"disconnect\"                      - disconnects from current server");
			System.Threading.Thread.Sleep(10);
			Console.WriteLine("\"quit\" or \"exit\"                  - closes this application");
			System.Threading.Thread.Sleep(10);
			Console.WriteLine("\"status\"                          - displays connection status");
			System.Threading.Thread.Sleep(10);
			Console.WriteLine("\"dir\"                             - displays list of files in current directory");
			System.Threading.Thread.Sleep(10);
			Console.WriteLine("\"cd\"                              - displays current directory name");
			System.Threading.Thread.Sleep(10);
			Console.WriteLine("\"cd <directory name>\"             - moves to specified  directory");
			System.Threading.Thread.Sleep(10);
			Console.WriteLine("\"mkd\"                             - creates a directori");
			System.Threading.Thread.Sleep(10);
			Console.WriteLine("\"rmd\"                             - removes specified directory");
			System.Threading.Thread.Sleep(10);
			Console.WriteLine("\"del <file name>\"                 - removes specified file");
			System.Threading.Thread.Sleep(10);
			Console.WriteLine("\"port <port number>\"              - sets port number");
			System.Threading.Thread.Sleep(10);
			Console.WriteLine("\"user <user name>\"                - sets default user name");
			System.Threading.Thread.Sleep(10);
			Console.WriteLine("\"pass <password>\"                 - sets default password");
			System.Threading.Thread.Sleep(10);
			Console.WriteLine("\"serverip <server>\"               - displays specified servers IP");
			System.Threading.Thread.Sleep(10);
			Console.WriteLine("\"serversys\"                       - displays servers OS");
			System.Threading.Thread.Sleep(10);
			Console.WriteLine("\"login <user> <pass>\"             - begins login procedure");
			System.Threading.Thread.Sleep(10);
			Console.WriteLine("\"dnload <serverfile> <localfile>\" - begins download procedure");
			System.Threading.Thread.Sleep(10);
			Console.WriteLine("\"upload <localfile> <serverfile>\" - begins upload procedure");
			ConsoleColor.SetForeGroundColor();

			StatusOutput.WriteInfo("");
		}

		public void show_ServerCmd()
		{
			StatusOutput.WriteInfo("");
			StatusOutput.WriteInfo("NULL,         PWD,");
			System.Threading.Thread.Sleep(10);
			StatusOutput.WriteInfo("CWD,          QUIT,");
			System.Threading.Thread.Sleep(10);
			StatusOutput.WriteInfo("DELE,         RNTO,");
			System.Threading.Thread.Sleep(10);
			StatusOutput.WriteInfo("PASS,         APPE,");
			System.Threading.Thread.Sleep(10);
			StatusOutput.WriteInfo("PORT,         RNFR,");
			System.Threading.Thread.Sleep(10);
			StatusOutput.WriteInfo("RETR,         RMD,");
			System.Threading.Thread.Sleep(10);
			StatusOutput.WriteInfo("STOR,         MKD,");
			System.Threading.Thread.Sleep(10);
			StatusOutput.WriteInfo("TYPE,         REST,");
			System.Threading.Thread.Sleep(10);
			StatusOutput.WriteInfo("USER,         PASV,");
			System.Threading.Thread.Sleep(10);
			StatusOutput.WriteInfo("ABOR,         SYST,");
			System.Threading.Thread.Sleep(10);
			StatusOutput.WriteInfo("LIST,         NOOP,");
			StatusOutput.WriteInfo("");
		}

		#endregion

		#region FTP Naredbe
		/// <summary>
		/// Sadrži opis svih FTP Server naredbi
		/// </summary>
		public enum  FtpCmds
		{
			/// <summary>
			/// does nothing.
			/// </summary>
			NULL,

			/// <summary>
			/// Changes the current directory
			/// </summary>
			/// <remarks>Reply codes: 250 500 501 502 421 530 550</remarks>
			CWD,

			/// <summary>
			/// Causes the file specified in the pathname to be deleted at the server site
			/// </summary>
			/// <remarks>Reply codes: 250 450 550 500 501 502 421 530</remarks>
			DELE,

			/// <summary>
			/// send the user's password
			/// </summary>
			/// <remarks>Reply codes: 230 202 530 500 501 503 421 332</remarks>
			PASS,

			/// <summary>
			/// Tells the server to connect to the IP and port.
			/// </summary>
			/// <remarks>Reply codes: 200 500 501 421 530</remarks>
			PORT,

			/// <summary>
			/// Causes the server-DTP to transfer a copy of the file, specified 
			/// in the pathname, to the server- or user-DTP at the other end of the data 
			/// connection. The status and contents of the file at the server site shall be unaffected.
			/// </summary>
			/// <remarks>Reply codes: 125 150 110 226 250 425 426 451 450 550 500 501 421 530</remarks>
			RETR,

			/// <summary>
			/// Causes the server-DTP to accept the data transferred via the data connection 
			/// and to store the data as a file at the server site. If the file specified in 
			/// the pathname exists at the server site, then its contents shall be replaced 
			/// by the data being transferred. A new file is created at the server site if 
			/// the file specified in the pathname does not already exist.
			/// </summary>
			/// <remarks>Reply codes: 125 150 110 226 250 425 426 451 551 552 532 450 452 553 500 501 421 530</remarks>
			STOR,

			/// <summary>
			/// Sends the representation type to be used for data control transfers
			/// </summary>
			/// <remarks>Reply codes: 200 500 501 504 421 530</remarks>
			TYPE,

			/// <summary> 
			/// Sends a user name
			/// </summary>
			/// <remarks>Reply codes: 230 530 500 501 421 331 332</remarks>
			USER,

			/// <summary>
			/// Tells the server to abort the previous FTP service command and any associated 
			/// transfer of data. The abort command may require "special action", as discussed in the 
			/// Section on FTP Commands, to force recognition by the server. No action is to be taken 
			/// if the previous command has been completed (including data transfer). The control 
			/// connection is not to be closed by the server, but the data connection must be closed.
			/// </summary>
			/// <remarks>Reply codes: 225 226 500 501 502 421</remarks>
			ABOR,

			/// <summary>
			/// Causes a list to be sent from the server to the passive DTP. If 
			/// the pathname specifies a directory or other group of files, the server should 
			/// transfer a list of files in the specified directory. If the pathname specifies 
			/// a file then the server should send current information on the file. A null 
			/// argument implies the user's current working or default directory.
			/// </summary>
			/// <remarks>Reply codes: 125 150 226 250 425 426 451 450 500 501 502 421 530</remarks>
			LIST,

			/// <summary>
			/// Causes the name of the current working directory to be returned in the reply.
			/// </summary>
			/// <remarks>Reply codes: 257 500 501 502 421 550</remarks>
			PWD,

			/// <summary>
			/// Terminates a USER and if file transfer is not in progress, the server 
			/// closes the control connection. If file transfer is in progress, the connection will 
			/// remain open for result response and the server will then close it.
			/// </summary>
			/// <remarks>Reply codes: 221 500</remarks>
			QUIT,

			/// <summary>
			/// Specifies the new pathname of the file specified in the immediately 
			/// preceding "rename from" command. Together the two commands cause a file to be renamed.
			/// </summary>
			/// <remarks>Reply codes: 250 532 553 500 501 502 503 421 530</remarks>
			RNTO,

			/// <summary>
			/// Causes the server-DTP to accept the data transferred via the data 
			/// connection and to store the data in a file at the server site. If the file 
			/// specified in the pathname exists at the server site, then the data shall be 
			/// appended to that file; otherwise the file specified in the pathname shall be 
			/// created at the server site.
			/// </summary>
			/// <remarks>Reply codes: 125 150 110 226 250 425 426 451 551 552 532 450 550 452 
			/// 553 500 501 502 421 530</remarks>
			APPE,

			/// <summary>
			/// Specifies the old pathname of the file which is to be renamed. This 
			/// command must be immediately followed by a "rename to" command specifying the new 
			/// file pathname.
			/// </summary>
			/// <remarks>Reply codes: 450 550 500 501 502 421 530 350</remarks>
			RNFR,

			/// <summary>
			/// Causes the directory specified in the pathname to be removed as 
			/// a directory (if the pathname is absolute) or as a subdirectory of the current 
			/// working directory (if the pathname is relative).
			/// </summary>
			/// <remarks>Reply codes: 250 500 501 502 421 530 550</remarks>
			RMD,

			/// <summary>
			/// Causes the directory specified in the pathname to be created as 
			/// a directory (if the pathname is absolute) or as a subdirectory of the current 
			/// working directory (if the pathname is relative).
			/// </summary>
			/// <remarks>Reply codes: 257 500 501 502 421 530 550</remarks>
			MKD,

			/// <summary>
			/// The argument field represents the server marker at which file transfer is to be
			/// restarted. This command does not cause file transfer but skips over the file to
			/// the specified data checkpoint.
			/// </summary>
			/// <remarks>Reply codes: 500 501 502 421 530 350</remarks>
			REST,

			/// <summary>
			/// Requests the server-DTP to "listen" on a data port (which is not
			/// its default data port) and to wait for a connection rather than initiate one
			/// upon receipt of a transfer command. The response to this command includes the 
			/// host and port address this server is listening on. 
			/// </summary>
			/// <remarks>Reply codes: 227 500 501 502 421 530</remarks>
			PASV,

			/// <summary>
			/// Finds out the type of operating system at the server.
			/// </summary>
			/// <remarks>Reply codes: 215 500 501 502 421</remarks>
			SYST,

			/// <summary>
			/// This command does not affect any parameters or previously entered commands. 
			/// It specifies no action other than that the server send an OK reply.
			/// </summary>
			/// <remarks>Reply codes: 200 500 421</remarks>
			NOOP
		}
		#endregion

		#region FTP Povratne naredbe
		public enum FTP_Return_Codes
		{
			//500 Series: The command was not accepted and the requested action did not take place.

			/// <summary>
			/// Syntax error, command unrecognized. This may include errors such as command line too long. 
			/// </summary>
			R500,
			/// <summary>
			/// Syntax error in parameters or arguments. 
			/// </summary>
			R501, 
			/// <summary>
			///  Command not implemented. 
			/// </summary>
			R502,
			/// <summary>
			/// Bad sequence of commands. 
			/// </summary>
			R503, 
			/// <summary>
			/// Command not implemented for that parameter. 
			/// </summary>
			R504,
			/// <summary>
			/// Not logged in.
			/// </summary>
			R530,  
			/// <summary>
			/// Need account for storing files.
			/// </summary>
			R532, 
			/// <summary>
			/// Requested action not taken. File unavailable (e.g., file not found, no access). 
			/// </summary>
			R550, 
			/// <summary>
			/// Requested action aborted. Page type unknown.
			/// </summary>
			R551, 
			/// <summary>
			/// Requested file action aborted. Exceeded storage allocation (for current directory or dataset). 
			/// </summary>
			R552, 
			/// <summary>
			/// Requested action not taken. File name not allowed.
			/// </summary>
			R553, 
 
			//400 Series: The command was not accepted and the requested action did not take place, but the error condition is temporary and the action may be requested again. 
       
			/// <summary>
			/// Service not available, closing control connection.This may be a reply to any command if the service knows it must shut down. 
			/// </summary>
			R421, 
			/// <summary>
			/// Can't open data connection. 
			/// </summary>
			R425, 
			/// <summary>
			/// Connection closed; transfer aborted. 
			/// </summary>
			R426, 
			/// <summary>
			/// Requested file action not taken.
			/// </summary>
			R450, 
			/// <summary>
			/// Requested action aborted. Local error in processing.
			/// </summary>
			R451,  
			/// <summary>
			/// Requested action not taken. Insufficient storage space in system.File unavailable (e.g., file busy). 
			/// </summary>
			R452, 
			/// <summary>
			/// Series: The command has been accepted, but the requested action is dormant, pending receipt of further information. 
			/// </summary>
			R300, 
			/// <summary>
			/// User name okay, need password. 
			/// </summary>
			R331, 
			/// <summary>
			/// Need account for login.
			/// </summary>
			R332,  
			/// <summary>
			/// Requested file action pending further information.
			/// </summary>
			R350,  

			//200 Series: The requested action has been successfully completed. 

			/// <summary>
			/// Command okay.
			/// </summary>
			R200,  
			/// <summary>
			/// Command not implemented, superfluous at this site. 
			/// </summary>
			R202, 
			/// <summary>
			/// System status, or system help reply.
			/// </summary>
			R211,  
			/// <summary>
			/// Directory status.
			/// </summary>
			R212,  
			/// <summary>
			/// File status.
			/// </summary>
			R213,  
			/// <summary>
			/// Help message.On how to use the server or the meaning of a particular non-standard command. This reply is useful only to the human user. 
			/// </summary>
			R214, 
			/// <summary>
			/// NAME system type. Where NAME is an official system name from the list in the Assigned Numbers document. 
			/// </summary>
			R215, 
			/// <summary>
			/// Service ready for new user. 
			/// </summary>
			R220, 
			/// <summary>
			/// Service closing control connection.
			/// </summary>
			R221,  
			/// <summary>
			/// Data connection open; no transfer in progress.
			/// </summary>
			R225, 
			/// <summary>
			/// Closing data connection. Requested file action successful (for example, file transfer or file abort). 
			/// </summary>
			R226, 
			/// <summary>
			/// Entering Passive Mode (h1,h2,h3,h4,p1,p2).
			/// </summary>
			R227,  
			/// <summary>
			/// User logged in, proceed. Logged out if appropriate. 
			/// </summary>
			R230, 
			/// <summary>
			/// Requested file action okay, completed. 
			/// </summary>
			R250, 
			/// <summary>
			/// "PATHNAME" created.
			/// </summary>
			R257,  

			//100 Series: The requested action is being initiated, expect another reply before proceeding with a new command.

			/// <summary>
			/// Restart marker reply. In this case, the text is exact and not left to the particular implementation; it must read: MARK yyyy = mmmm where yyyy is User-process data stream marker, and mmmm server's equivalent marker (note the spaces between markers and "=").  
			/// </summary>
			R110, 
			/// <summary>
			/// Service ready in nnn minutes. 
			/// </summary>
			R120, 
			/// <summary>
			/// Data connection already open; transfer starting.
			/// </summary>
			R125,  
			/// <summary>
			/// File status okay; about to open data connection. 
			/// </summary>
			R150, 
		}
		#endregion

		#region Platform Invoke

		[DllImport("Kernel32.dll")]
		private static extern bool SetConsoleTitle([MarshalAs(UnmanagedType.LPStr)]string title);

		[DllImport("Wininet.dll")]
		private static extern bool InternetGetConnectedState(ulong flags,ulong reserved);

		[DllImport("Wininet.dll")]
		private static extern bool InternetGoOnline([MarshalAs(UnmanagedType.LPTStr)]string adress,IntPtr hwnd,uint reserved);
	
		[DllImport("Wininet.dll")]
		private static extern ulong InternetDial(
			IntPtr hwndParent,
			[MarshalAs(UnmanagedType.LPTStr)]string lpszConnectoid,
			ulong dwFlags,
			ref ulong lpdwConnection,
			ulong dwReserved
		);

		[DllImport("Wininet.dll")]
		private static extern bool InternetAutodial(ulong flags,IntPtr hwnd);

		#endregion
	}
}
