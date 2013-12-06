/*
 * Created by SharpDevelop.
 * User: Freddie Nash
 * Date: 06/04/2012
 * Time: 11:49
 * 
 * To change this template use Tools | Options | Coding | Edit Standard Headers.
 */
using System;
using socks = System.Net.Sockets;
using System.Collections.Generic;

/*
 * + is con commant
 * - means send to room
 * >  means send to shared con
 * / for IRCness
 */

namespace minirelay
{
	class Program
	{
		public static bool disableAuth = true;
		
		public class room
		{
			public List<con> cons = new List<con>();
			public List<con> remCons = new List<con>();
			public string name;
			
			public room(string nameN)
			{
				name = nameN;
			}
			
			/// <summary>  NO USE  </summary>
			public void mainloop()
			{
				while (cons.Count > 0)
				{
					doStuff();
					System.Threading.Thread.Sleep(100);
				}
			}
			
			public void doStuff()
			{
				lock (cons)
				{
					foreach (con c in cons)
					{
						lock (c.roomBuff)
						{
							if (c.roomBuff == "")
								continue;
							
							int nlIndex = c.roomBuff.IndexOf('\n');
							string temp = c.roomBuff.Substring(0, nlIndex);
							c.roomBuff = c.roomBuff.Substring(nlIndex + 1);
							
							// ">>" means ">"
							// "> " means send to share con
							string msg = temp;
							bool share = false;
							if (msg.StartsWith(">"))
							{
								if (msg.StartsWith(">>"))
									msg = msg.Substring(1);
								else if (msg.Substring(1, 1) == " ")
								{
									msg = msg.Substring(2);
									share = true;
									c.roomShareExec(msg);
								}
							}
							
							foreach (con oc in cons)
							{
								if (oc != c)
									writeConSrc(msg, oc, share);
							}
						}
					}
					foreach (con c in remCons)
						cons.Remove(c);
					remCons.Clear();
				}
			}
			
			public void writeConSrc(string msg, con c, bool share)
			{
				try
				{
					if (share)
					{
						c.writeLineSrc("~> " + msg);
						if (c.shareCon)
							c.roomShareExec(msg);
					}
					else
						c.writeLineSrc("~" + msg);
				}
				catch 
				{
					remCons.Add(c);
				}
			}
			
			public void join(con c)
			{
				lock (cons)
				{
					cons.Add(c);
				}
			}
			
			public void part(con c)
			{
				lock (cons)
				{
					cons.Remove(c);
				}
			}
		}
		
		public class route
		{
			public int trg;
			
			public route(int trgN)
			{
				trg = trgN;
			}
		}
		
		public class con
		{
			public enum conMode
			{
				none, charByChar, lineByLine, irc, multiCharByChar, multiLineByLine
			}
			
			public class crypt
			{
				public string name = "none";
				
				// returning barr is OK
				public virtual byte[] encrypt(byte[] barr)
				{
					return barr;
				}
				
				// returning barr is OK
				public virtual byte[] decrypt(byte[] barr)
				{
					return barr;
				}
				
				public virtual string shakemsg()
				{
					return "";
				}
				
				public virtual void shake(string shakemsg)
				{
				}
				
				public virtual void init()
				{
					
				}
			}
			
			public class lblCrypt : crypt
			{
				
			}
			
			public class cbcCrypt : crypt
			{
				public int blockSize = 1;
			}
			
			/// <summary>
			/// Awful encryption
			/// </summary>
			public class swapCrypt : lblCrypt
			{
				public override byte[] decrypt(byte[] barr)
				{
					byte temp;
					for (int i = 0; i < barr.Length - 1; i += 2)
					{
						temp = barr[i];
						barr[i] = barr[i + 1];
						barr[i + 1] = temp;
					}
					return barr;
				}
				
				public override byte[] encrypt(byte[] barr)
				{
					byte temp;
					for (int i = 0; i < barr.Length - 1; i += 2)
					{
						temp = barr[i];
						barr[i] = barr[i + 1];
						barr[i + 1] = temp;
					}
					return barr;
				}
			}
			
			/// <summary>
			/// Awful encryption
			/// </summary>
			public class blockMash : cbcCrypt
			{
				public override byte[] decrypt(byte[] barr)
				{
					byte temp;
					for (int i = 0; i < barr.Length - 1; i += 2)
					{
						temp = barr[i];
						barr[i] = barr[i + 1];
						barr[i + 1] = temp;
					}
					return barr;
				}
				
				public override byte[] encrypt(byte[] barr)
				{
					byte temp;
					for (int i = 0; i < barr.Length - 1; i += 2)
					{
						temp = barr[i];
						barr[i] = barr[i + 1];
						barr[i + 1] = temp;
					}
					return barr;
				}
			}
			
			/// <summary>
			/// Viable encryption
			/// </summary>
			public class rsaCrypt : crypt
			{
				public int keyLen = 1024; // must be uniform accross servers... I think
				
				public System.Security.Cryptography.RSACryptoServiceProvider rsaEnInst;
				public System.Security.Cryptography.RSACryptoServiceProvider rsaDecInst;
				public string enPuplicKey; // use to encrypt stuff to send to partner
				public string decPuplicKey; // send to partner
				
				public override void init()
				{
					initRsa();
				}
				
				public void initRsa()
				{
					rsaDecInst = new System.Security.Cryptography.RSACryptoServiceProvider(keyLen);
					decPuplicKey = rsaDecInst.ToXmlString(false);
				}
				
				public void initEncRsa()
				{
					rsaEnInst = new System.Security.Cryptography.RSACryptoServiceProvider();
					rsaEnInst.FromXmlString(enPuplicKey);
				}
				
				public override string shakemsg()
				{
					Console.WriteLine("Sent Key:" + decPuplicKey);
					return decPuplicKey;
				}
				
