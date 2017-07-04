//
// Healthstone System Monitor - (C) 2015-2017 Patrick Lambert - http://healthstone.ca
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
using System.Linq;
using Microsoft.Win32;

[assembly: AssemblyTitle("Healthstone System Monitor")]
[assembly: AssemblyCopyright("(C) 2017 Patrick Lambert")]
[assembly: AssemblyFileVersion("2.0.5.0")]

namespace Healthstone
{
	public class Program : System.ServiceProcess.ServiceBase // Inherit Services
	{
		static System.Timers.Timer hstimer;
		public Dictionary<string, Dictionary<string, string>> cfg;
		public string output;
		public int alarms;
		public WebClient wc;
		public WebProxy wp;
		public ManagementObjectSearcher wmi;
		public RegistryKey rkey;
		public PerformanceCounter cpu;
		public int port;
		public string localusers;

		public Program() // Setting service name
        {
            this.ServiceName = "Healthstone";
        }
		
		static void Main(string[] args) // Required elements for a service program
		{
			Console.WriteLine("Healthstone System Monitor - http://healthstone.ca");
			ServiceBase[] servicesToRun;
			servicesToRun = new ServiceBase[] { new Program() };
			ServiceBase.Run(servicesToRun);
		}
  
		protected override void OnStart(string[] args) // Starting service
		{
			cfg = new Dictionary<string, Dictionary<string, string>>();
			Dictionary<string, string> section = new Dictionary<string, string>();
			rkey = Registry.LocalMachine.OpenSubKey("Software\\Healthstone");
			if(rkey == null) { EventLog.WriteEntry("Healthstone", "Registry hive missing. Please re-install Healthstone.", EventLogEntryType.Error); System.Environment.Exit(1); }
			if(rkey.GetValue("debug") == null) { section.Add("debug", "true"); }
			else { section.Add("debug", (string)rkey.GetValue("debug")); }
			if(rkey.GetValue("interval") == null) { section.Add("interval", "30"); }
			else { section.Add("interval", (string)rkey.GetValue("interval")); }
			if(rkey.GetValue("verbose") == null) { section.Add("verbose", "false"); }
			else { section.Add("verbose", (string)rkey.GetValue("verbose")); }
			if(rkey.GetValue("dashboard") == null) { EventLog.WriteEntry("Healthstone", "Registry entry missing: dashboard", EventLogEntryType.Error); System.Environment.Exit(1); }
			else { section.Add("dashboard", (string)rkey.GetValue("dashboard")); }
			if(rkey.GetValue("template") == null) { EventLog.WriteEntry("Healthstone", "Registry entry missing: template", EventLogEntryType.Error); System.Environment.Exit(1); }
			else { section.Add("template", (string)rkey.GetValue("template")); }
			cfg.Add("general", section);

			if(rkey.GetValue("proxy") != null) wp = new WebProxy((string)rkey.GetValue("proxy"));
			if(rkey.GetValue("tlsonly") != null)
			{
				ServicePointManager.ServerCertificateValidationCallback = delegate { return true; };
				System.Net.ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;
			}

			if(Int32.Parse(cfg["general"]["interval"]) < 10) // Since this may take a few seconds to run, disallow running it more than once every 10 secs
			{
				EventLog.WriteEntry("Healthstone", "Configuration error: Invalid interval value (must be above 10 seconds)", EventLogEntryType.Error);
				base.Stop();
			}
			hstimer = new System.Timers.Timer(Int32.Parse(cfg["general"]["interval"]) * 1000); // Set a timer for the value in Interval
			hstimer.Elapsed += new System.Timers.ElapsedEventHandler(ParseSections); // Call ParseSections after the timer is up
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

		private void ParseNewCfg(string data) // Parse config data retrieved from dashboard
		{
			string dashboard = cfg["general"]["dashboard"];
			string template = cfg["general"]["template"];
			cfg = new Dictionary<string, Dictionary<string, string>>();
			Dictionary<string, string> section = new Dictionary<string, string>();
			string sectionName = null;
			string line = "";
			int index = -1;

			try
			{
				data.Replace("\r", "");
				data.Replace("\t", " ");
				string[] stringSeparators = new string[] { "\n" };
				string[] lines = data.Split(stringSeparators, StringSplitOptions.None);
				foreach(string l in lines)
				{
					line = l.Trim();
					if(line.Length > 0 && line[0] != '#') // Avoid empty lines and comments
					{
						index = line.IndexOf('#');
						if(index > 0) { line = line.Substring(0, index); }   // Catch same-line comments
						if(line[0] == '[') // New section
						{
							if(sectionName != null)
							{
								cfg.Add(sectionName.ToLower(), section);
								section = new Dictionary<string, string>();
							}
							sectionName = line.Trim('[').Trim(']'); 
						}
						else
						{
							index = line.IndexOf(':');
							if(index > 0) { section.Add(line.ToLower().Substring(0, index).Trim(), line.ToLower().Substring(index + 1, line.Length - index - 1).Trim()); } // Assign key:value pairs to our cfg dictionary
						}
					}
				}
				if(sectionName != null)
				{
					cfg.Add(sectionName.ToLower(), section);
					section = new Dictionary<string, string>();
				}
			}
			catch (Exception e)
			{
				EventLog.WriteEntry("Healthstone", "Template error! Restoring default configuration: " + e, EventLogEntryType.Error);
				cfg = new Dictionary<string, Dictionary<string, string>>();
				section = new Dictionary<string, string>();
				section.Add("debug", "true");
				section.Add("interval", "30");
				section.Add("verbose", "false");
				cfg.Add("general", section);
			}

			cfg["general"]["dashboard"] = dashboard;
			cfg["general"]["template"] = template;
		}

		private void ParseSections(object sender, System.Timers.ElapsedEventArgs hse) // Main checks loop
		{
			hstimer.Stop();
			float curval = 0;

			// Headers
			alarms = 0;
			output = "Healthstone checks: " + Environment.MachineName; // The output string contains the results from all checks
			
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
				alarms += 1;
			}
			output += " - " + cfg["general"]["template"] + "\n\n";

			// Checks
			if(cfg.ContainsKey("checkcpu"))  // check cpu load
			{
				try
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
					if(curval > Int32.Parse(cfg["checkcpu"]["maximum"]))
					{
						output += "--> [CheckCPU] High CPU load: " + curval + "%\n";
						alarms += 1;
					}
					else if(cfg["general"]["verbose"] == "true") { output += "[CheckCPU] CPU load: " +  curval + "%\n"; }
				}
				catch (Exception e)
				{
					output += "[CheckCPU] Performance Counter failure: " + e + "\n";
					alarms += 1;
				}
			}

