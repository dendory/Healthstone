#!/usr/bin/python
import subprocess
import urllib.request
import urllib.parse

#
# Configuration values
#
# Interval (in seconds) configured in crontab [number]
Interval = 300
# Acceptable CPU threshold [number|False]
CheckCPU = 90
# URL of your dashboard [url]
DashboardURL = "http://healthstone.ca/dashboard"

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
data = "alarms=" + str(alarms) + "&cpu=" + str(cpu) + "&name=" + urllib.parse.quote(hostname, '') + "&output=" + urllib.parse.quote(output, '') + "&interval=" + str(Interval)
result = urllib.request.urlopen(DashboardURL + "/?" + data)
