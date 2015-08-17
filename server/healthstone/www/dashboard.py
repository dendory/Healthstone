#!/usr/bin/python3
# Healthstone System Monitor - (C) 2015 Patrick Lambert - http://healthstone.ca

#
# BEGIN CONFIGURATION
#

# Access code to access the dashboard [string]
AccessCode = "1234"

# Send notifications for systems that lose contacts [True|False]
NotifyOnLostContact = True

# Send notifications for systems that raise alarms [True|False]
NotifyOnAlarms = True

# Send a Pushbullet notification using this API key [API key|False]
NotifyPushbullet = False

# Create a NodePoint ticket using this URL, API key, product and release numbers [url|False]
NotifyNodePointURL = False
NotifyNodePointKey = "XXXXXXX"
NotifyNodePointProduct = "1"
NotifyNodePointRelease = "1.0"

# Send an email notification using this SMTP server, To address, and From address [smtp server|False]
NotifySMTPServer = False
NotifySMTPFrom = "me@example.com"
NotifySMTPTo = "you@example.com"

#
# END CONFIGURATION
#

import sqlite3
import time
import cgi
import os
import urllib.request
import urllib.parse
import smtplib
from email.mime.text import MIMEText
VERSION = "1.0.9"
query = cgi.FieldStorage()
now = int(time.time())
login = False

#
# Headers
#
print("HTTP/1.0 200 OK")
if query.getvalue("output") and query.getvalue("name"):
	print("Content-Type: text/plain; charset=utf-8")	
else:
	print("Content-Type: text/html; charset=utf-8")
if query.getvalue("ac"): # Login from form
	if query.getvalue("ac") == AccessCode:
		print("Set-Cookie: ac=" + AccessCode)
		login = True
if 'HTTP_COOKIE' in os.environ: # Login from cookies
	cookies = os.environ['HTTP_COOKIE']
	cookies = cookies.split('; ')
	for cookie in cookies:
		cookie = cookie.split('=')
		if cookie[0] == 'ac' and cookie[1] == AccessCode:
			login = True
print()

#
# Database initialization
#
try:
	db = sqlite3.connect("../db/dashboard.db")
except:
	print("Could not connect to database. Make sure this script has write access to the '../db/' folder.")
	quit(1)
def queryDB(query, args): # Query DB and return all rows
	cur = db.cursor()
	cur.execute(query, args)
	rows = cur.fetchall()
	return rows
def execDB(query, args): # Exec DB function
	db.execute(query, args)
	db.commit()
try:
	execDB("SELECT * FROM systems WHERE 0 = 1;", [])
except:
	execDB("CREATE TABLE systems (ip TEXT, name TEXT, cpu INT, interval INT, alarm INT, output TEXT, time INT);", [])
try:
	execDB("SELECT * FROM history WHERE 0 = 1;", [])
except:
	execDB("CREATE TABLE history (name TEXT, cpu INT, time INT);", [])
try:
	execDB("SELECT * FROM lostcontact WHERE 0 = 1;", [])
except:
	execDB("CREATE TABLE lostcontact (name TEXT);", [])
try:
	execDB("SELECT * FROM log WHERE 0 = 1;", [])
except:
	execDB("CREATE TABLE log (sev INT, name TEXT, event TEXT, time INT);", [])

#
# Notifications
#
def notify(title, text):
	if NotifyPushbullet: # Pushbullet notification
		post_params = {
			'type': 'note',
			'title': 'Healthstone checks: ' + title,
			'body': text
		}
		post_args = urllib.parse.urlencode(post_params)
		data = post_args.encode()
		request = urllib.request.Request(url='https://api.pushbullet.com/v2/pushes', headers={'Authorization': 'Bearer ' + NotifyPushbullet}, data=data)
		result = urllib.request.urlopen(request)
	if NotifyNodePointURL: # NodePoint notification
		data = "api=add_ticket&key=" + NotifyNodePointKey + "&product_id=" + NotifyNodePointProduct + "&release_id=" + NotifyNodePointRelease + "&title=" + urllib.parse.quote("Healthstone checks: " + title, '') + "&description=" + urllib.parse.quote(text, '')
		result = urllib.request.urlopen(NotifyNodePointURL + "/?" + data)
	if NotifySMTPServer: # Email notification
		msg = MIMEText(text)
		msg['Subject'] = 'Healthstone checks: ' + title
		msg['From'] = NotifySMTPFrom
		msg['To'] = NotifySMTPTo
		s = smtplib.SMTP(NotifySMTPServer)
		s.send_message(msg)
		s.quit()

