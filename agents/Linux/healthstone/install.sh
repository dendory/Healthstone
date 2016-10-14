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
if ! grep -q healthstone /etc/rc.local; then
        echo "/usr/bin/healthstone.py $dashboard $template > /var/log/healthstone.log 2>&1 &" >> /etc/rc.local
        chmod +x /etc/rc.local
fi
echo "Installation done. The agent will connect to $dashboard shortly to fetch its configuration."
/usr/bin/healthstone.py $dashboard $template > /var/log/healthstone.log 2>&1 &