			if(cfg.ContainsKey("checkmemory"))  // check free memory
			{
				try
				{
					wmi = new ManagementObjectSearcher("root\\CIMV2", "SELECT * FROM Win32_OperatingSystem");
					foreach(ManagementObject items in wmi.Get())
					{
						if((Int32.Parse(items["FreePhysicalMemory"].ToString()) / 1000) < Int32.Parse(cfg["checkmemory"]["minimum"]))
						{
							output += "--> [CheckMemory] Low physical memory: " + (Int32.Parse(items["FreePhysicalMemory"].ToString()) / 1000) + " MB.\n";
							alarms += 1;
						}
						else if(cfg["general"]["verbose"] == "true") { output += "[CheckMemory] Free physical memory: " + (Int32.Parse(items["FreePhysicalMemory"].ToString()) / 1000) + " MB.\n"; }

					}
				}
				catch (Exception e)
				{
					output += "--> [CheckMemory] WMI Query failure: " + e + "\n";
					alarms += 1;
				}
			}

			if(cfg.ContainsKey("checkprocesses"))  // check processes
			{
				try
				{
					wmi = new ManagementObjectSearcher("root\\CIMV2", "SELECT * FROM Win32_OperatingSystem");
					foreach(ManagementObject items in wmi.Get())
					{
						if(Int32.Parse(items["NumberOfProcesses"].ToString()) > Int32.Parse(cfg["checkprocesses"]["maximum"]))
						{
							output += "--> [CheckProcesses] There are too many processes running: " + items["NumberOfProcesses"] + ".\n";
							alarms += 1;
						}
						else if(Int32.Parse(items["NumberOfProcesses"].ToString()) < Int32.Parse(cfg["checkprocesses"]["minimum"]))
						{
							output += "--> [CheckProcesses] There are too few processes running: " + items["NumberOfProcesses"] + ".\n";
							alarms += 1;
						}
						else if(cfg["general"]["verbose"] == "true") { output += "[CheckProcesses] " +  items["NumberOfProcesses"] + " processes are running.\n"; }
					}
					wmi = new ManagementObjectSearcher("root\\CIMV2", "SELECT * FROM Win32_Process");
					foreach(string pp in cfg["checkprocesses"]["include"].Split(' '))
					{
						string p = pp.Trim();
						if(p != "")
						{
							bool found = false;
							foreach(ManagementObject items in wmi.Get())
							{
								if(string.Compare(items["Caption"].ToString().ToLower(), p) == 0)
								{
									found = true;
								}
							}
							if(!found)
							{
								output +=  "--> [CheckProcesses] Process in include list is not running: " + p + "\n";
								alarms += 1;
							}
						}
					}
					wmi = new ManagementObjectSearcher("root\\CIMV2", "SELECT * FROM Win32_Process");
					foreach(string pp in cfg["checkprocesses"]["exclude"].Split(' '))
					{
						string p = pp.Trim();
						if(p != "")
						{
							bool found = false;
							foreach(ManagementObject items in wmi.Get())
							{
								if(string.Compare(items["Caption"].ToString().ToLower(), p) == 0)
								{
									found = true;
								}
							}
							if(found)
							{
								output +=  "--> [CheckProcesses] Process in exclude list is running: " + p + "\n";
								alarms += 1;
							}
						}
					}
				}
				catch (Exception e)
				{
					output += "--> [CheckProcesses] WMI Query failure: " + e + "\n";
					alarms += 1;
				}
			}

