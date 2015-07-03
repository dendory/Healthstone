#!/usr/bin/python3
# Healthstone System Monitor - (C) 2015 Patrick Lambert - http://healthstone.ca

#
# BEGIN CONFIGURATION
#

# Interval (in seconds) configured in crontab between runs [number]
Interval = 300

# Check for acceptable CPU threshold [number|False]
CheckCPU = 90

# Notify a Healthstone dashboard [url|False]
NotifyDashboardURL = "http://localhost/healthstone"

# Write alarms to a log file [filename|False]
NotifyFile = False

#
# END CONFIGURATION
#

import subprocess
import urllib.request
import urllib.parse
import time
VERSION = "1.0.8"

#
# Gather system data
#
hostname = subprocess.check_output(["hostname"]).decode("utf-8").upper().rstrip('\n')
os = subprocess.check_output(["uname", "-srv"]).decode("utf-8").rstrip('\n')
arch = subprocess.check_output(["uname", "-i"]).decode("utf-8").rstrip('\n')
uptime = subprocess.check_output(["uptime"]).decode("utf-8").rstrip('\n')
output = "Healthstone checks: " + hostname + " - " + os + " (" + arch + ")\n\n" + uptime 
(tmp1, tmp2) = uptime.split(': ')
cpu = int(float(tmp2.split(',')[0]))

#
# Run checks
#
alarms = False
if CheckCPU:
	if cpu > CheckCPU:
		alarms = True

#
# Send results off
#
if NotifyDashboardURL:
	data = "alarms=" + str(alarms) + "&cpu=" + str(cpu) + "&name=" + urllib.parse.quote(hostname, '') + "&output=" + urllib.parse.quote(output, '') + "&interval=" + str(Interval)
	result = urllib.request.urlopen(NotifyDashboardURL + "/?" + data)
if NotifyFile:
	f = open(NotifyFile, "a")
	if alarms:
		f.write(str(int(time.time())) + " FAILED\n" + output + "\n")
	else:
		f.write(str(int(time.time())) + " OK\n" + output + "\n");
	f.close()
