//
// Healthstone System Monitor - (C) 2015 Patrick Lambert - http://dendory.net
//

using System;
using System.Windows;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Management;
using System.ServiceProcess;
using System.Timers;
using System.Diagnostics;

[assembly: AssemblyTitle("Healthstone System Monitor")]
[assembly: AssemblyCopyright("(C) 2015 Patrick Lambert")]
[assembly: AssemblyFileVersion("1.0.0.0")]

namespace Healthstone
{
	public class Program : System.ServiceProcess.ServiceBase
	{
		static Timer hstimer;

		public Program()
        {
            this.ServiceName = "Healthstone";
        }
		
		static void Main(string[] args)
		{
			Console.WriteLine("Healthstone System Monitor - http://dendory.net");
			ServiceBase[] servicesToRun;
			servicesToRun = new ServiceBase[] { new Program() };
			ServiceBase.Run(servicesToRun);
		}
  
		protected override void OnStart(string[] args)
		{
			hstimer = new Timer(10 * 1000);
			hstimer.Elapsed += new System.Timers.ElapsedEventHandler(DoChecks);
			hstimer.Start();
		}

		protected override void OnStop()
		{
			hstimer.Stop();
		}

		private void DoChecks(object sender, System.Timers.ElapsedEventArgs hse)
		{
			hstimer.Stop();
			
			string output = "Healthstone checks: " + Environment.MachineName;
			bool alarms = false;
			ManagementObjectSearcher wmi;
			
			try // Get computer name
			{
				wmi = new ManagementObjectSearcher("root\\CIMV2", "SELECT * FROM Win32_OperatingSystem");
				foreach(ManagementObject items in wmi.Get())
				{
					output += " - " + items["Caption"] + " (" +  items["BuildType"] + ")";
				}
			}
			catch (ManagementException e)
			{
				output += " - " + e.Message;
				alarms = true;
			}
			output += "\n\n";
					
			if(alarms == true) // Alarms were raised
			{
				EventLog.WriteEntry("Healthstone", output, EventLogEntryType.Warning);
			}
			else
			{
				EventLog.WriteEntry("Healthstone", output, EventLogEntryType.Information);			
			}
			
			hstimer.Start();
		}
	}
}