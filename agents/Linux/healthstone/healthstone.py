#!/usr/bin/env python3
# Healthstone System Monitor - (C) 2016 Patrick Lambert - http://healthstone.ca

import subprocess
import urllib.request
import urllib.parse
import time
import sys
import os
VERSION = "2.0.1"
if len(sys.argv) < 3:
	print("* Syntax: healthstone.py <dashboard url> <template name>")
	quit(1)
cfg = {"general": {"interval": 30, "verbose": "true", "debug": "true", "dashboard": sys.argv[1], "template": sys.argv[2]}}
print("* Healthstone System Monitor - Dashboard set to " + cfg['general']['dashboard'])

while True:
	try:
		# Gather system data
		hostname = subprocess.check_output(["hostname"]).decode("utf-8").upper().rstrip('\n')
		osys = subprocess.check_output(["uname", "-srv"]).decode("utf-8").rstrip('\n')
		arch = subprocess.check_output(["uname", "-i"]).decode("utf-8").rstrip('\n')
		uptime = subprocess.check_output(["uptime"]).decode("utf-8").rstrip('\n')
		output = "Healthstone checks: " + hostname + " - " + osys + " (" + arch + ") - " + cfg['general']['template'] + "\n\n" + uptime + "\n\n";
		(tmp1, tmp2) = uptime.split(': ')
		cpu = int(float(tmp2.split(',')[0]))

		# Run checks
		alarms = 0
		if "checkcpu" in cfg:
			try:
				if cpu > int(cfg['checkcpu']['maximum']):
					alarms += 1
					output += "--> [CheckCPU] CPU utilization above set threshold: " + str(cpu) + "%\n"
				elif cfg['general']['verbose'] == 'true':
					output += "[CheckCPU] CPU utilization: " + str(cpu) + "%\n"
			except:
				a, b, c = sys.exc_info()
				alarms += 1
				output += "--> [CheckCPU] Error while fetching CPU information: " + str(b) + "\n"
		if "checkmemory" in cfg:
			try:
				freememory = float(os.popen("free | grep Mem | awk '{print ($2 - $3) / 1000}'").read().rstrip('\n'))
				if freememory < int(cfg['checkmemory']['minimum']):
					alarms += 1
					output += "--> [CheckMemory] Free memory below set threshold: " + str(freememory) + " MB\n"
				elif cfg['general']['verbose'] == 'true':
					output += "[CheckMemory] Free memory: " + str(freememory) + " MB\n"
			except:
				a, b, c = sys.exc_info()
				alarms += 1
				output += "--> [CheckMemory] Error while fetching memory information: " + str(b) + "\n"
		if "checklusers" in cfg:
			try:
				localusers = os.popen("grep -v -e 'false' -e 'nologin' -e 'halt' -e 'sync' -e 'shutdown' /etc/passwd | cut -d: -f1 | tr '\n' ' '").read()
				for lu in cfg['checklusers']['include'].split(' '):
					if lu != "" and lu not in localusers.lower():
						alarms += 1
						output += "--> [CheckLUsers] User in include list is missing: " + lu + "\n"
				for lu in cfg['checklusers']['exclude'].split(' '):
					if lu != "" and lu in localusers.lower():
						alarms += 1
						output += "--> [CheckLUsers] User in exclude list is present: " + lu + "\n"
				if cfg['general']['verbose'] == 'true':
					output += "[CheckLUsers] Local users: " + localusers + "\n"
			except:
				a, b, c = sys.exc_info()
				alarms += 1
				output += "--> [CheckLUsers] Error while listing users: " + str(b) + "\n"
		if "checkprocesses" in cfg:
			try:
				ps = os.popen("ps -Af").read()
				for pp in cfg['checkprocesses']['include'].split(' '):
					if pp != "" and ps.count(pp) < 1:
						alarms += 1
						output += "--> [CheckProcesses] Process in include list is not running: " + pp + "\n"
				for pp in cfg['checkprocesses']['exclude'].split(' '):
					if pp != "" and ps.count(pp) > 0:
						alarms += 1
						output += "--> [CheckProcesses] Process in exclude list is running: " + pp + "\n"
				if ps.count('\n') > int(cfg['checkprocesses']['maximum']):
					alarms += 1
					output += "--> [CheckProcesses] Too many processes running\n"
				if ps.count('\n') < int(cfg['checkprocesses']['minimum']):
					alarms += 1
					output += "--> [CheckProcesses] Not enough processes running\n"
				if cfg['general']['verbose'] == 'true':
					output += "[CheckProcesses] Running processes: " + str(ps.count('\n')) + "\n"
			except:
				a, b, c = sys.exc_info()
				alarms += 1
				output += "--> [CheckProcesses] Error while listing processes: " + str(b) + "\n"
		if "checkdiskspace" in cfg:
			try:
				df = os.popen("df").read()
				tmpspace = ""
				for line in df.splitlines():
					tmp = line.split(' ')
					tmp2 = [x for x in tmp if x]
					freespace = str(tmp2[3])
					if freespace.isdigit() and "tmpfs" not in tmp2[0] and "udev" not in tmp2[0] and "mmcblk" not in tmp2[0]:
						freespace = int(int(freespace)/1000)
						if int(freespace) < int(cfg['checkdiskspace']['minimum']):
							alarms += 1
							output += "--> [CheckDiskSpace] Disk space threshold exceeded: " + tmp2[0] + " (" + str(freespace) + " MB)\n"
						tmpspace += "\n" + tmp2[0] + ": " + str(freespace)  + " MB"
				if cfg['general']['verbose'] == 'true':
					output += "[CheckDiskSpace] Free disk space: " + tmpspace + "\n"
			except:
				a, b, c = sys.exc_info()
				alarms += 1
				output += "--> [CheckDiskSpace] Error while fetching disk information: " + str(b) + "\n"

		# Send results off
		if alarms > 1:
			output += "\nChecks completed with " + str(alarms) + " alarms raised.\n"
		else:
			output += "\nChecks completed with " + str(alarms) + " alarm raised.\n"
		print("* Output:\n" + output)

		data = "alarms=" + str(alarms) + "&cpu=" + str(cpu) + "&name=" + urllib.parse.quote(hostname, '') + "&template=" + urllib.parse.quote(cfg['general']['template'], '') + "&output=" + urllib.parse.quote(output, '') + "&interval=" + str(cfg['general']['interval'])
		result = urllib.request.urlopen(cfg['general']['dashboard'] + "/?" + data).read().decode('utf8')
		if "[general]" not in str(result).lower():
			print("* Error: Invalid template received from dashboard:\n" + result)

		else:
			if cfg['general']['debug'] == 'true':
				print("* Template received by dashboard:\n" + result)
			dashboard = cfg['general']['dashboard']
			template = cfg['general']['template']
			cfg = {}
			section = {}
			sectionName = ""
			for line in str(result).split('\n'):
				line = line.strip()
				line = line.replace('\r','')
				line = line.replace('\t','')
				if len(line) > 0 and line[0] != '#':
					line = line.split('#', 1)[0]
					if line[0] == '[':
						if section != {}:
							cfg[sectionName] = section
						sectionName = line.replace('[','').replace(']','').lower()
						section = {}
					sp = line.split(':')
					if len(sp) > 1:
						section[str(sp[0]).lower().strip()] = str(sp[1]).lower().strip()
			if section != {}:
				cfg[sectionName] = section
			cfg['general']['dashboard'] = dashboard
			cfg['general']['template'] = template

	except:
		a, b, c = sys.exc_info()
		print("* A critical error occurred. Configuration reset: " + str(b))
		cfg = {"general": {"interval": 30, "verbose": "true", "debug": "true", "dashboard": sys.argv[1], "template": sys.argv[2]}}

	print("* Waiting: " + str(cfg['general']['interval']))
	time.sleep(int(cfg['general']['interval']))