				public override void shake(string shakemsg)
				{
					enPuplicKey = shakemsg;
					Console.WriteLine("Recd Key:" + enPuplicKey);
					initEncRsa();
				}
				
				public override byte[] decrypt(byte[] barr)
				{
					byte[] decBarr = rsaDecInst.Decrypt(barr, true);
					return barr;
				}
				
				public override byte[] encrypt(byte[] barr)
				{
					byte[] enBarr = rsaEnInst.Encrypt(barr, true);
					return barr;
				}
			}
			
			public abstract class proto
			{
				public abstract bool connected();
				public abstract bool dataAvailable();
				public abstract void close();
				public abstract void writeByte(byte b);
				public abstract void write(byte[] bytes, int offset, int len);
				public abstract int readByte();
			}
			
			public class proto_null : proto
			{
				public proto_null()
				{
				}
				
				public override bool connected()
				{
					return true;
				}
				
				public override bool dataAvailable()
				{
					return false;
				}
				
				public override void close()
				{
				}
				
				public override void writeByte(byte b)
				{
				}
				
				public override void write(byte[] bytes, int offset, int len)
				{
				}
				
				public override int readByte()
				{
					return -1;
				}
			}
			
			public class proto_tcpClient : proto
			{
				private socks.TcpClient tcpClient;
				private socks.NetworkStream netStream;
				
				public proto_tcpClient(socks.TcpClient tcN)
				{
					tcpClient = tcN;
					netStream = tcN.GetStream();
				}
				
				public override bool connected()
				{
					return tcpClient.Connected;
				}
				
				public override bool dataAvailable()
				{
					return netStream.DataAvailable;
				}
				
				public override void close()
				{
					tcpClient.Close();
					netStream.Dispose();
				}
				
				public override void writeByte(byte b)
				{
					netStream.WriteByte(b);
				}
				
				public override void write(byte[] bytes, int offset, int len)
				{
					netStream.Write(bytes, offset, len);
				}
				
				public override int readByte()
				{
					return netStream.ReadByte();
				}
			}
			
			public Program inst;
			
			public int id;
			public List<byte> srcBuff = new List<byte>();
			public List<List<byte>> trgBuff = new List<List<byte>>();
		
			public proto src;
			public List<proto> trg = new List<proto>();
			
			public List<int> trgPort = new List<int>();
			public List<string> trgIp = new List<string>();
			public List<route[]> trgRoutes = new List<route[]>();
			public List<crypt> trgCrypt = new List<crypt>();
			public crypt srcCrypt = new crypt();
			
			public int sleepLen = 20;
			public int readTimeOut = 60000;
			public int maxRead = 100;
			
			public string lineTermSrc = "\n";
			public string lineTermTrg = "\n";
			public bool presBS = false;
			public static char[] frcTrimChars = new char[2] { '\r', '\n' };
			public static char[] trgTrimChars = new char[1] { '\r' };
			public static char[] srcTrimChars = new char[1] { '\r' };
			
			public byte nlByte = 10;
			public System.Text.Encoding srcEncoding = System.Text.Encoding.ASCII; // start off with ascii
			public System.Text.Encoding trgEncoding = System.Text.Encoding.ASCII; // start off with ascii
			
			public conMode mode = conMode.lineByLine; // default
			
			public bool looping = false;
			public bool frozen = false; // freeze target handling (stops input being taken) - useful for routing things which get stropy when they miss stuff
			
			System.Diagnostics.Stopwatch sw = new System.Diagnostics.Stopwatch();
			
			Random rnd = new Random();
			
			System.Collections.Generic.Dictionary<string, string> vars = new System.Collections.Generic.Dictionary<string, string>();
			
			// rooms
			public room curRoom;
			public string roomBuff;
			public bool shareCon;
			
			public con(socks.TcpClient tcon, int idN, Program instN)
			{
				bool noOpen = false;
				
				id = idN;
				inst = instN;
				src = new proto_tcpClient(tcon);
				
				string resp, resp1;
				int authInt0 = rnd.Next(100), authInt1 = rnd.Next(100);
				
				writeLineSrc("HELLO");
				readLineBlockSrc();
				
				resp = reqLineSrc("UID");
				resp1 = reqLineSrc("AUTH " + authInt0.ToString() + " " + authInt1.ToString());
				if (!inst.uids.ContainsKey(resp) || (int.Parse(resp1) != (authInt0 + inst.uids[resp]) * 2 + authInt1 + 1))
				{
					if (!disableAuth)
					{
						writeLineSrc("FAIL AUTH");
						close();
						return;
					}
				}
				
				while (!setMode(reqLineSrc("MODE")))
				{
					writeLineSrc("MODE UNSUPPORTED");
				}
				
				if (mode == conMode.none)
					goto none;
					
				createTrg(); // index 0
				
			moreIp:
				trgIp[0] = reqLineSrc("TRGIP");
				if (trgIp[0] == "")
				{
					if (mode == conMode.multiCharByChar || mode == conMode.multiLineByLine)
					{
						writeLineSrc("NO INITIAL SERVER");
						remTrg(0);
						noOpen = true;
						goto noServer;
					}
					else
					{
						writeLineSrc("PLEASE ENTER A HOSTNAME");
						goto moreIp;
					}
				}
				
				int tempPort;
				while (!int.TryParse(reqLineSrc("TRGPORT"), out tempPort))
				{
					writeLineSrc("TRGPORT MUST BE INT");
				}
				trgPort[0] = tempPort;
				
			noServer:
				
				resp = reqLineSrc("PRESBS");
				if (resp == "1")
					presBS = true;
				else
					presBS = false;
				
			askAgain:
				resp = reqLineSrc("START");
				if (resp != "1")
				{
					if (resp == "2")
					{
						openTrg();
						goto noNice;
					}
					exec(ref resp);
					// option for more config stuff here?
					goto askAgain;
				}
				
				if (!noOpen)
				{
					writeLineSrc("CONNECTING");
					openTrg();
					writeLineSrc("CONNECTED");
				}
				
			noNice:
				switch (mode)
				{
					case conMode.irc:
						ircInit();
						break;
				}
				
			none:
				varInit();
				looping = true;
				mainLoop();
			}
			
