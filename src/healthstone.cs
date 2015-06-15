//
// Healthstone System Monitor - (C) 2015 Patrick Lambert - http://dendory.net
//

using System;
using System.Windows;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Management;
using System.ServiceProcess;
using System.Timers;
using System.Diagnostics;
using System.Threading;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Data.Odbc;
using System.Text;
using System.Security.Cryptography;
using Microsoft.Win32;

[assembly: AssemblyTitle("Healthstone System Monitor")]
[assembly: AssemblyCopyright("(C) 2015 Patrick Lambert")]
[assembly: AssemblyFileVersion("1.0.5.0")]

namespace Healthstone
{
	public class Program : System.ServiceProcess.ServiceBase // Inherit Services
	{
		static System.Timers.Timer hstimer;
		public Dictionary<string, string> cfg;
		public string output;
		public bool alarms;
		public WebClient wc;
		public ManagementObjectSearcher wmi;
		public RegistryKey rkey;
		public PerformanceCounter cpu;
		public int port;
		
		public Program() // Setting service name
        {
            this.ServiceName = "Healthstone";
        }
		
		static void Main(string[] args) // Required elements for a service program
		{
			Console.WriteLine("Healthstone System Monitor - http://dendory.net");
			ServiceBase[] servicesToRun;
			servicesToRun = new ServiceBase[] { new Program() };
			ServiceBase.Run(servicesToRun);
		}
  
		protected override void OnStart(string[] args) // Starting service
		{
			cfg = new Dictionary<string, string>();
			string line;
			rkey = Registry.LocalMachine.OpenSubKey("Software\\Healthstone");
			System.IO.StreamReader cfgfile = new System.IO.StreamReader((string)rkey.GetValue("Config"));
			while((line = cfgfile.ReadLine()) != null)
			{
				line = line.Trim();
				if(line.Length > 0 && line[0] != '#') // Avoid empty lines and comments
				{
					int index = line.IndexOf('#');
					if(index > 0) line = line.Substring(0, index);   // Catch same-line comments
					index = line.IndexOf(':');
					if(index > 0) { cfg.Add(line.Substring(0, index).Trim(), line.Substring(index + 1, line.Length - index - 1).Trim()); } // Assign key:value pairs to our cfg dictionary
				}
			}
			cfgfile.Close();
			if(Int32.Parse(cfg["Interval"]) < 10) // Since this may take a few seconds to run, disallow running it more than once every 10 secs
			{
				EventLog.WriteEntry("Healthstone", "Configuration error: Invalid interval value (must be above 10 seconds)", EventLogEntryType.Error);
				base.Stop();
			}
			hstimer = new System.Timers.Timer(Int32.Parse(cfg["Interval"]) * 1000); // Set a timer for the value in Interval
			hstimer.Elapsed += new System.Timers.ElapsedEventHandler(DoChecks); // Call DoChecks after the timer is up
			hstimer.Start();
		}

		protected override void OnStop() // Stopping the service
		{
			hstimer.Stop();
		}
 
		private string DateToString(string s) // Convert .NET WMI date to DateTime and return as a string
		{
			int year = int.Parse(s.Substring(0, 4));
			int month = int.Parse(s.Substring(4, 2));
			int day = int.Parse(s.Substring(6, 2));
			int hour = int.Parse(s.Substring(8, 2));
			int minute = int.Parse(s.Substring(10, 2));
			int second = int.Parse(s.Substring(12, 2));
			DateTime date = new DateTime(year, month, day, hour, minute, second);
			return date.ToString();
		}

		private Int32 DateToHours(string s) // Convert .NET WMI date to DateTime then return number of hours between now and then
		{
			int year = int.Parse(s.Substring(0, 4));
			int month = int.Parse(s.Substring(4, 2));
			int day = int.Parse(s.Substring(6, 2));
			int hour = int.Parse(s.Substring(8, 2));
			int minute = int.Parse(s.Substring(10, 2));
			int second = int.Parse(s.Substring(12, 2));
			DateTime date1 = new DateTime(year, month, day, hour, minute, second);
			DateTime date2 = DateTime.Now;
			return (int)((date2 - date1).TotalHours);
		}