			if(cfg.ContainsKey("checkdiskspace"))  // check disk space
			{
				try
				{
					wmi = new ManagementObjectSearcher("root\\CIMV2", "SELECT * FROM Win32_LogicalDisk");
					foreach(ManagementObject items in wmi.Get())
					{
						if(items["FreeSpace"] != null && (cfg["checkdiskspace"]["onlysystemdisk"] != "true" || items["Caption"].ToString() == "C:"))
						{
							ulong freespace = (ulong)items["FreeSpace"] / 1000000;
							if(freespace < UInt64.Parse(cfg["checkdiskspace"]["minimum"]) && freespace > 0)
							{
								output +=  "--> [CheckDiskSpace] Low free space on drive " + items["Caption"] + " " + freespace + " MB\n";
								alarms += 1;
							}
							else if(cfg["general"]["verbose"] == "true") { output += "[CheckDiskSpace] Free space on drive " + items["Caption"] + " " + freespace + " MB\n"; }
						}
					}
				}
				catch (Exception e)
				{
					output += "--> [CheckDiskSpace] WMI Query failure: " + e + "\n";
					alarms += 1;
				}
			}

			if(cfg.ContainsKey("checkupdates"))  // check Windows updates
			{
				try
				{
					string[] WUstatus = new string[6] {"Unknown", "Disabled", "Manual", "Manual", "Automatic", "Unknown"};
					rkey = Registry.LocalMachine.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\WindowsUpdate\\Auto Update");
					string curWU = rkey.GetValue("AUOptions").ToString();
					if(string.Compare(WUstatus[Int32.Parse(curWU)].ToLower(), cfg["checkupdates"]["status"]) != 0)
					{
						output += "--> [CheckUpdates] Windows Updates are set to: " + WUstatus[Int32.Parse(curWU)] + ".\n";
						alarms += 1;
					}
					else if(cfg["general"]["verbose"] == "true") { output += "[CheckUpdates] Windows Updates are set to: " + WUstatus[Int32.Parse(curWU)] + ".\n"; }
				}
				catch (Exception e)
				{
					if(output.ToLower().IndexOf("microsoft windows 10") != -1)  // Windows 10 updates are on be default, the registry key doesn exist
					{ output += "[CheckUpdates] Windows Updates are set to: Default\n"; }
					else 
					{
						output += "--> [CheckUpdates] Registry query failure: " + e + "\n";
						alarms += 1;
					}
				}
			}