			public void varInit()
			{
				vars.Add("MPRE", "#: ");
			}
			
			public void ircInit()
			{
				vars.Add("NICK", reqLineSrc("NICK"));
				writeLineTrg("NICK " + vars["NICK"]);
				writeLineTrg("USER " + vars["NICK"].ToLower() + " \"tim32.org\" * :" + vars["NICK"].ToLower());
				
				lineTermSrc = "\r\n";
				lineTermTrg = "\r\n";
				
				srcEncoding = System.Text.Encoding.UTF8;
				trgEncoding = System.Text.Encoding.UTF8;
			}
			
			public void roomShareExec(string msg)
			{
				string temp = msg;
				switch (mode)
				{
					case conMode.lineByLine:
					{
						writeLineTrg(temp);
					}
					break;
				case conMode.multiLineByLine:
					{
						for (int i = 0; i < trg.Count; i++)
							writeLineTrg(i, temp);
					}
					break;
				case conMode.irc:
					{
						if (!ircExec(ref temp))
							writeLineTrg(temp);
					}
					break;
				}
			}
			
			public void mainLoop()
			{
				string temp;
				byte[] temp0;
				byte[] temp1;
				bool read = false;
				while (looping && check())
				{
					if (frozen)
					{
						temp = readLineSrc(out read);
						if (read && !exec(ref temp))
						{
							switch (mode)
							{
								case conMode.lineByLine:
									{
										writeTrg(temp);
									}
									break;
								case conMode.multiLineByLine:
									{
										for (int i = 0; i < trg.Count; i++)
											writeLineTrg(i, temp);
									}
									break;
								case conMode.irc:
									{
										writeTrg(temp);
									}
									break;
							}
							
							if (shareCon)
								writeLineRoom("<" + temp);
						}
						
						goto cont;
					}
					
					switch (mode)
					{
						case conMode.none:
							{
								temp = readLineSrc(out read);
								if (read && !exec(ref temp))
								{
									writeLineRoom(temp);
								}
							}
							break;
						case conMode.charByChar:
							{
								temp0 = readTrg();
								if (temp0.Length != 0)
								{
									writeSrc(temp0);
//									route(trgRead); // not multi, can't route
								}
								writeTrg(readSrc());
							}
							break;
						case conMode.multiCharByChar:
							{
								temp1 = readSrc();
								for (int i = 0; i < trg.Count; i++)
								{
									temp0 = readTrg(i);
									if (temp0.Length != 0)
									{
										writeSrc(temp0);
										route(i, temp0);
									}
									writeTrg(i, temp1);
								}
							}
							break;
						case conMode.lineByLine:
							{
								temp = readLineTrg(out read);
								if (read)
								{
									writeLineSrc(temp);
//									routeLine(temp); // not multi, can't route
								}
								
								temp = readLineSrc(out read);
								if (read && !exec(ref temp))
								{
									writeLineTrg(temp);
									if (shareCon)
										writeLineRoom("<" + temp);
								}
							}
							break;
						case conMode.multiLineByLine:
							{
								temp = readLineSrc(out read);
								if (read && !exec(ref temp))
								{
									for (int i = 0; i < trg.Count; i++)
										writeLineTrg(i, temp);
								}
								
								for (int i = 0; i < trg.Count; i++)
								{
									temp = readLineTrg(i, out read);
									if (read)
									{
										routeLine(i, temp); // not multi, can't route
										temp = vars["MPRE"].Replace("#", i.ToString()) + temp;
										writeLineSrc(temp);
										if (shareCon)
											writeLineRoom("<" + temp);
									}
								}
							}
							break;
						case conMode.irc:
							{
								temp = readLineTrg(out read);
								if (read)
								{
									if (temp.StartsWith("PING :"))
									{
										temp = forceClean(temp);
										writeLineTrg("PONG :" + temp.Substring(6));
									}
									else
									{
										try { ircCollect(forceClean(temp)); }
										catch
										{ /*bad message from server*/
											writeLineSrc("BMFS!!!");
											writeLineRoom("BMFS!!!");
										}
										writeLineSrc(temp);
										if (shareCon)
											writeLineRoom("<" + temp);
									}
								}
								
								temp = readLineSrc(out read);
								if (read && !exec(ref temp) && !ircExec(ref temp))
								{
									writeLineTrg(temp);
								}
							}
							break;
					}
					
				cont:
					System.Threading.Thread.Sleep(sleepLen);
				}
			}
			
			public bool createCrypt(string cryptName, ref crypt cpt)
			{
				switch (cryptName)
				{
					case "none":
						cpt = new crypt();
						cpt.init();
						return true;
					case "swap":
						cpt = new swapCrypt();
						cpt.init();
						return true;
					case "rsa":
						cpt = new rsaCrypt();
						cpt.init();
						return true;
				}
				return false;
			}
			
			public bool setEncoding(string encodingString, ref System.Text.Encoding enc)
			{
				switch (encodingString)
				{
					case "ascii":
						enc = System.Text.Encoding.ASCII;
						break;
					case "utf8":
						enc = System.Text.Encoding.UTF8;
						break;
					case "utf16":
						enc = System.Text.Encoding.Unicode;
						break;
					default:
						try
						{
							enc = System.Text.Encoding.GetEncoding(encodingString);
						}
						catch
						{
							return false;
						}
						break;
				}
				return true;
			}
			