		static uint SwapEndianness(ulong x) // Swap Endian value for NTP query
		{
			return (uint) (((x & 0x000000ff) << 24) + ((x & 0x0000ff00) << 8) + ((x & 0x00ff0000) >> 8) + ((x & 0xff000000) >> 24));
		}

		private bool CfgValue(string s) // Check if a value exists and whether it is non-false
		{
			if(!cfg.ContainsKey(s))
			{
				output += "Configuration error: Missing key " + s + "\n";
				alarms = true;
				return false;
			}
			if(string.Compare(cfg[s], "false") == 0) return false;
			return true;
		}
		
		private void DoChecks(object sender, System.Timers.ElapsedEventArgs hse) // Main checks loop
		{
			hstimer.Stop();
			float curval = 0;
			
			// Headers
			output = "Healthstone checks: " + Environment.MachineName; // The output string contains the results from all checks
			alarms = false;
			
			try // Get computer name
			{
				wmi = new ManagementObjectSearcher("root\\CIMV2", "SELECT * FROM Win32_OperatingSystem");
				foreach(ManagementObject items in wmi.Get())
				{
					output += " - " + items["Caption"] + " (" +  items["OSArchitecture"] + ")";
				}
			}
			catch (Exception e)
			{
				output += " - " + e;
				alarms = true;
			}
			output += "\n\n";
			// Checks
			
			try // Check DEP, time zone, code page, locale, free memory, last boot, status, process count
			{
				wmi = new ManagementObjectSearcher("root\\CIMV2", "SELECT * FROM Win32_OperatingSystem");
				foreach(ManagementObject items in wmi.Get())
				{
					if(CfgValue("CheckDEP"))
					{
						if(Int32.Parse(items["DataExecutionPrevention_SupportPolicy"].ToString()) == 0)
						{
							output +=  "Data Execution Protection (DEP) is set to OFF.\n";
							alarms = true;
						}
						else if(CfgValue("Verbose")) { output += "DEP: " +  items["DataExecutionPrevention_SupportPolicy"] + "\n"; }
					}
					if(CfgValue("CheckTimeZone"))
					{
						if((Int32.Parse(items["CurrentTimeZone"].ToString()) / 60) != Int32.Parse(cfg["CheckTimeZone"]))
						{
							output += "Wrong time zone: Expected " + cfg["CheckTimeZone"] + " but got " + (Int32.Parse(items["CurrentTimeZone"].ToString()) / 60) + ".\n";
							alarms = true;
						}
						else if(CfgValue("Verbose")) { output += "Time zone: " +  (Int32.Parse(items["CurrentTimeZone"].ToString()) / 60) + "\n"; }
					}
					if(CfgValue("CheckCodePage"))
					{
						if(Int32.Parse(items["CodeSet"].ToString()) != Int32.Parse(cfg["CheckCodePage"]))
						{
							output += "Wrong code page: Expected " + cfg["CheckCodePage"] + " but got " + Int32.Parse(items["CodeSet"].ToString()) + ".\n";
							alarms = true;
						}
						else if(CfgValue("Verbose")) { output += "Code page: " +  items["CodeSet"] + "\n"; }
					}
					if(CfgValue("CheckLocale"))
					{
						if(string.Compare(items["Locale"].ToString(), cfg["CheckLocale"]) != 0)
						{
							output += "Wrong locale: Expected " + cfg["CheckLocale"] + " but got " + items["Locale"] + ".\n";
							alarms = true;
						}
						else if(CfgValue("Verbose")) { output += "Locale: " +  items["Locale"] + "\n"; }
					}
					if(CfgValue("CheckFreeMemory"))
					{
						if((Int32.Parse(items["FreePhysicalMemory"].ToString()) / 1000) < Int32.Parse(cfg["CheckFreeMemory"]))
						{
							output += "Low physical memory: " + (Int32.Parse(items["FreePhysicalMemory"].ToString()) / 1000) + " MB.\n";
							alarms = true;
						}
						else if(CfgValue("Verbose")) { output += "Free physical memory: " + (Int32.Parse(items["FreePhysicalMemory"].ToString()) / 1000) + " MB.\n"; }
					}
					if(CfgValue("CheckLastBoot"))
					{
						if(DateToHours(items["LastBootUpTime"].ToString()) < Int32.Parse(cfg["CheckLastBoot"]))
						{
							output += "System last rebooted on: " + DateToString(items["LastBootUpTime"].ToString()) + ".\n";
							alarms = true;
						}
						else if(CfgValue("Verbose")) { output += "Last reboot time: " + DateToString(items["LastBootUpTime"].ToString()) + "\n"; }
					}
					if(CfgValue("CheckSystemStatus"))
					{
						if(string.Compare(items["Status"].ToString(), "OK") != 0)
						{
							output +=  "System status set to: " + items["Status"] + ".\n";
							alarms = true;
						}
						else if(CfgValue("Verbose")) { output += "System status: " +  items["Status"] + "\n"; }
					}
					if(CfgValue("CheckProcessCount"))
					{
						if(Int32.Parse(items["NumberOfProcesses"].ToString()) > Int32.Parse(cfg["CheckProcessCount"]))
						{
							output += "There are " + items["NumberOfProcesses"] + " processes running.\n";
							alarms = true;
						}
						else if(CfgValue("Verbose")) { output += "Number of processes: " +  items["NumberOfProcesses"] + "\n"; }
					}
				}
			}
			catch (Exception e)
			{
				if(CfgValue("RaiseQueryFailures"))
				{
					output += "Windows checks: WMI Query failure: " + e + "\n";
					alarms = true;				
				}
			}

			try // Check boot state, host name, domain name
			{
				wmi = new ManagementObjectSearcher("root\\CIMV2", "SELECT * FROM Win32_ComputerSystem");
				foreach(ManagementObject items in wmi.Get())
				{
					if(CfgValue("CheckBootState"))
					{
						if(string.Compare(items["BootupState"].ToString(), "Normal boot") != 0)
						{
							output +=  "Abnormal boot state: " + items["BootupState"] + "\n";
							alarms = true;
						}
						else if(CfgValue("Verbose")) { output += "Boot state: " +  items["BootupState"] + "\n"; }
					}
					if(CfgValue("CheckHostName"))
					{
						if(string.Compare(items["DNSHostName"].ToString(), cfg["CheckHostName"]) != 0)
						{
							output +=  "Wrong hostname: Expected " + cfg["CheckHostName"] + " but got " + items["DNSHostName"] + "\n";
							alarms = true;
						}
						else if(CfgValue("Verbose")) { output += "Hostname: " +  items["DNSHostName"] + "\n"; }
					}
					if(CfgValue("CheckDomainName"))
					{
						if(string.Compare(items["Domain"].ToString(), cfg["CheckDomainName"]) != 0)
						{
							output +=  "Wrong domain: Expected " + cfg["CheckDomainName"] + " but got " + items["Domain"] + "\n";
							alarms = true;
						}
						else if(CfgValue("Verbose")) { output += "Domain name: " +  items["Domain"] + "\n"; }
					}
				}
			}
			catch (Exception e)
			{
				if(CfgValue("RaiseQueryFailures"))
				{
					output += "Computer checks: WMI Query failure: " + e + "\n";
					alarms = true;				
				}
			}

			try // Check running processes
			{
				if(CfgValue("CheckProcesses"))
				{
					wmi = new ManagementObjectSearcher("root\\CIMV2", "SELECT * FROM Win32_Process");
					foreach(string p in cfg["CheckProcesses"].Split(' '))
					{
						bool found = false;
						foreach(ManagementObject items in wmi.Get())
						{
							if(string.Compare(items["Caption"].ToString(), p) == 0)
							{
								found = true;
							}
						}
						if(!found)
						{
							output +=  "Process not found in list of running process: " + p + "\n";
							alarms = true;
						}
						else if(CfgValue("Verbose")) { output +=  "Process running: " + p + "\n"; }
					}
				}
			}
			catch (Exception e)
			{
				if(CfgValue("RaiseQueryFailures"))
				{
					output += "Processes check: WMI Query failure: " + e + "\n";
					alarms = true;				
				}
			}

			try // Check disk space
			{
				if(CfgValue("CheckDiskSpace"))
				{
					wmi = new ManagementObjectSearcher("root\\CIMV2", "SELECT * FROM Win32_LogicalDisk");
					foreach(ManagementObject items in wmi.Get())
					{
						if(items["FreeSpace"] != null)
						{
							ulong freespace = (ulong)items["FreeSpace"] / 1000000;
							if(freespace < UInt64.Parse(cfg["CheckDiskSpace"]) && freespace > 0)
							{
								output +=  "Low disk space on drive " + items["Caption"] + " " + freespace + " MB\n";
								alarms = true;
							}
							else if(CfgValue("Verbose")) { output += "Disk space on drive " + items["Caption"] + " " + freespace + " MB\n"; }
						}
					}
				}
			}
			catch (Exception e)
			{
				if(CfgValue("RaiseQueryFailures"))
				{
					output += "Disk checks: WMI Query failure: " + e + "\n";
					alarms = true;				
				}
			}

			// Check ports
			if(CfgValue("CheckCustomPort")) 
			{
				TcpClient tcp = new TcpClient();
				foreach(string p in cfg["CheckCustomPort"].Split(' '))
				{
					try
					{
						port = Int32.Parse(p);
						tcp.Connect("127.0.0.1", port);
						tcp.Close();
						if(CfgValue("Verbose")) { output += "Port " + port + " is open.\n"; }
					}
					catch (Exception)
					{
						output += "Port " + port + " is closed.\n";
						alarms = true;				
					}
				}
			}

			try // Check temperature
			{
				wmi = new ManagementObjectSearcher("root\\WMI", "SELECT * FROM MSAcpi_ThermalZoneTemperature");
				foreach(ManagementObject items in wmi.Get())
				{
					if(CfgValue("CheckTemperature"))
					{
						if((((double)items["CurrentTemperature"]  - 2732) / 10) > Convert.ToDouble(cfg["CheckTemperature"]))
						{
							output +=  "High system temperature: " + (((double)items["CurrentTemperature"]  - 2732) / 10) + " C\n";
							alarms = true;
						}
						else if(CfgValue("Verbose")) { output += "System temperature: " +  (((double)items["CurrentTemperature"]  - 2732) / 10) + " C\n"; }
					}
				}
			}
			catch (Exception e)
			{
				if(CfgValue("RaiseQueryFailures"))
				{
					output += "Temperature checks: WMI Query failure: " + e + "\n";
					alarms = true;				
				}
			}

			try // Check anti virus
			{
				wmi = new ManagementObjectSearcher("root\\SecurityCenter2", "SELECT * FROM AntiVirusProduct");
				foreach(ManagementObject items in wmi.Get())
				{
					if(CfgValue("CheckAntiVirus"))
					{
						if(string.Compare(items["displayName"].ToString(), "") == 0)
						{
							output +=  "There is no Anti Virus product installed.\n";
							alarms = true;
						}
					}
					if(CfgValue("CheckAntiVirusState"))
					{
						if(Int32.Parse(items["productState"].ToString()).ToString("X6")[2] == '0') // This bit is for disabled
						{
							output +=  "Anti Virus " + items["displayName"] + " is disabled.\n";
							alarms = true;
						}
						if(Int32.Parse(items["productState"].ToString()).ToString("X6")[4] == '1') // This bit is for out of date
						{
							output +=  "Anti Virus " + items["displayName"] + " is out of date.\n";
							alarms = true;
						}
					}
				}
			}
			catch (Exception e)
			{
				if(CfgValue("RaiseQueryFailures"))
				{
					output += "Anti Virus checks: WMI Query failure: " + e + "\n";
					alarms = true;				
				}
			}

			try // Check firewall
			{
				if(CfgValue("CheckFirewallPublic"))
				{
					rkey = Registry.LocalMachine.OpenSubKey("System\\CurrentControlSet\\Services\\SharedAccess\\Parameters\\FirewallPolicy\\PublicProfile");
					if((int)rkey.GetValue("EnableFirewall") != 1)
					{
						output += "Public firewall is OFF\n";
						alarms = true;
					}
					else if(CfgValue("Verbose")) { output += "Public firewall: " +  rkey.GetValue("EnableFirewall") + "\n"; }
				}
				if(CfgValue("CheckFirewallPrivate"))
				{
					rkey = Registry.LocalMachine.OpenSubKey("System\\CurrentControlSet\\Services\\SharedAccess\\Parameters\\FirewallPolicy\\StandardProfile");
					if((int)rkey.GetValue("EnableFirewall") != 1)
					{
						output += "Private firewall is OFF\n";
						alarms = true;
					}
					else if(CfgValue("Verbose")) { output += "Private firewall: " +  rkey.GetValue("EnableFirewall") + "\n"; }
				}
				if(CfgValue("CheckFirewallDomain"))
				{
					rkey = Registry.LocalMachine.OpenSubKey("System\\CurrentControlSet\\Services\\SharedAccess\\Parameters\\FirewallPolicy\\DomainProfile");
					if((int)rkey.GetValue("EnableFirewall") != 1)
					{
						output += "Domain firewall is OFF\n";
						alarms = true;
					}
					else if(CfgValue("Verbose")) { output += "Domain firewall: " +  rkey.GetValue("EnableFirewall") + "\n"; }
				}
			}
			catch (Exception e)
			{
				if(CfgValue("RaiseQueryFailures"))
				{
					output += "Firewall checks: Registry Query failure: " + e + "\n";
					alarms = true;				
				}
			}

			try // Check windows updates
			{
				if(CfgValue("CheckWULast"))
				{
					rkey = Registry.LocalMachine.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\WindowsUpdate\\Auto Update\\Results\\Install");
					if((DateTime.Now - Convert.ToDateTime((string)rkey.GetValue("LastSuccessTime"))).TotalHours > Int32.Parse(cfg["CheckWULast"]))
					{
						output += "Last Windows updates " + (int)(DateTime.Now - Convert.ToDateTime((string)rkey.GetValue("LastSuccessTime"))).TotalHours + " hours ago on " + rkey.GetValue("LastSuccessTime") + "\n";
						alarms = true;
					}
					else if(CfgValue("Verbose")) { output += "Last Windows update: " + rkey.GetValue("LastSuccessTime") + "\n"; }
				}
				if(CfgValue("CheckWUEnabled"))
				{
					rkey = Registry.LocalMachine.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\WindowsUpdate\\Auto Update");
					if((int)rkey.GetValue("AUOptions") != 4)
					{
						output += "Windows Updates are not set to automatically install.\n";
						alarms = true;
					}
					else if(CfgValue("Verbose")) { output += "Windows Updates are set to " + rkey.GetValue("AUOptions") + "\n"; }
				}
			}
			catch (Exception e)
			{
				if(CfgValue("RaiseQueryFailures"))
				{
					output += "Windows Update checks: Registry Query failure: " + e + "\n";
					alarms = true;				
				}
			}

			try // Check Windows Update KB fixes
			{
				if(CfgValue("CheckWUHotFixes"))
				{
					wmi = new ManagementObjectSearcher("root\\CIMV2", "SELECT * FROM Win32_QuickFixEngineering");
					foreach(string p in cfg["CheckWUHotFixes"].Split(' '))
					{
						bool found = false;
						foreach(ManagementObject items in wmi.Get())
						{
							if(string.Compare(p, items["HotFixID"].ToString()) == 0) { found = true; }
						}
						if(!found)
						{
							output +=  "Missing update: " + p + "\n";
							alarms = true;
						}
					}
				}
			}
			catch (Exception e)
			{
				if(CfgValue("RaiseQueryFailures"))
				{
					output += "Windows Update: WMI Query failure: " + e + "\n";
					alarms = true;
				}
			}

			try // check cpu load
			{
				if(CfgValue("CheckCpuLoad"))
				{
					cpu = new PerformanceCounter();
					cpu.CategoryName = "Processor";
					cpu.CounterName = "% Processor Time";
					cpu.InstanceName = "_Total";
					float val1 = cpu.NextValue();
					Thread.Sleep(500);
					float val2 = cpu.NextValue();
					Thread.Sleep(500);
					float val3 = cpu.NextValue();					
					curval = Math.Max(val1, Math.Max(val2, val3));
					if(curval > Int32.Parse(cfg["CheckCpuLoad"]))
					{
						output += "High CPU load: " + curval + "%\n";
						alarms = true;
					}
					else if(CfgValue("Verbose")) { output += "CPU load: " +  curval + "%\n"; }					
				}
			}
			catch (Exception e)
			{
				if(CfgValue("RaiseQueryFailures"))
				{
					output += "CPU checks: Performance Counter failure: " + e + "\n";
					alarms = true;				
				}
			}

			try // check network connection
			{
				if(CfgValue("CheckNetConnectivity"))
				{
					Ping ping = new Ping();
					PingReply reply = ping.Send(cfg["CheckNetConnectivity"], 2000);
					if(reply.Status != IPStatus.Success)
					{
						output += "Network connectivity: " + cfg["CheckNetConnectivity"] + " is not reachable.\n";
						alarms = true;
					}
					else if(CfgValue("CheckNetLatency"))
					{
						if(reply.RoundtripTime > Int32.Parse(cfg["CheckNetLatency"]))
						{
							output += "High network latency: " + reply.RoundtripTime + "ms.\n";
							alarms = true;
						}
						else if(CfgValue("Verbose")) { output += "Network connectivity: " + cfg["CheckNetConnectivity"] + " is reachable (" + reply.RoundtripTime + "ms).\n"; }
					}
					else if(CfgValue("Verbose")) { output += "Network connectivity: " + cfg["CheckNetConnectivity"] + " is reachable.\n"; }
				}
			}
			catch (Exception e)
			{
				if(CfgValue("RaiseQueryFailures"))
				{
					output += "Network checks: Network connectivity failure: " + e + "\n";
					alarms = true;				
				}
			}

			try // check http request
			{
				if(CfgValue("CheckNetHttp"))
				{
					wc = new WebClient();
					string result = wc.DownloadString(cfg["CheckNetHttp"]);
					if(CfgValue("Verbose")) { output += "HTTP connectivity succeeded.\n"; }
				}
			}
			catch (WebException e)
			{
				output += "HTTP connectivity failure: " + e.Message + "\n";
				alarms = true;
			}
			catch (Exception e)
			{
				if(CfgValue("RaiseQueryFailures"))
				{
					output += "HTTP checks: Network connectivity failure: " + e + "\n";
					alarms = true;				
				}
			}

			try // check ODBC
			{
				if(CfgValue("CheckODBC"))
				{
					OdbcConnection cnn = new OdbcConnection(cfg["CheckODBC"]);
					cnn.Open();
					if(CfgValue("Verbose")) { output += "ODBC connectivity: Server is running version " + cnn.ServerVersion + ".\n"; }
					cnn.Close();
				}
			}
			catch (OdbcException e)
			{
				output += "ODBC connectivity failure: " + e.Message;
				alarms = true;
			}
			catch (Exception e)
			{
				if(CfgValue("RaiseQueryFailures"))
				{
					output += "ODBC checks: Network connectivity failure: " + e + "\n";
					alarms = true;				
				}
			}

			try // check time
			{
				if(CfgValue("CheckTime"))
				{
					var ntpData = new byte[48];  // stackoverflow.com/questions/1193955
					ntpData[0] = 0x1B;   // query for NTP server
					var addresses = Dns.GetHostEntry(cfg["CheckTime"]).AddressList;
					var ipEndPoint = new IPEndPoint(addresses[0], 123);   // connect to NTP server port 123
					var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
					socket.Connect(ipEndPoint);
					socket.ReceiveTimeout = 3000;     
					socket.Send(ntpData);  // send
					var pctime = (long)(DateTime.UtcNow.Subtract(new DateTime(1900, 1, 1, 0, 0, 0, DateTimeKind.Utc))).TotalSeconds * 1000;  // current time (ms)
					socket.Receive(ntpData);  // receive
					socket.Close();
					const byte serverReplyTime = 40;
					ulong intPart = BitConverter.ToUInt32(ntpData, serverReplyTime);
					ulong fractPart = BitConverter.ToUInt32(ntpData, serverReplyTime + 4);
					intPart = SwapEndianness(intPart);
					fractPart = SwapEndianness(fractPart);
					var ntptime = (intPart * 1000) + ((fractPart * 1000) / 0x100000000L); // NTP server time (ms)
					if(((ulong)ntptime + 300000) < (ulong)pctime || (ulong)pctime < ((ulong)ntptime - 300000))
					{
						output += "Time check failed: Local time: " + (DateTime.UtcNow) + ", NTP time: " + (new DateTime(1900, 1, 1, 0, 0, 0, DateTimeKind.Utc)).AddMilliseconds((long)ntptime) + "\n";
						alarms = true;
					}
					else if(CfgValue("Verbose")) { output += "Local time: " + (DateTime.UtcNow) + ", NTP time: " + (new DateTime(1900, 1, 1, 0, 0, 0, DateTimeKind.Utc)).AddMilliseconds((long)ntptime) + "\n"; }
				}
			}
			catch (Exception e)
			{
				if(CfgValue("RaiseQueryFailures"))
				{
					output += "Time checks: NTP query failure: " + e + "\n";
					alarms = true;				
				}
			}

			// Footers
			if(alarms == false) output += "No check failed.";
			output += cfg["CustomText"];
			// Healthstone dashboard
			if(CfgValue("NotifyHealthstoneDashboard"))
			{
				try
				{
					wc = new WebClient();
					wc.QueryString.Add("alarms", alarms.ToString());
					wc.QueryString.Add("cpu", curval.ToString());
					wc.QueryString.Add("name", Environment.MachineName);
					wc.QueryString.Add("interval", cfg["Interval"]);
					wc.QueryString.Add("output", output);
					string result = wc.DownloadString(cfg["NotifyHealthstoneDashboard"]);
					if(CfgValue("NotifyDebug")) { output += "Healthstone dashboard updated: " + result; }
				}
				catch (Exception e)
				{
					EventLog.WriteEntry("Healthstone", "Healthstone dashboard could not be updated: " + e, EventLogEntryType.Error);
				}
			}
			if(alarms == true || CfgValue("AlwaysRaise")) // Alarms were raised
			{
				// NodePoint notifications
				if(CfgValue("NotifyNodePoint") && CfgValue("NotifyNodePointKey") && CfgValue("NotifyNodePointAddress") && CfgValue("NotifyNodePointProduct"))
				{
					try
					{
						wc = new WebClient();
						wc.QueryString.Add("api", "add_ticket");
						wc.QueryString.Add("key", cfg["NotifyNodePointKey"]);
						wc.QueryString.Add("product_id", cfg["NotifyNodePointProduct"]);
						wc.QueryString.Add("release_id", "1.0");
						wc.QueryString.Add("title", "Healthstone checks: " + Environment.MachineName);
						wc.QueryString.Add("description", output);
						string result = wc.DownloadString(cfg["NotifyNodePointAddress"]);
						if(CfgValue("NotifyDebug")) { output += "NodePoint ticket sent: " + result; }
					}
					catch (Exception e)
					{
						EventLog.WriteEntry("Healthstone", "NodePoint ticket entry failed: " + e, EventLogEntryType.Error); // If NodePoint connection fails, write an Event Log error
					}
				}
				// Pushbullet notifications
				if(CfgValue("NotifyPushbullet") && CfgValue("NotifyPushbulletKey"))
				{
					try
					{
						wc = new WebClient();
						wc.Credentials = new NetworkCredential(cfg["NotifyPushbulletKey"], "");
						byte[] response = wc.UploadValues("https://api.pushbullet.com/v2/pushes", new NameValueCollection()
						{
							{ "type", "note" },
							{ "title", "Healthstone checks: " + Environment.MachineName },
							{ "body", output }
						});
						string result = System.Text.Encoding.UTF8.GetString(response);
						if(CfgValue("NotifyDebug")) { output += "Pushbullet notification sent: " + result; }
					}
					catch (Exception e)
					{
						EventLog.WriteEntry("Healthstone", "NodePoint ticket entry failed: " + e, EventLogEntryType.Error); // If Pushbullet notify fails, write an Event Log error
					}
				}
				// Email notifications
				if(CfgValue("NotifyEmail") && CfgValue("NotifyEmailServer") && CfgValue("NotifyEmailPort") && CfgValue("NotifyEmailFrom") && CfgValue("NotifyEmailTo"))
				{
					try
					{
						System.Net.Mail.MailMessage message = new System.Net.Mail.MailMessage();
						foreach (string s in cfg["NotifyEmailTo"].Split(' ')) { message.To.Add(s); }
						message.Subject = "Healthstone checks: " + Environment.MachineName;
						message.From = new System.Net.Mail.MailAddress(cfg["NotifyEmailFrom"]);
						message.Body = output;
						System.Net.Mail.SmtpClient smtp = new System.Net.Mail.SmtpClient(cfg["NotifyEmailServer"]);
						smtp.Send(message);
					}
					catch (Exception e)
					{
						EventLog.WriteEntry("Healthstone", "Email notification failed: " + e, EventLogEntryType.Error); // If email sending failed, write an Event Log error
					}
				}
				// Custom program
				if(CfgValue("NotifyProgram"))
				{
					try
					{
						System.Diagnostics.Process pProcess = new System.Diagnostics.Process();
						pProcess.StartInfo.FileName = cfg["NotifyProgram"];
						pProcess.StartInfo.Arguments = output.Replace('\n', ' ');
						pProcess.StartInfo.UseShellExecute = false;
						pProcess.StartInfo.RedirectStandardOutput = true;   
						pProcess.Start();
						string result = pProcess.StandardOutput.ReadToEnd();
						pProcess.WaitForExit(10000);  // wait 10 secs for exit
						if(CfgValue("NotifyDebug")) { output += "Custom program notification sent: " + result; }
					}
					catch (Exception e)
					{
						EventLog.WriteEntry("Healthstone", "Email notification failed: " + e, EventLogEntryType.Error); // If process execution fails, write an Event Log error
					}
				}	
				// SNS notification
				if(CfgValue("NotifyAWSTopic") && CfgValue("NotifyAWSRegion") && CfgValue("NotifyAWSKey") && CfgValue("NotifyAWSSecret"))
				{
					try
					{
						string query = "AWSAccessKeyId=" + Uri.EscapeDataString(cfg["NotifyAWSKey"]) + "&Action=Publish&Message=" + Uri.EscapeDataString(output.Replace("("," ").Replace(")"," ")) + "&SignatureMethod=HmacSHA256&SignatureVersion=2&TargetArn=" + Uri.EscapeDataString(cfg["NotifyAWSTopic"]) + "&Timestamp=" + Uri.EscapeDataString(System.DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"));
						string tosign = "GET\nsns." + cfg["NotifyAWSRegion"] + ".amazonaws.com\n/\n" + query;
						UTF8Encoding encoding = new UTF8Encoding();
						HMACSHA256 hmac = new HMACSHA256(encoding.GetBytes(cfg["NotifyAWSSecret"]));
						string signature = Convert.ToBase64String(hmac.ComputeHash(encoding.GetBytes(tosign)));
						query += "&Signature=" + Uri.EscapeDataString(signature);
						wc = new WebClient();
						string result = wc.DownloadString("https://sns." + cfg["NotifyAWSRegion"] + ".amazonaws.com/?" + query);
						if(CfgValue("NotifyDebug")) { output += "SNS notification sent: " + result; }
					}
					catch (Exception e)
					{
						EventLog.WriteEntry("Healthstone", "SNS notification failed: " + e, EventLogEntryType.Error); // If process execution fails, write an Event Log error
					}
				}
				// Event Log
				if(CfgValue("RaiseErrors")) EventLog.WriteEntry("Healthstone", output, EventLogEntryType.Error); // Log an error or warning to Event Log at the end
				else EventLog.WriteEntry("Healthstone", output, EventLogEntryType.Warning);
			}
			else
			{
				EventLog.WriteEntry("Healthstone", output, EventLogEntryType.Information);			
			}
			
			hstimer.Start();
		}
	}
}