			if(cfg.ContainsKey("checklusers"))  // check local users
			{
				try
				{
					string localusers = "";
					ManagementObjectSearcher usersSearcher = new ManagementObjectSearcher(@"SELECT * FROM Win32_UserAccount");
					ManagementObjectCollection users = usersSearcher.Get();
					var lUsers = users.Cast<ManagementObject>().Where(u => (bool)u["LocalAccount"] == true && (bool)u["Disabled"] == false);
					foreach (ManagementObject user in lUsers)
					{
						localusers += user["Caption"].ToString() + " ";
					}
					foreach(string pp in cfg["checklusers"]["include"].Split(' '))
					{
						string p = pp.Trim();
						string domainp = Environment.MachineName + "\\" + p;
						domainp = domainp.ToLower();
						if(p != "")
						{
							bool found = false;
							foreach(string lu in localusers.Split(' '))
							{
								if(string.Compare(lu.ToLower(), p) == 0 || string.Compare(lu.ToLower(), domainp) == 0)
								{
									found = true;
								}
							}
							if(!found)
							{
								output +=  "--> [CheckLUsers] User in include list is not enabled: " + p + "\n";
								alarms += 1;
							}
						}
					}
					foreach(string pp in cfg["checklusers"]["exclude"].Split(' '))
					{
						string p = pp.Trim();
						string domainp = Environment.MachineName + "\\" + p;
						domainp = domainp.ToLower();
						if(p != "")
						{
							bool found = false;
							foreach(string lu in localusers.Split(' '))
							{
								if(string.Compare(lu.ToLower(), p) == 0 || string.Compare(lu.ToLower(), domainp) == 0)
								{
									found = true;
								}
							}
							if(found)
							{
								output +=  "--> [CheckLUsers] User in exclude list is enabled: " + p + "\n";
								alarms += 1;
							}
						}
					}
					if(cfg["general"]["verbose"] == "true") { output += "[CheckLUsers] Local users: " + localusers + "\n"; } 
				}
				catch (Exception e)
				{
					output += "--> [CheckLUsers] WMI Query failure: " + e + "\n";
					alarms += 1;
				}
			}