#
# Update list of lost contact
#
lostcontact = []
rows = queryDB("SELECT name FROM lostcontact", [])
for row in rows:
	lostcontact.append(row[0])
rows = queryDB("SELECT * FROM systems", [])
for row in rows:
	if (row[6] + row[3] * 2 + 15) < time.time() and row[1] not in lostcontact:
		execDB("INSERT INTO lostcontact VALUES (?)", [row[1]])
		if NotifyOnLostContact:
			notify("Lost contact with " + row[1], "Last contact: " + time.strftime("%Y/%m/%d %H:%M:%S", time.localtime(row[6])))
		execDB("DELETE FROM log WHERE name = ? AND time < ?", [row[1], now - 604800])
		execDB("INSERT INTO log VALUES (?, ?, ?, ?)", [1, row[1], "Lost contact with host.", now])

#
# Connection from Healthstone clients
#
if query.getvalue("output") and query.getvalue("name"):
	cpu = 0
	alarm = 1
	interval = 300
	if query.getvalue("cpu"):
		cpu = int(float(query.getvalue("cpu")))
	if query.getvalue("alarms"):
		if query.getvalue("alarms").lower() == "false":
			alarm = 0
	if query.getvalue("interval"):
		interval = int(query.getvalue("interval"))
	found = False
	rows = queryDB("SELECT * FROM systems WHERE name = ?", [query.getvalue("name")])
	for row in rows:
		if row[4] == 0 and alarm == 1:
			if NotifyOnAlarms:
				notify("Alarms raised on " + row[1], query.getvalue("output"))
			execDB("DELETE FROM log WHERE name = ? AND time < ?", [row[1], now - 604800])
			execDB("INSERT INTO log VALUES (?, ?, ?, ?)", [2, row[1], query.getvalue("output"), now])
		found = True
	if found:
		execDB("UPDATE systems SET cpu = ?, interval = ?, alarm = ?, output = ?, time = ?, ip = ? WHERE name = ?", [cpu, interval, alarm, query.getvalue("output"), now, os.environ["REMOTE_ADDR"], query.getvalue("name")])
	else:
		execDB("INSERT INTO systems VALUES (?, ?, ?, ?, ?, ?, ?)", [os.environ["REMOTE_ADDR"], query.getvalue("name"), cpu, interval, alarm, query.getvalue("output"), now])
	execDB("INSERT INTO history VALUES (?, ?, ?)", [query.getvalue("name"), cpu, now])
	execDB("DELETE FROM history WHERE name = ? AND time < ?", [query.getvalue("name"), now - (50 * interval)])
	rows = queryDB("SELECT * FROM lostcontact WHERE name = ?", [query.getvalue("name")])
	for row in rows:
		execDB("INSERT INTO log VALUES (?, ?, ?, ?)", [0, query.getvalue("name"), "Contact restored with host.", now])		
	execDB("DELETE FROM lostcontact WHERE name = ?", [query.getvalue("name")])
	print("OK")
	db.close()
	quit(0)

#
# Dashboard display
#
f = open("top.html", "r")
for line in f:
	print(line.replace("##TIME##", time.strftime("%Y/%m/%d %H:%M:%S")))
if not login: # Login form
	print("<p><form method='POST' action='.'><div class='row text-center'><div class='col-md-3'></div><div class='col-md-6'><input type='password' class='form-control' name='ac' placeholder='Access code'><br><input type='submit' class='btn btn-primary' value='Login'></div></div><div class='col-md-3'></div></form></p>")
