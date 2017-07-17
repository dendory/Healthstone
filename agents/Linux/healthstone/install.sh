#!/bin/bash
if [[ $EUID -ne 0 ]]; then
   echo "This script must be run as root"
   exit 1
fi

if [ ! -f /usr/bin/python3 ]; then
    echo "Could not find /usr/bin/python3, make sure Python 3.x is installed."
    exit 1
fi

dashboard="$1"
template="$2"
if [ -z "$dashboard" ]; then
        read -p "Dashboard URL: " dashboard
fi
if [ -z "$template" ]; then
        read -p "Template name: " template
fi

cp healthstone.py /usr/bin/healthstone.py
chmod +x /usr/bin/healthstone.py
if ! grep -q healthstone /etc/rc.local; then
		echo "if ! pgrep -f \"healthstone.py\" > /dev/null" >> /etc/rc.local
		echo "then" >> /etc/rc.local
        echo " /usr/bin/healthstone.py $dashboard $template > /var/log/healthstone.log 2>&1 &" >> /etc/rc.local
		echo "fi" >> /etc/rc.local
        chmod +x /etc/rc.local
fi

if [[ $(ps ax |grep healthstone |wc -l) -lt 1 ]]; then
	echo "Starting Healthstone..."
	/usr/bin/healthstone.py $dashboard $template > /var/log/healthstone.log 2>&1 &
fi

echo "Installation done. The agent will connect to $dashboard shortly to fetch its configuration."