			if(cfg.ContainsKey("checkfirewall"))  // check firewall
			{
				try
				{
					string firewallresults = "";
					rkey = Registry.LocalMachine.OpenSubKey("System\\CurrentControlSet\\Services\\SharedAccess\\Parameters\\FirewallPolicy\\PublicProfile");
					if((int)rkey.GetValue("EnableFirewall") != 1)
					{
						if(cfg["checkfirewall"]["require"].IndexOf("public") != -1)
						{
							output += "--> [CheckFirewall] Public firewall is OFF\n";
							alarms += 1;
						}
						firewallresults += "Public: OFF  ";						
					}
					else { firewallresults += "Public: ON  "; }
					rkey = Registry.LocalMachine.OpenSubKey("System\\CurrentControlSet\\Services\\SharedAccess\\Parameters\\FirewallPolicy\\StandardProfile");
					if((int)rkey.GetValue("EnableFirewall") != 1)
					{
						if(cfg["checkfirewall"]["require"].IndexOf("private") != -1)
						{
							output += "--> [CheckFirewall] Private firewall is OFF\n";
							alarms += 1;
						}
						firewallresults += "Private: OFF  ";	
					}
					else { firewallresults += "Private: ON  "; }
					rkey = Registry.LocalMachine.OpenSubKey("System\\CurrentControlSet\\Services\\SharedAccess\\Parameters\\FirewallPolicy\\DomainProfile");
					if((int)rkey.GetValue("EnableFirewall") != 1)
					{
						if(cfg["checkfirewall"]["require"].IndexOf("domain") != -1)
						{
							output += "--> [CheckFirewall] Domain firewall is OFF\n";
							alarms += 1;
						}
						firewallresults += "Domain: OFF  ";
					}
					else { firewallresults += "Domain: ON  "; }
					if(cfg["general"]["verbose"] == "true") { output += "[CheckFirewall] " + firewallresults + "\n"; }
				}
				catch (Exception e)
				{
					output += "--> [CheckLUsers] WMI Query failure: " + e + "\n";
					alarms += 1;
				}
			}

			if(cfg.ContainsKey("checknetwork"))  // check network connection
			{
				try
				{
					Ping ping = new Ping();
					PingReply reply = ping.Send(cfg["checknetwork"]["host"], 4000);
					if(reply.Status != IPStatus.Success)
					{
						output += "--> [CheckNetwork] Host " + cfg["checknetwork"]["host"] + " is not reachable.\n";
						alarms += 1;
					}
					else
					{
						if(reply.RoundtripTime > Int32.Parse(cfg["checknetwork"]["latency"]))
						{
							output += "--> [CheckNetwork] High network latency: " + reply.RoundtripTime + "ms.\n";
							alarms += 1;
						}
						else if(cfg["general"]["verbose"] == "true") { output += "[CheckNetwork] Latency: " + reply.RoundtripTime + " ms.\n"; }
					}
				}
				catch (Exception e)
				{
					output += "--> [CheckNetwork] Ping failure: " + e + "\n";
					alarms += 1;
				}
			}


			// Notifications
			if(alarms > 1) { output += "\nChecks completed with " + alarms.ToString() + " alarms raised.\n"; }
			else { output += "\nChecks completed with " + alarms.ToString() + " alarm raised.\n"; }

			try
			{
				wc = new WebClient();
				wc.Proxy = wp;
				wc.QueryString.Add("alarms", alarms.ToString());
				wc.QueryString.Add("cpu", curval.ToString());
				wc.QueryString.Add("name", Environment.MachineName);
				wc.QueryString.Add("interval", cfg["general"]["interval"]);
				wc.QueryString.Add("template", cfg["general"]["template"]);
				wc.QueryString.Add("output", output);
				string result = wc.DownloadString(cfg["general"]["dashboard"]);
				if(result.ToLower().IndexOf("[general]") != -1)
				{
					if(cfg["general"]["debug"] == "true") { EventLog.WriteEntry("Healthstone", "Dashboard template provided: " + result, EventLogEntryType.Information); }
					ParseNewCfg(result);
				}
				else { EventLog.WriteEntry("Healthstone", "Unknown reply from dashboard: " + result, EventLogEntryType.Error); }
			}
			catch (Exception e)
			{
				EventLog.WriteEntry("Healthstone", "Healthstone dashboard [" + cfg["general"]["dashboard"] + "] could not be contacted: " + e, EventLogEntryType.Error);
			}

			if(alarms > 0) { EventLog.WriteEntry("Healthstone", output, EventLogEntryType.Warning); }
			else { EventLog.WriteEntry("Healthstone", output, EventLogEntryType.Information); }			

			hstimer.Interval = Int32.Parse(cfg["general"]["interval"]) * 1000;
			hstimer.Start();
		}
	}
}