			public bool setMode(string modeString)
			{
				switch (modeString)
				{
					case "cbc":
						mode = conMode.charByChar;
						break;
					case "none":
						mode = conMode.none;
						break;
					case "mcbc":
						mode = conMode.multiCharByChar;
						break;
					case "":
					case "lbl":
						mode = conMode.lineByLine;
						break;
					case "mlbl":
						mode = conMode.multiLineByLine;
						break;
					case "irc":
						mode = conMode.irc;
						
						if (!vars.ContainsKey("CURCHANNEL"))
							vars.Add("CURCHANNEL", ""); // define
						if (!vars.ContainsKey("CHANNELS"))
							vars.Add("CHANNELS", "");
						
						break;
					default:
						Console.WriteLine("fail");
						return false;
				}
				return true;
			}
			
			public void route(string msg)
			{
				route(0, msg);
			}
			
			public void route(int index, string msg)
			{
				if (trgRoutes[index] != null)
				{
					for (int i = trgRoutes[index].Length - 1; i >= 0; i--)
						writeTrg(trgRoutes[index][i].trg, msg);
				}
			}
			
			public void route(int index, byte[] msg)
			{
				if (trgRoutes[index] != null)
				{
					for (int i = trgRoutes[index].Length - 1; i >= 0; i--)
						writeTrg(trgRoutes[index][i].trg, msg);
				}
			}
			
			public void routeLine(string msg)
			{
				routeLine(0, msg);
			}
			
			public void routeLine(int index, string msg)
			{
				if (trgRoutes[index] != null)
				{
					for (int i = trgRoutes[index].Length - 1; i >= 0; i--)
						writeLineTrg(trgRoutes[index][i].trg, msg);
				}
			}
			
			public bool exec(ref string msg)
			{
				int temp;
				crypt tempCrypt = null;
				if (msg.Length < 3)
					return false;
				if (msg[0] == '-')
				{
					if (msg[1] == '-')
					{
						msg = msg.Substring(1);
						return false;
					}
					else
					{
						msg = remBSs(msg.Substring(1));
						writeLineRoom(msg);
						return true;
					}
				}
				if (msg[0] == '+')
				{
					if (msg[1] == '+')
					{
						msg = msg.Substring(1);
						return false;
					}
					else
					{
						msg = forceClean(msg.Substring(1));
						string[] data = stringSplit(msg, ' ');
						switch (data[0].ToLower())
						{
							case "help":
								help(data);
								break;
							case "mode":
								if (!setMode(msg.Substring(5)))
									writeLineSrc("MODE UNSUPPORTED");
								break;
							case "srccrypt":
								if (!createCrypt(data[1], ref tempCrypt))
									writeLineSrc("Unsupported Crypt");
								else
								{
									// shake
									writeLineSrc(tempCrypt.shakemsg());
									bool fsr = false;
									string fsts = "";
									while (!fsr)
										fsts = readLineSrc(out fsr);
									tempCrypt.shake(fsts);
									// change to new crypt
									srcEncoding = null;
									srcCrypt = tempCrypt;
								}
								break;
							case "trgcrypt":
								if (!createCrypt(data[2], ref tempCrypt))
									writeLineSrc("Unsupported Crypt");
								else
								{
									writeLineTrg(int.Parse(data[1]), "+srccrypt " + data[2]); // tell the target to use the appropriate crypt
									// shake
									writeLineTrg(int.Parse(data[1]), tempCrypt.shakemsg());
									bool ftr = false;
									string ftts = "";
									while (!ftr)
										ftts = readLineTrg(int.Parse(data[1]), out ftr);
									tempCrypt.shake(ftts);
									// change to new crypt
									trgEncoding = null;
									trgCrypt[int.Parse(data[1])] = tempCrypt;
								}
								break;
							case "srcencoding":
								if (setEncoding(msg.Substring(13), ref srcEncoding))
									writeLineSrc("Source Encoding changed to: " + srcEncoding.EncodingName);
								else
									writeLineSrc("Invalid Encoding");
								break;
							case "trgencoding":
								if (setEncoding(msg.Substring(13), ref trgEncoding))
									writeLineSrc("Target Encoding changed to: " + trgEncoding.EncodingName);
								else
									writeLineSrc("Invalid Encoding");
								break;
							case "to":
								writeLineTrg(int.Parse(data[1]), msg.Substring(data[0].Length + data[1].Length + 2));
								break;
							case "route":
								route[] oldArr = trgRoutes[int.Parse(data[1])];
								if (oldArr != null)
								{
									route[] newArr = new route[oldArr.Length + 1];
									for (int i = oldArr.Length - 1; i >= 0; i--)
										newArr[i] = oldArr[i];
									newArr[oldArr.Length] = new route(int.Parse(data[2]));
									trgRoutes[int.Parse(data[1])] = newArr;
								}
								else
									trgRoutes[int.Parse(data[1])] = new Program.route[] { new route(int.Parse(data[2])) };
								break;
							case "add":
								temp = trg.Count;
								createTrg();
								writeLineSrc("ADDED " + temp);
								if (data.Length < 3)
									break;
								trgIp[temp] = data[1];
								trgPort[temp] = int.Parse(data[2]);
								openTrg(temp);
								break;
							case "addto":
								temp = int.Parse(data[1]);
								trgIp[temp] = data[2];
								trgPort[temp] = int.Parse(data[3]);
								openTrg(temp);
								break;
							case "drop":
								temp = int.Parse(data[1]);
								trg[temp].close();
								remTrg(temp);
								break;
							case "freeze":
								frozen = true;
								writeLineSrc("FROZEN");
								break;
							case "unfreeze":
								frozen = false;
								writeLineSrc("UNFROZEN");
								break;
							case "list":
								writeLineSrc("ID: ip, port, crypt, routes");
								System.Text.StringBuilder routeStr;
								for (int i = 0; i < trg.Count; i++)
								{
									routeStr = new System.Text.StringBuilder();
									if (trgRoutes[i] != null)
									{
										foreach (route r in trgRoutes[i])
										{
											routeStr.Append(" ");
											routeStr.Append(r.trg);
										}
									}
									writeLineSrc(i + ": " + trgIp[i] + ", " + trgPort[i] + ", " + trgCrypt[i].name + "," + routeStr.ToString());
								}
								break;
							case "close":
								writeLineSrc("CLOSING");
								close();
								break;
							case "share":
								shareCon = true;
								break;
							case "noshare":
								shareCon = false;
								break;
							case "ping":
								writeLineSrc("PING");
								break;
							case "join":
								if (curRoom != null)
									curRoom.part(this);
								curRoom = inst.joinRoom(data[1], this);
								roomBuff = "";
								break;
							case "part":
								if (curRoom != null)
									curRoom.part(this);
								curRoom = null;
								break;
							case "getvar":
								if (vars.ContainsKey(data[1]))
									writeLineSrc(data[1] + " = \"" + vars[data[1]] + "\"");
								else
									writeLineSrc(data[1] + " undefined");
								break;
							case "listvars":
								if (data.Length == 1)
								{
									foreach (string k in vars.Keys)
									{
										writeLineSrc(k);
									}
								}
								else
								{
									foreach (string k in vars.Keys)
									{
										if (k.Contains(data[1]))
											writeLineSrc(k);
									}
								}
								break;
							case "setvar":
								if (vars.ContainsKey(data[1]))
									vars[data[1]] = data[2];
								else
									vars.Add(data[1], data[2]);
								break;
							case "reconnect":
								if (data.Length > 1)
									reconnectTrg(int.Parse(data[1]));
								else
									reconnectTrg();
								break;
							case "handover":
								if (data.Length < 3)
									break;
								
								int hoPort = int.Parse(data[1]);
								
								if (!setMode(data[2]))
									writeLineSrc("MODE UNSUPPORTED");
								
								acceptSource(hoPort);
								if (data.Length > 3)
									writeLineTrg(string.Join(" ", data).Substring(data[0].Length + data[1].Length + data[2].Length + 3));
								
								break;
							case "accept":
								if (data.Length < 2)
									break;
								
								int aPort = int.Parse(data[1]);
								createTrg();
								acceptTarget(aPort, trg.Count - 1);
								break;
							case "acceptto":
								if (data.Length < 3)
									break;
								
								temp = int.Parse(data[1]);
								int atPort = int.Parse(data[2]);
								acceptTarget(atPort, temp);
								break;
						}
						return true;
					}
				}
				return false;
			}
			
