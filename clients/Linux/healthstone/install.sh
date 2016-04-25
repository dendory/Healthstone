cp healthstone.py /usr/bin/healthstone.py
chmod +x /usr/bin/healthstone.py
crontab -l > /tmp/mycron
if ! grep -q healthstone /tmp/mycron; then
        echo "*/5 * * * * /usr/bin/healthstone.py" >> /tmp/mycron
        crontab /tmp/mycron
fi
rm -f /tmp/mycron
