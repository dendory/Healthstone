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

yes|cp healthstone.py /usr/bin/healthstone.py
chmod +x /usr/bin/healthstone.py
yes|cp ./healthstone.service /etc/systemd/system/healthstone.service
sed -i "s,DASHBOARD,$dashboard,g" /etc/systemd/system/healthstone.service
sed -i "s,TEMPLATE,$template,g" /etc/systemd/system/healthstone.service
systemctl restart healthstone
systemctl enable healthstone

echo "Installation done. The agent will connect to $dashboard shortly to fetch its configuration."