			public void acceptSource(int port)
			{
				socks.TcpListener listener = new System.Net.Sockets.TcpListener(port);
				listener.Start();
				
				while (true)
				{
					if (listener.Pending())
					{
						proto temp = new proto_tcpClient(listener.AcceptTcpClient());
						
						listener.Stop();
						
						writeLineSrc("ACCEPTED SOURCE ON " + port);
						src.close();
					
						src = temp;
						
						return;
					}
					System.Threading.Thread.Sleep(100);
				}
			}
			
			public void acceptTarget(int port, int index)
			{
				socks.TcpListener listener = new System.Net.Sockets.TcpListener(port);
				listener.Start();
				
				while (true)
				{
					if (listener.Pending())
					{
						trg[index] = new proto_tcpClient(listener.AcceptTcpClient());
						trgPort[index] = port;
						
						listener.Stop();
						
						writeLineSrc("ACCEPTED " + index + " ON " + port);
						
						return;
					}
					System.Threading.Thread.Sleep(100);
				}
			}
			
			public void ircCollect(string msg)
			{
				string[] data = msg.Split(' ');
				string sender = "FAILFAILFAIL";
				if (data[0].Contains("!"))
					sender = data[0].Substring(1, data[0].IndexOf("!") - 1);
				string varKey, varVal;
				
				if (data.Length < 2)
					return;
				
				switch (data[1])
				{
					case "JOIN":
						varKey = "USERS_" + data[2];
						if (vars.ContainsKey(varKey))
							vars[varKey] = vars[varKey] + "," + sender;
						else
							vars.Add(varKey, sender + ",");
						break;
					case "PART":
						varKey = "USERS_" + data[2];
						if (vars.ContainsKey(varKey))
							vars[varKey] = vars[varKey].Replace(sender + ",", "");
						break;
					case "QUIT":
						string[] channels = vars["CHANNELS"].Split(',');
						foreach (string cs in channels)
						{
							varKey = "USERS_" + cs;
							if (vars.ContainsKey(varKey))
								vars[varKey] = vars[varKey].Replace(sender + ",", "");
						}
						break;
					case "353":
						// channel user msg
						varKey = "USERS_" + data[4];
						varVal = msg.Substring(msg.IndexOf(":", 1) + 1).Replace("@", "").Replace("+", "").Replace(" ", ",");
						if (vars.ContainsKey(varKey))
							vars[varKey] = varVal;
						else
							vars.Add(varKey, varVal);
						break;
				}
			}
			
