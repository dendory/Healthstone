#!/usr/bin/env python3
# Healthstone System Monitor - (C) 2016 Patrick Lambert - http://healthstone.ca

import subprocess
import urllib.request
import urllib.parse
import time
import sys
import os
VERSION = "2.0.0"
cfg = {"general": {"interval": 30, "verbose": True, "debug": True, "dashboard": sys.argv[1], "template": sys.argv[2]}}

#
# Gather system data
#
hostname = subprocess.check_output(["hostname"]).decode("utf-8").upper().rstrip('\n')
osys = subprocess.check_output(["uname", "-srv"]).decode("utf-8").rstrip('\n')
arch = subprocess.check_output(["uname", "-i"]).decode("utf-8").rstrip('\n')
uptime = subprocess.check_output(["uptime"]).decode("utf-8").rstrip('\n')
freememory = float(os.popen("free | grep Mem | awk '{print $3/$2 * 100.0}'").read().rstrip('\n'))
localusers = os.popen("grep -v -e 'false' -e 'nologin' -e 'halt' -e 'sync' -e 'shutdown' /etc/passwd | cut -d: -f1 | tr '\n' ' '").read()
diskspace = os.popen("df -h").read()
updays = os.popen("uptime | cut -d' ' -f5").read().rstrip('\n')
output = "Healthstone checks: " + hostname + " - " + osys + " (" + arch + ")\n\n" + uptime + "\n\nLocal users: " + localusers + "\n\nDisk space:\n" + diskspace + "\n\n"
(tmp1, tmp2) = uptime.split(': ')
cpu = int(float(tmp2.split(',')[0]))

#
# Run checks
#
alarms = False
if "checkcpu" in cfg:
	if cpu > CheckCPU:
		alarms = True
		output += "CPU utilization above set threshold: " + str(cpu) + "%\n"
if "checkmemory" in cfg:
	if freememory > CheckMemory:
		alarms = True
		output += "Memory utilization above set threshold: " + str(freememory) + "%\n"
if "checklusers" in cfg:
	if CheckUser not in localusers:
		alarms = True
		output += "User missing: " + CheckUser + "\n"
if "checkprocesses" in cfg:
	ps = os.popen("ps -Af").read()
	if ps.count(CheckProcess) < 1:
		alarms = True
		output += "Process is not running: " + CheckProcess + "\n"
if "checkdiskspace" in cfg:
	df = os.popen("df").read()
	for line in df.splitlines():
		tmp = line.split(' ')
		tmp2 = [x for x in tmp if x]
		freespace = str(tmp2[4]).replace('%','')
		if freespace.isdigit() and int(freespace) > CheckDiskSpace:
			alarms = True
			output += "Disk space threshold exceeded: " + tmp2[0] + " (" + freespace + "%)\n"
if "checkmemory" in cfg:
	pass

#
# Send results off
#
data = "alarms=" + str(alarms) + "&cpu=" + str(cpu) + "&name=" + urllib.parse.quote(hostname, '') + "&template=" + urllib.parse.quote(cfg['general']['template'], '') + "&output=" + urllib.parse.quote(output, '') + "&interval=" + str(cfg['general']['interval'])
result = urllib.request.urlopen(NotifyDashboardURL + "/?" + data)
