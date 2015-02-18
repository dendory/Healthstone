//
// Healthstone System Monitor - (C) 2015 Patrick Lambert - http://dendory.net
//

using System;
using System.Windows;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Management;
using System.ServiceProcess;
using System.Timers;
using System.Diagnostics;
using Microsoft.Win32;

[assembly: AssemblyTitle("Healthstone System Monitor")]
[assembly: AssemblyCopyright("(C) 2015 Patrick Lambert")]
[assembly: AssemblyFileVersion("1.0.0.0")]

namespace Healthstone
{
	public class Program : System.ServiceProcess.ServiceBase // Inherit Services
	{
		static Timer hstimer;
		public Dictionary<string, string> cfg;

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
			string[] values;
			char[] delimiterChars = { '=', ':', '\t' }; // Characters available to split keys and values in .cfg file
			RegistryKey rkey = Registry.LocalMachine.OpenSubKey("Software\\Healthstone");
			System.IO.StreamReader cfgfile = new System.IO.StreamReader((string)rkey.GetValue("Config"));
			while((line = cfgfile.ReadLine()) != null)
			{
				if(line.Length > 0 && line.Trim()[0] != '#') // Avoid empty lines and comments
				{
					int index = line.LastIndexOf('#');
					if(index > 0) line = line.Substring(0, index);   // Catch same-line comments
					values = line.Trim().Split(delimiterChars);
					if(values.Length == 2) { cfg.Add(values[0].Trim(), values[1].Trim()); } // Assign key:value pairs to our cfg dictionary
				}
			}
			cfgfile.Close();
			if(Int32.Parse(cfg["Interval"]) < 10) // Since this may take a few seconds to run, disallow running it more than once every 10 secs
			{
				EventLog.WriteEntry("Healthstone", "Configuration error: Invalid interval value (must be above 10 seconds)", EventLogEntryType.Error);
				base.Stop();
			}
			hstimer = new Timer(Int32.Parse(cfg["Interval"]) * 1000); // Set a timer for the value in Interval
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

		private void DoChecks(object sender, System.Timers.ElapsedEventArgs hse)
		{
			hstimer.Stop();
			
			// Headers
			string output = "Healthstone checks: " + Environment.MachineName; // The output string contains the results from all checks
			bool alarms = false;
			ManagementObjectSearcher wmi;
			
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
					if(string.Compare(cfg["CheckDEP"], "false") != 0)
					{
						if(Int32.Parse(items["DataExecutionPrevention_SupportPolicy"].ToString()) == 0)
						{
							output +=  "Data Execution Protection (DEP) is set to OFF.\n";
							alarms = true;
						}
					}
					if(string.Compare(cfg["CheckTimeZone"], "false") != 0)
					{
						if((Int32.Parse(items["CurrentTimeZone"].ToString()) / 60) != Int32.Parse(cfg["CheckTimeZone"]))
						{
							output += "Wrong time zone set. Expected " + cfg["CheckTimeZone"] + " but got " + (Int32.Parse(items["CurrentTimeZone"].ToString()) / 60) + ".\n";
							alarms = true;
						}
					}
					if(string.Compare(cfg["CheckCodePage"], "false") != 0)
					{
						if(Int32.Parse(items["CodeSet"].ToString()) != Int32.Parse(cfg["CheckCodePage"]))
						{
							output += "Wrong code page set. Expected " + cfg["CheckCodePage"] + " but got " + Int32.Parse(items["CodeSet"].ToString()) + ".\n";
							alarms = true;
						}
					}
					if(string.Compare(cfg["CheckLocale"], "false") != 0)
					{
						if(string.Compare(items["Locale"].ToString(), cfg["CheckLocale"]) != 0)
						{
							output += "Wrong locale set. Expected " + cfg["CheckLocale"] + " but got " + items["Locale"] + ".\n";
							alarms = true;
						}
					}
					if(string.Compare(cfg["CheckFreeMemory"], "false") != 0)
					{
						if((Int32.Parse(items["FreePhysicalMemory"].ToString()) / 1000) < Int32.Parse(cfg["CheckFreeMemory"]))
						{
							output += "Low physical memory: " + (Int32.Parse(items["FreePhysicalMemory"].ToString()) / 1000) + " MB.\n";
							alarms = true;
						}
					}
					if(string.Compare(cfg["CheckLastBoot"], "false") != 0)
					{
						if(DateToHours(items["LastBootUpTime"].ToString()) < Int32.Parse(cfg["CheckLastBoot"]))
						{
							output += "System last rebooted on: " + DateToString(items["LastBootUpTime"].ToString()) + ".\n";
							alarms = true;
						}
					}
					if(string.Compare(cfg["CheckSystemStatus"], "false") != 0)
					{
						if(string.Compare(items["Status"].ToString(), "OK") != 0)
						{
							output +=  "System status set to: " + items["Status"] + ".\n";
							alarms = true;
						}
					}
					if(string.Compare(cfg["CheckProcessCount"], "false") != 0)
					{
						if(Int32.Parse(items["NumberOfProcesses"].ToString()) > Int32.Parse(cfg["CheckProcessCount"]))
						{
							output += "There are " + items["NumberOfProcesses"] + " processes running.\n";
							alarms = true;
						}
					}
				}
			}
			catch (Exception) {}

			try // Check anti virus
			{
				wmi = new ManagementObjectSearcher("root\\SecurityCenter2", "SELECT * FROM AntiVirusProduct");
				foreach(ManagementObject items in wmi.Get())
				{
					if(string.Compare(cfg["CheckAntiVirus"], "false") != 0)
					{
						if(string.Compare(items["displayName"].ToString(), "") == 0)
						{
							output +=  "There is no Anti Virus product installed.\n";
							alarms = true;
						}
					}
					if(string.Compare(cfg["CheckAntiVirusState"], "false") != 0)
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
			catch (Exception) {}

			// Footers
			if(alarms == false) output += "No check failed.";
			output += cfg["CustomText"];
			if(alarms == true || Convert.ToBoolean(cfg["AlwaysRaise"])) // Alarms were raised
			{
				if(Convert.ToBoolean(cfg["RaiseErrors"])) EventLog.WriteEntry("Healthstone", output, EventLogEntryType.Error);
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