			public bool ircExec(ref string msg)
			{
				if (msg.Length < 1)
					return false;
				if (msg[0] == '/')
				{
					if (msg.Length < 2)
						return false;
					if (msg[1] == '/')
					{
						msg = msg.Substring(2);
						return false;
					}
					else
					{
						msg = forceClean(msg.Substring(1).ToLower());
						string[] data = stringSplit(msg, ' ');
						switch (data[0])
						{
							case "join":
								writeLineTrg("JOIN " + data[1]);
								vars["CURCHANNEL"] = data[1];
								vars["CHANNELS"] = vars["CHANNELS"] + data[1] + ",";
								break;
							case "part":
								// implement
								vars["CHANNELS"] = vars["CHANNELS"].Replace(data[1] + vars["CURCHANNEL"] + ",", "");
								break;
							case "msg":
								writeLineTrg("PRIVMSG " + data[1] + " :" + msg.Substring(data[0].Length + data[1].Length + 2));
								break;
							case "op":
								writeLineTrg("MODE " + vars["CURCHANNEL"] + " +o " + data[1]);
								break;
							case "deop":
								writeLineTrg("MODE " + vars["CURCHANNEL"] + " -o " + data[1]);
								break;
							case "voice":
								writeLineTrg("MODE " + vars["CURCHANNEL"] + " +v " + data[1]);
								break;
							case "devoice":
								writeLineTrg("MODE " + vars["CURCHANNEL"] + " -v " + data[1]);
								break;
							case "pure":
								msg = msg.Substring(5);
								return false;
							case "quote":
								msg = msg.Substring(6);
								return false;
							case "reconnect":
								reconnectTrg();
								vars["NICK"] = reqLineSrc("NICK");
								writeLineTrg("NICK " + vars["NICK"]);
								writeLineTrg("USER " + vars["NICK"].ToLower() + " \"tim32.org\" * :" + vars["NICK"].ToLower());
								break;
						}
						return true;
					}
				}
				else
				{
					writeLineTrg("PRIVMSG " + vars["CURCHANNEL"] + " :" + msg);
					return true;
				}
				return false;
			}
			
			public void close()
			{
				if (curRoom != null)
				{
					curRoom.part(this);
					curRoom = null;
				}
				if (src != null && src.connected())
				{
					src.close();
				}
				for (int i = 0; i < trg.Count; i++)
				{
					if (trg != null && trg[i].connected())
					{
						trg[i].close();
					}
				}
			}
			
			public bool check()
			{
				if (!src.connected())
				{
					close();
					return false;
				}
				for (int i = 0; i < trg.Count; i++)
				{
					if (!trg[i].connected())
					{
						writeLineSrc("CONNECTION FAILURE - DISCONNECTED");
						close();
						Console.WriteLine("CHECK FAILED");
						return false;
					}
				}
				return true;
			}
			
			public void help(string[] data)
			{
				if (data.Length > 1)
				{ // about given command
					switch (data[1])
					{
						case "to":
							writeLineSrc("TO <trg> <msg> - sends the msg to the given trg");
							break;
					}
				}
				else
				{
					writeLineSrc("+<command>");
					writeLineSrc("TO <trg> <msg>");
					writeLineSrc("DROP <trg>");
					writeLineSrc("ADD (<ip> <port>)");
					writeLineSrc("ADDTO <trg> <ip> <port>");
					writeLineSrc("RECONNECT (<trg>)");
					writeLineSrc("JOIN <room>");
					writeLineSrc("PART");
					writeLineSrc("CLOSE");
					writeLineSrc("SHARE");
					writeLineSrc("NOSHARE");
					writeLineSrc("HANDOVER <port> <mode> (<final message>)");
					writeLineSrc("ACCEPT <port>");
					writeLineSrc("ACCEPTTO <trg> <port>");
					writeLineSrc("ROUTE <trgFrom> <trgTo>");
					writeLineSrc("-<room msg>");
					writeLineSrc("-> <shared con msg>");
				}
			}
			
			public void createTrg()
			{
				trg.Add(new proto_null());
				trgBuff.Add(new List<byte>());
				trgIp.Add(null);
				trgPort.Add(0);
				trgRoutes.Add(null);
				trgCrypt.Add(new crypt());
			}
			
			public void remTrg(int index)
			{
				trg.RemoveAt(index);
				trgBuff.RemoveAt(index);
				trgIp.RemoveAt(index);
				trgPort.RemoveAt(index);
				trgRoutes.RemoveAt(index);
				
				foreach (route[] rarr in trgRoutes)
				{
					for (int i = rarr.Length - 1; i >= 0; i--)
					{
						if (rarr[i].trg == index)
						{
							route[] newArr = new route[rarr.Length - 1];
							for (int j = i + 1; j < rarr.Length; j++)
								newArr[j - 1] = rarr[j];
							for (int j = 0; j < i; j++)
								newArr[j] = rarr[j];
						}
						else if (rarr[i].trg > index)
						{
							rarr[i].trg--;
						}
					}
				}
			}
			
			public void openTrg()
			{
				openTrg(0);
			}
			
			public void openTrg(int index)
			{
				try
				{
					trg[index] = new proto_tcpClient(new socks.TcpClient(trgIp[index], trgPort[index]));
				}
				catch (Exception ex)
				{
					writeLineSrc("CONNECTION FAILURE - DISCONNECTED");
					throw ex;
				}
			}
			
			public void reconnectTrg()
			{
				reconnectTrg(0);
			}
			
			public void reconnectTrg(int index)
			{
				writeLineSrc("CLOSING");
				trg[index].close();
				writeLineSrc("CONNECTING");
				openTrg(index);
				writeLineSrc("CONNECTED");
			}
			
			public string trimCRs(string txt)
			{
				return txt.Trim(frcTrimChars);
			}
			
			public string trimSrc(string txt)
			{
				return txt.Trim(srcTrimChars);
			}
			
			public string trimTrg(string txt)
			{
				return txt.Trim(trgTrimChars);
			}
			
