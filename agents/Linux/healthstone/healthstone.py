#!/usr/bin/env python3
# Healthstone System Monitor - (C) 2015-2019 Patrick Lambert - https://dendory.net

import logging
import logging.handlers
import urllib.request
import urllib.parse
import subprocess
import time
import sys
import os

if len(sys.argv) < 3:
	print("* Syntax: healthstone.py <dashboard url> <template name>")
	quit(1)
cfg = {"general": {"interval": 30, "verbose": "true", "debug": "true", "dashboard": sys.argv[1], "template": sys.argv[2]}}


criterr = ""
log = logging.getLogger("healthstone")
handler = logging.handlers.SysLogHandler(address = '/dev/log')
log.addHandler(handler)
log.setLevel(logging.INFO)
log.info("Healthstone System Monitor starting - Dashboard=" + cfg['general']['dashboard'] + ", Template=" + cfg['general']['template'])
print("* Healthstone System Monitor starting - Dashboard=" + cfg['general']['dashboard'] + ", Template=" + cfg['general']['template'])

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
		alarms = 0

		# Something happened on the last loop
		if criterr != "":
			alarms += 1
			output += criterr
			criterr = ""

		# Run checks
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
		if "checkupdates" in cfg:
			try:
				upd = subprocess.check_output(["yum", "check-update"])
				if cfg['general']['verbose'] == 'true':
					output += "[CheckUpdates] No updates available.\n"
			except subprocess.CalledProcessError as grepexc:
				if grepexc.returncode == 100:
					alarms += 1
					output += "--> [CheckUpdates] System updates are available.\n"
				else:
					alarms += 1
					output += "--> [CheckUpdates] Error while running 'yum check-update'.\n"
			except:
				try:
					upd = subprocess.check_output(["apt-get", "-u", "upgrade", "--assume-no"]).decode("utf-8")
					if "0 upgraded, 0 newly installed, 0 to remove and 0 not upgraded" not in upd:
						alarms += 1
						output += "--> [CheckUpdates] System updates are available.\n"
					elif cfg['general']['verbose'] == 'true':
						output += "[CheckUpdates] No updates available.\n"
				except:
					alarms += 1
					output += "--> [CheckUpdates] Both 'yum check-update' and 'apt-get -u upgrade --assume-no' failed to run.\n"
		if "checkfirewall" in cfg:
			try:
				subprocess.check_output(["systemctl", "is-active", "firewalld"])
				if cfg['general']['verbose'] == 'true':
					output += "[CheckFirewall] firewalld is active.\n"
			except:
				try:
					subprocess.check_output(["systemctl", "is-active", "iptables"])
					if cfg['general']['verbose'] == 'true':
						output += "[CheckFirewall] iptables is active.\n"
				except:
					alarms += 1
					output += "--> [CheckFirewall] Both firewalld and iptables are inactive.\n"
		if "checkdocker" in cfg:
			try:
				docker = str(os.popen("docker container ps --format \"{{.ID}}: {{.Names}}\"").read().rstrip('\n'))
				for container in cfg['checkdocker']['running'].split(' '):
					if container != "" and container not in docker.lower():
						alarms += 1
						output += "--> [CheckDocker] Required container is missing: " + container + "\n"
				if cfg['general']['verbose'] == 'true':
					output += "[CheckDocker] Running containers:\n" + docker + "\n"
			except:
				a, b, c = sys.exc_info()
				alarms += 1
				output += "--> [CheckDocker] Error while fetching docker information: " + str(b) + "\n"
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
					if freespace.isdigit() and "tmpfs" not in tmp2[0] and tmp2[0] != "shm" and "mmcblk" not in tmp2[0] and "udev" not in tmp2[0] and (cfg['checkdiskspace']['onlysystemdisk'] != 'true' or tmp2[5] == '/'):
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
		if "checknetwork" in cfg:
			try:
				rtt = os.popen("ping -nqc 3 " + cfg['checknetwork']['host'] + " |grep rtt |awk '{print $4}'").read()
				if len(rtt.split('/')) == 4:
					if float(rtt.split('/')[1]) > int(cfg['checknetwork']['latency']):
						alarms += 1
						output += "--> [CheckNetwork] High network latency: " + str(int(float(rtt.split('/')[1]))) + " ms.\n"
					elif cfg['general']['verbose'] == 'true':
						output += "[CheckNetwork] Latency: " + str(int(float(rtt.split('/')[1]))) + " ms.\n"
				else:
					alarms += 1
					output += "--> [CheckNetwork] Could not ping host. " + rtt + "\n"
			except:
				a, b, c = sys.exc_info()
				alarms += 1
				output += "--> [CheckNetwork] Error pinging host: " + str(b) + "\n"

		# Send results off
		if alarms > 0:
			output += "\nChecks completed with " + str(alarms) + " alarms raised.\n"
			log.critical("Healthstone checks failed: " + output)
		else:
			output += "\nChecks completed without any alarm raised.\n"
			if cfg['general']['debug'] == 'true':
				log.debug("Healthstone checks succeeded: " + output)

		data = "alarms=" + str(alarms) + "&cpu=" + str(cpu) + "&name=" + urllib.parse.quote(hostname, '') + "&template=" + urllib.parse.quote(cfg['general']['template'], '') + "&output=" + urllib.parse.quote(output, '') + "&interval=" + str(cfg['general']['interval'])
		result = urllib.request.urlopen(cfg['general']['dashboard'] + "/?" + data).read().decode('utf8')

		if "[general]" not in str(result).lower():
			criterr = "Healthstone encountered a critical error. Invalid template received from dashboard. Configuration reset.\n"
			log.critical(criterr)
		else:
			if cfg['general']['debug'] == 'true':
				log.debug("Healthstone received the following template from the dashboard:\n" + result)
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
		criterr = "Healthstone has encountered a critical error. Configuration reset: " + str(b) + "\n"
		log.critical(criterr)
		cfg = {"general": {"interval": 30, "verbose": "true", "debug": "true", "dashboard": sys.argv[1], "template": sys.argv[2]}}

	if cfg['general']['debug'] == 'true':
		log.debug("Healthstone waiting interval: " + str(cfg['general']['interval']))
	time.sleep(int(cfg['general']['interval']))
