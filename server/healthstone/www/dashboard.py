#!/usr/local/bin/python
import sqlite3
import time
import cgi
import os
VERSION = "1.0.4"
query = cgi.FieldStorage()

#
# Headers
#
print("HTTP/1.0 200 OK")
if query.getvalue("output") and query.getvalue("name"):
	print("Content-Type: text/plain; charset=utf-8")	
else:
	print("Content-Type: text/html; charset=utf-8")
print()

#
# Database init
#
db = sqlite3.connect("../db/dashboard.db")
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

#
# Connection from Healthstone clients
#
if query.getvalue("output") and query.getvalue("name"):
	cpu = 0
	alarm = 1
	interval = 300
	now = int(time.time())
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
		found = True
	if found:
		execDB("UPDATE systems SET cpu = ?, interval = ?, alarm = ?, output = ?, time = ?, ip = ? WHERE name = ?", [cpu, interval, alarm, query.getvalue("output"), now, os.environ["REMOTE_ADDR"], query.getvalue("name")])
	else:
		execDB("INSERT INTO systems VALUES (?, ?, ?, ?, ?, ?, ?)", [os.environ["REMOTE_ADDR"], query.getvalue("name"), cpu, interval, alarm, query.getvalue("output"), now])
	print("OK")
	db.close()
	quit(0)

#
# Dashboard display
#
f = open("top.html", "r")
for line in f:
	print(line.replace("##TIME##", time.strftime("%Y/%m/%d %H:%M:%S")))
if query.getvalue("ip") and query.getvalue("delete"): # delete an entry
	execDB("DELETE FROM systems WHERE ip = ? AND name = ?", [query.getvalue("ip"), query.getvalue("delete")])
	print("<p><center><b>The specified system has been removed from the list.</b></center></p>")
if query.getvalue("ip") and query.getvalue("name"): # details on one system
	rows = queryDB("SELECT * FROM systems WHERE ip = ? AND name = ?", [query.getvalue("ip"), query.getvalue("name")])
	for row in rows:
		if (row[6] + row[3] * 2) < time.time():
			print("<div class='panel panel-warning'>")
		elif row[4] == 1:
			print("<div class='panel panel-danger'>")
		else:
			print("<div class='panel panel-success'>")
		print("<div class='panel-heading'><h3 class='panel-title'><span style='float:right'><i>" + time.strftime("%Y/%m/%d %H:%M:%S", time.localtime(row[6])) + "</i></span>" + row[1] + " (" + row[0] + ")</h3></div><div class='panel-body'><pre>" + row[5] + "</pre><br><form method='GET' action='.'><input type='hidden' name='ip' value='" + row[0] + "'><input type='hidden' name='delete' value='" + row[1] + "'><input type='submit' class='btn btn-danger' value='Remove system'></form></div></div>")
else: # list of systems
	print("<table class='table table-striped'><tr><th>IP</th><th>Name</th><th>CPU</th><th>Last update</th><th>Status</th></tr>")
	rows = queryDB("SELECT * FROM systems ORDER BY time DESC", [])
	for row in rows:
		print("<tr><td>" + row[0] + "</td><td>" + row[1] + "</td><td>" + str(row[2]) + "%</td><td>" + time.strftime("%Y/%m/%d %H:%M:%S", time.localtime(row[6])) + "</td><td><form method='GET' action='.'><input type='hidden' name='ip' value='" + row[0] + "'><input type='hidden' name='name' value='" + row[1] + "'><input type='submit' class='btn ")
		if (row[6] + row[3] * 2) < time.time():
			print("btn-warning' value='Lost contact'>")		
		elif row[4] == 1:
			print("btn-danger' value='Alarms raised'>")
		else:
			print("btn-success' value='Ok'>")
		print("</form></td></tr>")	
	print("</table>")	
f = open("bottom.html", "r")
for line in f:
	print(line)
db.close()
