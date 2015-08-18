cp healthstone.py /usr/bin/healthstone.py
chmod +x /usr/bin/healthstone.py
crontab -l > mycron
echo "*/5 * * * * python3 /usr/bin/healthstone.py" >> mycron
crontab mycron
rm -f mycron