			public string remBSs(string txt)
			{
				string res = "";
				for (int i = 0; i < txt.Length; i++)
				{
					if (txt[i] == '\b')
					{
						if (res.Length > 0)
							res = res.Substring(0, res.Length - 1);
					}
					else
						res += txt[i];
				}
				return res;
			}
			
			public string cleanTrg(string txt)
			{
				txt = trimTrg(txt);
				if (!presBS)
					txt = remBSs(txt);
				return txt;
			}
			
			public string cleanSrc(string txt)
			{
				txt = trimSrc(txt);
				if (!presBS)
					txt = remBSs(txt);
				return txt;
			}

			// trims common stuff
			public string forceClean(string txt)
			{
				txt = remBSs(trimCRs(txt));
				return txt;
			}
			
			public string reqLineSrc(string msg)
			{
				writeLineSrc(msg);
				return forceClean(readLineBlockSrc()); // req for this progam, so must be trimmed
			}
			
			public void writeLineSrc(string msg)
			{
				src.write(enCryptEnCodeSrc(msg), 0, msg.Length);
				src.write(srcEncoding.GetBytes(lineTermSrc), 0, lineTermSrc.Length);
			}
			
			public void writeLineTrg(string msg)
			{
				writeLineTrg(0, msg);
			}
			
			public void writeLineTrg(int index, string msg)
			{
				trg[index].write(enCryptEnCodeTrg(index, msg), 0, msg.Length);
				trg[index].write(trgEncoding.GetBytes(lineTermTrg), 0, lineTermTrg.Length);
			}
			
			public void writeLineRoom(string msg)
			{
				if (curRoom != null)
				{
					lock (roomBuff)
					{
						roomBuff += msg + "\n";
					}
				}
			}
			
			// base write to source
			public void writeSrc(string msg)
			{
				src.write(srcEncoding.GetBytes(msg), 0, msg.Length);
			}
			
			// base write to source
			public void writeSrc(byte[] msg)
			{
				src.write(msg, 0, msg.Length);
			}
			
			public void writeTrg(string msg)
			{
				writeTrg(0, msg);
			}
			
			// base write to targ
			public void writeTrg(int index, string msg)
			{
				trg[index].write(trgEncoding.GetBytes(msg), 0, msg.Length);
			}
			
			public void writeTrg(byte[] msg)
			{
				writeTrg(0, msg);
			}
			
			// base write to targ
			public void writeTrg(int index, byte[] msg)
			{
				trg[index].write(msg, 0, msg.Length);
			}
			
			public void writeRoom(string msg)
			{
				if (curRoom != null)
				{
					lock (roomBuff)
					{
						roomBuff += msg;
					}
				}
			}
			
			public string readLineBlockSrc()
			{
				sw.Reset();
				sw.Start();
				int b;
				while (true)
				{
					while ((b = src.readByte()) != -1)
					{
						sw.Reset();
						if (b == nlByte)
						{
							string temp = srcEncoding.GetString(srcBuff.ToArray());
							srcBuff.Clear();
							return cleanSrc(temp);
						}
						srcBuff.Add((byte)b);
					}
					if (sw.ElapsedMilliseconds > readTimeOut)
					{
						sw.Stop();
						throw new Exception("Timeout");
					}
					System.Threading.Thread.Sleep(sleepLen);
				}
				sw.Stop();
				return "";
			}
			
			public string readLineBlockTrg()
			{
				return readLineBlockTrg(0);
			}
			
			public string readLineBlockTrg(int index)
			{
				sw.Reset();
				sw.Start();
				int b;
				while (true)
				{
					while ((b = trg[index].readByte()) != -1)
					{
						sw.Reset();
						if ((byte)b == nlByte)
						{
							string temp = trgEncoding.GetString(trgBuff[index].ToArray());
							trgBuff[index].Clear();
							return cleanTrg(temp);
						}
						trgBuff[index].Add((byte)b);
					}
					if (sw.ElapsedMilliseconds > readTimeOut)
					{
						sw.Stop();
						throw new Exception("Timeout");
					}
					System.Threading.Thread.Sleep(sleepLen);
				}
				sw.Stop();
				return "";
			}
			
			public byte[] getCheapBytes(string str)
			{
				byte[] bytes = new byte[str.Length * sizeof(char)];
				System.Buffer.BlockCopy(str.ToCharArray(), 0, bytes, 0, bytes.Length);
				return bytes;
			}
			
			public string getCheapString(byte[] barr)
			{
				char[] chars = new char[barr.Length / sizeof(char)];
				System.Buffer.BlockCopy(barr, 0, chars, 0, barr.Length);
				return new string(chars);
			}
			
			public byte[] enCryptEnCodeSrc(string str)
			{
				byte[] barr;
				if (srcEncoding != null)
					barr = srcEncoding.GetBytes(str);
				else
					barr = getCheapBytes(str);
				barr = srcCrypt.encrypt(barr);
				return barr;
			}
			
			public string deCryptDeCodeSrc(byte[] barr)
			{
				barr = srcCrypt.decrypt(barr);
				string str;
				if (srcEncoding != null)
					str = srcEncoding.GetString(barr);
				else
					str = getCheapString(barr);
				return str;
			}
			
			public string readLineSrc(out bool read)
			{
				int b, i = 0;
				while (i < maxRead && src.dataAvailable() && (b = src.readByte()) != -1)
				{
					if (b == nlByte)
					{
						string temp = deCryptDeCodeSrc(srcBuff.ToArray());
						srcBuff.Clear();
						read = true;
						return cleanSrc(temp);
					}
					srcBuff.Add((byte)b);
					i++;
				}
				read = false;
				return "";
			}
			
			public string readLineTrg(out bool read)
			{
				return readLineTrg(0, out read);
			}
			
