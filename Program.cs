using System;
using System.Collections.Generic;
using System.Text;
using System.Net;
using System.IO;

namespace DarkLight
{
    class Program
    {
        /// <summary>
        /// Glavna funkcija programa
        /// </summary>
        static void Main(string[] args)
        {
            System.Threading.Mutex mutex = new System.Threading.Mutex(false, "FTPClient");
            bool NotFirstInstance = !mutex.WaitOne(0, false);
            if (NotFirstInstance)
            {
                System.Windows.Forms.MessageBox.Show("FTP Client je veæ pokrenut!", "FTP Client already running!");
                return;
            }

            FTP_Client ftpc = new FTP_Client();

            string inServer = null;

            string command = null;
            string[] cmd = null;

            while (true)
            {
                StatusOutput.Write("Cmd: ");
                command = Console.ReadLine();

                cmd = command.Split(' ');

                switch (cmd[0].ToLower())
                {
                    case "connect":
                        {
                            if (ftpc.isConnected)
                            {
                                StatusOutput.WriteError("Error:\n\t Already connected!!!");
                                break;
                            }

                            if (!(cmd.Length > 1))
                                inServer = ftpc.AskServerName();
                            else
                                inServer = cmd[1];

                            StatusOutput.WriteStatus("Status: Connecting to " + inServer + " on port " + ftpc.serverPort.ToString() + ". Please wait...");

                            ftpc.ConnectServer(inServer);

                            if (ftpc.isConnected)
                                StatusOutput.WriteStatus("Status: Connected to " + inServer + ",type \"login\" to login to the server");

                            break;
                        }
                    case "disconnect":
                        {
                            if (ftpc.isConnected)
                                ftpc.DisconnectServer();
                            else
                                StatusOutput.WriteError("Error: Not connected!!!");
                            break;
                        }
                    case "exit":
                        {
                            goto case "quit";
                        }
                    case "quit":
                        {
                            if (ftpc.isConnected)
                                ftpc.DisconnectServer();
                            StatusOutput.WriteStatus("Status: Exiting FTP Client...");
                            System.Threading.Thread.Sleep(200);
                            return;
                        }
                    case "status":
                        {
                            string stat = null;
                            if (ftpc.isConnected)
                                stat = "Connected to " + inServer;
                            else
                                stat = "Connection inactive";
                            StatusOutput.WriteStatus("Status: " + stat);
                            break;
                        }
                    case "help":
                        {
                            ftpc.show_Legand();
                            break;
                        }
                    case "-":
                        {
                            if (!ftpc.isConnected)
                            {
                                StatusOutput.WriteError("Error: Not connected!!!");
                                break;
                            }

                            string cmd1, cmd2;

                            if (!(cmd.Length > 1))
                            {
                                StatusOutput.Write("Enter Server Command: ");
                                cmd1 = Console.ReadLine();
                            }
                            else
                                cmd1 = cmd[1];

                            if (!(cmd.Length > 2))
                            {
                                StatusOutput.Write("Enter Command Parameter: ");
                                cmd2 = Console.ReadLine();
                            }
                            else
                                cmd2 = cmd[2];

                            StatusOutput.WriteStatus("Status: Sending command \"" + cmd1 + "\" to server");
                            ftpc.SendCommand(cmd1, cmd2);
                            break;
                        }
                    case "port":
                        {
                            if (cmd.Length > 1)
                            {
                                int temp = -1234;
                                try
                                {
                                    temp = Convert.ToInt32(cmd[1]);
                                }
                                catch
                                {
                                    temp = -1234;
                                }
                                if (temp != -1234 && temp >= 0)
                                    ftpc.serverPort = temp;
                            }
                            StatusOutput.WriteInfo("Info: Current port is " + ftpc.serverPort.ToString() + ", default is 21");
                            break;
                        }
                    case "helpcmd":
                        {
                            ftpc.show_ServerCmd();
                            break;
                        }
                    case "login":
                        {
                            if (!ftpc.isConnected)
                            {
                                StatusOutput.WriteError("Error: Not Connected!!!");
                                break;
                            }

                            if (cmd.Length > 1)
                                ftpc.LoginUser(cmd[1]);
                            else
                                ftpc.LoginUser("");

                            if (cmd.Length > 2)
                                ftpc.LoginPass(cmd[2]);
                            else
                                ftpc.LoginPass("");

                            break;
                        }
                    case "about":
                        {
                            StatusOutput.WriteInfo("");
                            StatusOutput.WriteInfo("Product: FTP Client");
                            StatusOutput.WriteInfo("Company: miha software");
                            StatusOutput.WriteInfo("Author: Marko Mihoviliæ");
                            StatusOutput.WriteInfo("Version: 0.9.8.8");
                            StatusOutput.WriteInfo("");
                            break;
                        }
                    case "user":
                        {
                            if (cmd.Length > 1)
                                ftpc.defaultUser = cmd[1];

                            StatusOutput.WriteInfo("Info: Curent default user is " + ftpc.defaultUser + ", default is \"anonymus\"");
                            break;
                        }
                    case "pass":
                        {
                            if (cmd.Length > 1)
                                ftpc.defaultPass = cmd[1];

                            StatusOutput.WriteInfo("Info: Curent default password is " + ftpc.defaultPass + ", default is \"anonymus@something.com\"");
                            break;
                        }
                    case "serverip":
                        {
                            string ser = inServer;

                            if (cmd.Length > 1)
                                ser = cmd[1];

                            try
                            {
                                StatusOutput.WriteInfo("Info: " + ser + " IP is " + Dns.GetHostByName(ser).AddressList[0].ToString());
                            }
                            catch (Exception ex)
                            {
                                StatusOutput.WriteError("Error: " + ex.Message);
                            }
                            break;
                        }
                    case "cd":
                        {
                            if (!ftpc.isConnected)
                            {
                                StatusOutput.WriteError("Error: Not Connected!!!");
                                break;
                            }
                            if (!(cmd.Length > 1))
                            {
                                ftpc.GetCurrentDir();
                            }
                            else
                            {
                                ftpc.SetCurrentDir(cmd[1]);
                            }
                            break;
                        }
                    case "dir":
                        {
                            if (!ftpc.isConnected)
                            {
                                StatusOutput.WriteError("Error: Not Connected!!!");
                                break;
                            }

                            string param = "";

                            if (cmd.Length > 1)
                                param = cmd[1];

                            if (ftpc.SendCommand("TYPE", "A"))
                            {
                                ftpc.CreateDataConn();

                                if (ftpc.SendCommand("PORT", ftpc.CreatePortParam()))
                                {
                                    if (ftpc.SendCommand("LIST", param))
                                    {
                                        ftpc.AcceptDataConnection();

                                        StatusOutput.WriteServerStatus(">>");
                                        ConsoleColor.SetForeGroundColor(ConsoleColor.ForeGroundColor.Green, true);

                                        string[] text = ftpc.ReadTransportConnectionText().Split('\n');
                                        foreach (string str in text)
                                        {
                                            Console.WriteLine(str);
                                            System.Threading.Thread.Sleep(10);
                                        }

                                        ConsoleColor.SetForeGroundColor();
                                        StatusOutput.WriteServerStatus("<<");
                                        ftpc.CloseDataTransConn();

                                        ftpc.GetServerStatus();
                                    }
                                }
                            }

                            break;
                        }
                    case "rmd":
                        {
                            if (!ftpc.isConnected)
                            {
                                StatusOutput.WriteError("Error: Not Connected!!!");
                                break;
                            }

                            if (!(cmd.Length > 1))
                            {
                                StatusOutput.WriteInfo("Info: RMD <directory name>  - removes specified directory from server");
                                break;
                            }

                            ftpc.RemoveDirectory(cmd[1]);
                            break;
                        }
                    case "mkd":
                        {
                            if (!ftpc.isConnected)
                            {
                                StatusOutput.WriteError("Error: Not Connected!!!");
                                break;
                            }

                            if (!(cmd.Length > 1))
                            {
                                StatusOutput.WriteInfo("Info: MKD <directory name>  - creates specified directory on server");
                                break;
                            }

                            ftpc.CreateDirectory(cmd[1]);
                            break;
                        }
                    case "del":
                        {
                            if (!ftpc.isConnected)
                            {
                                StatusOutput.WriteError("Error: Not Connected!!!");
                                break;
                            }

                            string param = "";

                            if (!(cmd.Length > 1))
                            {
                                StatusOutput.Write("Enter File Name: ");
                                param = Console.ReadLine();
                            }
                            else
                                param = cmd[1];

                            ftpc.DeleteFile(param);
                            break;
                        }
                    case "serversys":
                        {
                            if (!ftpc.isConnected)
                            {
                                StatusOutput.WriteError("Error: Not Connected!!!");
                                break;
                            }

                            ftpc.GetServerSystem();
                            break;
                        }
                    case "dnload":
                        {
                            if (!ftpc.isConnected)
                            {
                                StatusOutput.WriteError("Error: Not Connected!!!");
                                break;
                            }

                            if (!(cmd.Length > 1))
                            {
                                StatusOutput.WriteInfo("Info: dnload <file name> <local file name>");
                                break;
                            }

                            string param = Path.Combine(ftpc.defaultDnloadPath, Path.GetFileName(cmd[1]));

                            if (cmd.Length > 2)
                            {
                                param = cmd[2];
                            }

                            if (ftpc.SendCommand("TYPE", "I"))
                            {
                                ftpc.CreateDataConn();

                                if (ftpc.SendCommand("PORT", ftpc.CreatePortParam()))
                                {
                                    if (ftpc.SendCommand("REST", "0"))
                                    {
                                        long index = 0;

                                        if (File.Exists(param))
                                        {
                                            if (ftpc.AskFileContinue(param))
                                            {
                                                FileStream file = File.Open(param, FileMode.Open);
                                                index = file.Length;
                                                file.Close();

                                                ftpc.SendCommand("REST", index.ToString());
                                            }
                                            else
                                                File.Delete(param);
                                        }

                                        if (ftpc.SendCommand("RETR", cmd[1]))
                                        {
                                            ftpc.AcceptDataConnection();

                                            ftpc.ReadTransportConnectionFile(param, index);
                                            ftpc.CloseDataTransConn();
                                            ftpc.GetServerStatus();
                                        }
                                    }
                                }
                            }

                            break;
                        }
                    case "upload":
                        {
                            if (!ftpc.isConnected)
                            {
                                StatusOutput.WriteError("Error: Not Connected!!!");
                                break;
                            }

                            string locfile = "";
                            string remotefile = "";

                            if (!(cmd.Length > 1))
                            {
                                StatusOutput.Write("Entre local file path: ");
                                locfile = Console.ReadLine();
                            }
                            else
                                locfile = cmd[1];

                            if (cmd.Length > 2)
                            {
                                remotefile = cmd[2];
                            }
                            else
                                remotefile = Path.GetFileName(locfile);

                            if (ftpc.SendCommand("TYPE", "I"))
                            {
                                ftpc.CreateDataConn();

                                if (ftpc.SendCommand("PORT", ftpc.CreatePortParam()))
                                {
                                    if (ftpc.SendCommand("REST", "0"))
                                    {
                                        if (ftpc.SendCommand("STOR", remotefile))
                                        {
                                            ftpc.AcceptDataConnection();

                                            ftpc.WriteTransportConnectionFile(locfile, remotefile);
                                            ftpc.CloseDataTransConn();
                                            ftpc.GetServerStatus();
                                        }
                                        else if (ftpc.SendCommand("APPE", remotefile))
                                        {
                                            ftpc.AcceptDataConnection();

                                            ftpc.WriteTransportConnectionFile(locfile, remotefile);
                                            ftpc.CloseDataTransConn();
                                            ftpc.GetServerStatus();
                                        }
                                    }
                                }
                            }

                            break;
                        }
                    case "dnloadpath":
                        {
                            if (cmd.Length > 1)
                            {
                                try
                                {
                                    Directory.CreateDirectory(cmd[1]);
                                    Directory.Delete(cmd[1]);
                                    ftpc.defaultDnloadPath = cmd[1];
                                }
                                catch
                                {
                                    StatusOutput.WriteError("Error: Not a valid path!!!");
                                }
                            }
                            StatusOutput.WriteInfo("Info: Default download path is " + ftpc.defaultDnloadPath);
                            break;
                        }
                    case "keepalive":
                        {
                            if (ftpc.isConnected)
                            {
                                if (cmd.Length > 1)
                                {
                                    bool temp;
                                    try
                                    {
                                        temp = Convert.ToBoolean(int.Parse(cmd[1]));
                                        ftpc.KeepAlive = temp;
                                    }
                                    catch
                                    {
                                    }
                                }

                                StatusOutput.WriteInfo("Info: Keep Alive is " + ftpc.KeepAlive.ToString() + ", default is False");
                            }
                            else
                                StatusOutput.WriteError("Error: Not Connected!!!");
                            break;
                        }
                    case "":
                        {
                            break;
                        }
                    default:
                        {
                            StatusOutput.WriteError("Error: Unknown command. Use \"help\" for list of commands.");
                            break;
                        }
                }
            }
        }
    }
}
