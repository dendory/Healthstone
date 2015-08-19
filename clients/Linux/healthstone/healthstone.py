#!/usr/bin/python3
# Healthstone System Monitor - (C) 2015 Patrick Lambert - http://healthstone.ca

#
# BEGIN CONFIGURATION
#

# Interval (in seconds) configured in your crontab between runs [number]
Interval = 300

# Check for acceptable CPU threshold [number|False]
CheckCPU = 90

# Check if a specific process is running [process name|False]
CheckProcess = False

# Check if used disk space is above x percent [number|False]
CheckDiskSpace = 90

# Notify a Healthstone dashboard [url|False]
NotifyDashboardURL = "http://localhost/healthstone"

# Write alarms to a log file [filename|False]
NotifyFile = False

# Run a custom shell command when alarms fail [command|False]
NotifyProgram = False

# Send a Pushbullet notification [API key|False]
NotifyPushbullet = False

#
# END CONFIGURATION
#

import subprocess
import urllib.request
import urllib.parse
import time
import os
VERSION = "1.0.9"

#
# Gather system data
#
hostname = subprocess.check_output(["hostname"]).decode("utf-8").upper().rstrip('\n')
osys = subprocess.check_output(["uname", "-srv"]).decode("utf-8").rstrip('\n')
arch = subprocess.check_output(["uname", "-i"]).decode("utf-8").rstrip('\n')
uptime = subprocess.check_output(["uptime"]).decode("utf-8").rstrip('\n')
output = "Healthstone checks: " + hostname + " - " + osys + " (" + arch + ")\n\n" + uptime + "\n\n"
(tmp1, tmp2) = uptime.split(': ')
cpu = int(float(tmp2.split(',')[0]))

#
# Run checks
#
alarms = False
if CheckCPU:
	if cpu > CheckCPU:
		alarms = True
		output += "CPU utilization above set threshold: " + str(CheckCPU) + "\n"
if CheckProcess:
	ps = os.popen("ps -Af").read()
	if ps.count(CheckProcess) < 1:
		alarms = True
		output += "Process is not running: " + CheckProcess + "\n"
if CheckDiskSpace:
	df = os.popen("df").read()
	for line in df.splitlines():
		tmp = line.split(' ')
		tmp2 = [x for x in tmp if x]
		freespace = str(tmp2[4]).replace('%','')
		if freespace.isdigit() and int(freespace) > CheckDiskSpace:
			alarms = True
			output += "Disk space threshold exceeded: " + tmp2[0] + " (" + freespace + "%)\n"

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
if NotifyProgram:
	if alarms:
		print(subprocess.check_output([NotifyProgram]).decode("utf-8"))
if NotifyPushbullet:
	if alarms:
		post_params = {
			'type': 'note',
			'body': output
		}
		post_args = urllib.parse.urlencode(post_params)
		data = post_args.encode()
		request = urllib.request.Request(url='https://api.pushbullet.com/v2/pushes', headers={'Authorization': 'Bearer ' + NotifyPushbullet}, data=data)
		result = urllib.request.urlopen(request)