else: # Logged in
	if query.getvalue("ip") and query.getvalue("delete"): # delete an entry
		execDB("DELETE FROM systems WHERE ip = ? AND name = ?", [query.getvalue("ip"), query.getvalue("delete")])
		print("<p><center><b>The specified system has been removed from the list.</b></center></p>")
	if query.getvalue("ip") and query.getvalue("name"): # details on one system
		rows = queryDB("SELECT * FROM systems WHERE ip = ? AND name = ?", [query.getvalue("ip"), query.getvalue("name")])
		for row in rows:
			if (row[6] + row[3] * 2 + 15) < time.time():
				print("<div class='panel panel-warning'>")
			elif row[4] == 1:
				print("<div class='panel panel-danger'>")
			else:
				print("<div class='panel panel-success'>")
			print("<div class='panel-heading'><h3 class='panel-title'><span style='float:right'><i>" + time.strftime("%Y/%m/%d %H:%M:%S", time.localtime(row[6])) + "</i></span>" + row[1] + " (" + row[0] + ")</h3></div><div class='panel-body'><h4>System Profile</h4><pre>" + row[5] + "</pre><br>")
			print("<h4>CPU History (Interval: " + str(float(row[3] / 60)) + " mins)</h4>")
			cpus = []
			times = []
			rows2 = queryDB("SELECT cpu,time FROM history WHERE name = ? ORDER BY time ASC LIMIT 50;", [query.getvalue("name")])
			for row2 in rows2:
				cpus.append(row2[0])
				times.append(row2[1])
			print("<canvas id='cpu' style='max-width:99%'></canvas><script>Chart.defaults.global.responsive = true; var data = { labels: [")
			i = 0
			for t in times:
				if i % 5 == 0:
					print("'" + time.strftime("%H:%M:%S", time.localtime(t)) + "'")
				else:
					print("''")
				i += 1
				if i < len(times):
					print(",")
			print("], datasets: [{ label: 'CPU History (Interval: " + str(float(row[3] / 60)) + " mins)', fillColor: '#F2FBFC', strokeColor: '#97BBCC', pointDot : false, data: [")
			i = 0
			for c in cpus:
				print(str(c))
				i += 1
				if i < len(cpus):
					print(",")
			print("] }]}; var ctx0 = document.getElementById('cpu').getContext('2d'); new Chart(ctx0).Line(data);</script>") 
			print("<br><h4>Last events</h4><table class='table table-striped'>")
			rows2 = queryDB("SELECT * FROM log WHERE name = ? ORDER BY time DESC LIMIT 50", [query.getvalue("name")])
			for row2 in rows2:
				print("<tr><th>")
				if int(row2[0]) == 2:
					print("<i class='fa fa-exclamation-triangle'></i>")
				elif int(row2[0]) == 1:
					print("<i class='fa fa-question-circle'></i>")
				else:
					print("<i class='fa fa-info'></i>")
				print("</th><td>" + time.strftime("%Y/%m/%d %H:%M:%S", time.localtime(row2[3])) + "</td><td>" + str(row2[2]).replace("\n"," ") + "</td></tr>")
			print("</table>")
			print("<form method='GET' action='.'><input type='hidden' name='ip' value='" + row[0] + "'><input type='hidden' name='delete' value='" + row[1] + "'><input type='submit' class='btn btn-danger' value='Remove system'></form></div></div>")
	else: # list of systems
		print("<table class='table table-striped'><tr><th><i class='fa fa-laptop'></i></th><th>IP</th><th>Name</th><th>CPU</th><th>Last update</th><th>Status</th></tr>")
		rows = queryDB("SELECT * FROM systems ORDER BY time DESC", [])
		for row in rows:
			print("<tr>")
			if "Microsoft Windows" in row[5]:
				print("<td><i class='fa fa-windows'></i></td>")
			elif "Linux" in row[5]:
				print("<td><i class='fa fa-linux'></i></td>")
			else:
				print("<td><i class='fa fa-laptop'></i></td>")			
			print("<td>" + row[0] + "</td><td>" + row[1] + "</td><td>" + str(row[2]) + "%</td><td>" + time.strftime("%Y/%m/%d %H:%M:%S", time.localtime(row[6])) + "</td><td><form method='GET' action='.'><input type='hidden' name='ip' value='" + row[0] + "'><input type='hidden' name='name' value='" + row[1] + "'><input type='submit' class='btn ")
			if (row[6] + row[3] * 2 + 15) < time.time():
				print("btn-warning' value='Lost contact'>")		
			elif row[4] == 1:
				print("btn-danger' value='Alarms raised'>")
			else:
				print("btn-success' value='Ok'>")
			print("</form></td></tr>")	
		print("</table>")	
f = open("bottom.html", "r")
for line in f:
	print(line.replace("##VERSION##", VERSION))
db.close()
