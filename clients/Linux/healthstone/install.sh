#!/bin/bash
if [[ $EUID -ne 0 ]]; then
   echo "This script must be run as root" 1>&2
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
crontab -l > /tmp/mycron
if ! grep -q healthstone /tmp/mycron; then
        echo "*/5 * * * * /usr/bin/healthstone.py $dashboard $template" >> /tmp/mycron
        crontab /tmp/mycron
fi
rm -f /tmp/mycron
echo "Installation done. The server will connect to $dashboard in 30 seconds to fetch its configuration."