			public byte[] enCryptEnCodeTrg(int index, string str)
			{
				byte[] barr;
				if (trgEncoding != null)
					barr = trgEncoding.GetBytes(str);
				else
					barr = getCheapBytes(str);
				barr = trgCrypt[index].encrypt(barr);
				return barr;
			}
			
			public string deCryptDeCodeTrg(int index, byte[] barr)
			{
				barr = trgCrypt[index].decrypt(barr);
				string str;
				if (trgEncoding != null)
					str = trgEncoding.GetString(barr);
				else
					str = getCheapString(barr);
				return str;
			}
			
			public string readLineTrg(int index, out bool read)
			{
				int b, i = 0;
				while (i < maxRead && trg[index].dataAvailable() && (b = trg[index].readByte()) != -1)
				{
					if ((byte)b == nlByte)
					{
						string temp = deCryptDeCodeTrg(index, trgBuff[index].ToArray());
						trgBuff[index].Clear();
						read = true;
						return cleanTrg(temp);
					}
					trgBuff[index].Add((byte)b);
					i++;
				}
				read = false;
				return "";
			}
			
			// concept encryption - needs work
//			public byte[] readSrc()
//			{
//				int b, i = 0;
//				while (i < maxRead && srcNetStream.DataAvailable && (b = srcNetStream.ReadByte()) != -1)
//				{
//					srcBuff.Add((byte)b);
//					i++;
//					if (srcBuff.Count == ((cbcCrypt)srcCrypt).blockSize)
//					{
//						byte[] temp = srcBuff.ToArray();
//						srcBuff.Clear();
//						return srcCrypt.decrypt(temp);
//					}
//				}
//				return null;
//			}
			
			public byte[] readSrc()
			{
				int b, i = 0;
				while (i < maxRead && src.dataAvailable() && (b = src.readByte()) != -1)
				{
					srcBuff.Add((byte)b);
					i++;
				}
				byte[] temp = srcBuff.ToArray();
				srcBuff.Clear();
				return temp;
			}
			
			public byte[] readTrg()
			{
				return readTrg(0);
			}
			
			public byte[] readTrg(int index)
			{
				int b, i = 0;
				while (i < maxRead && trg[index].dataAvailable() && (b = trg[index].readByte()) != -1)
				{
					trgBuff[index].Add((byte)b);
					i++;
				}
				byte[] temp = trgBuff[index].ToArray();
				trgBuff[index].Clear();
				return temp;
			}
			
			public static string[] stringSplit(string str, char del)
			{
				return str.Split(' ');
			}
		}
		
		public delegate void conDel(socks.TcpClient tcon, int id);
//		public delegate void roomDel(string name);
		public conDel cd;
//		public roomDel rd;
		public Dictionary<string, room> rooms = new Dictionary<string, room>();
		public Dictionary<string, int> uids = new Dictionary<string, int>();
		
		public static void Main(string[] args)
		{
			Program p = new Program();
		}
		
		public Program()
		{
			loadUIDs("uids.txt");
			
			cd = new conDel(newCon);
//			rd = new roomDel(newRoom);
			
			Console.WriteLine("MiniRelay \"quotefix\" (2013-10-16 22:46)");
			Console.Write("Port[4343]: ");
			int port = 4343;
			string temp = Console.ReadLine();
			if (!int.TryParse(temp, out port))
			{
				port = 4343;
			}
			
			socks.TcpListener listener = new System.Net.Sockets.TcpListener(port);
			listener.Start();
			
			
			
			int count = 0;
			
			Console.WriteLine("Looping " + port.ToString());
			while (true)
			{
				// newbies
				if (listener.Pending())
				{
					cd.BeginInvoke(listener.AcceptTcpClient(), count, null, null);
					count++;
				}
				// roomies
				foreach (room r in rooms.Values)
					r.doStuff();
				System.Threading.Thread.Sleep(100);
			}
		}
		
		public void loadUIDs(string fileName)
		{
			System.IO.StreamReader reader = new System.IO.StreamReader(fileName);
			while (!reader.EndOfStream)
			{
				string[] data = reader.ReadLine().Split(' ');
				uids.Add(data[0], int.Parse(data[1]));
			}
			reader.Close();
		}
		
		public void newCon(socks.TcpClient tcon, int id)
		{
			con nc = null;
			try
			{
				Console.WriteLine("New Con: " + id.ToString());
				nc = new Program.con(tcon, id, this);
			}
			catch (Exception ex)
			{
				try
				{
					if (nc != null)
						nc.close();
				}
				catch { }
				Console.WriteLine("Con [" + id.ToString() + "] died: " + ex.Message);
			}
			Console.WriteLine("Con [" + id.ToString() + "] died");
		}
		
		/// <summary> NO USE </summary>
		public void newRoomAsyc(string name)
		{
			try
			{
				Console.WriteLine("New Room: " + name);
				room rn = new room(name);
				rooms.Add(name, rn);
				rn.mainloop();
			}
			catch (Exception ex)
			{
				Console.WriteLine("Room [" + name + "] died: " + ex.Message);
			}
			Console.WriteLine("Room [" + name + "] died");
		}
		
		public void newRoom(string name)
		{
			Console.WriteLine("New Room: " + name);
			room rn = new room(name);
			rooms.Add(name, rn);
		}
		
		public void write(socks.NetworkStream ns, string msg)
		{
			ns.Write(System.Text.Encoding.ASCII.GetBytes(msg + "\n"), 0, msg.Length + 1);
		}
		
		public room joinRoom(string name, con c)
		{
			if (!rooms.ContainsKey(name))
			{
//				rd.BeginInvoke(name, null, null);
				newRoom(name);
			}
			while (!rooms.ContainsKey(name))
			{
				System.Threading.Thread.Sleep(10);
			}
			rooms[name].join(c);
			return rooms[name];
		}
	}
}