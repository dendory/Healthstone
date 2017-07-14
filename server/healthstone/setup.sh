#!/bin/bash

# Make sure you are root
if [[ $EUID -ne 0 ]]; then
   echo "This script must be run as root."
   exit 1
fi

if [ ! -f /usr/bin/python3 ]; then
	echo "Could not find /usr/bin/python3, make sure Python 3.x is installed."
	exit 1
fi

echo "This script will copy Healthstone files, set permissions, add crontab and Apache configuration options. Press ENTER to continue or CTRL-C to cancel."
DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"

# Get default values
read -p "Software folder [/usr/share]: " installdir
installdir=${installdir:-/usr/share}
read -p "Apache document root [/var/www/html]: " wwwroot
wwwroot=${wwwroot:-/var/www/html}
read -p "Apache user [apache]: " wwwuser
wwwuser=${wwwuser:-apache}
read -p "Apache configuration file [/etc/httpd/conf/httpd.conf]: " conf
conf=${conf:-/etc/httpd/conf/httpd.conf}

# Copy files
echo "* Copying files..."
mkdir -p $installdir
cp -r $DIR $installdir

# Fix permissions
echo "* Setting permissions..."
chown -R $wwwuser.$wwwuser $installdir/healthstone
chmod 755 $installdir/healthstone/www/dashboard.py

# Add crontab for probes
echo "* Adding automation schedule for probes..."
crontab -l > /tmp/mycron
if ! grep -q dashboard /tmp/mycron; then
	echo "*/1 * * * * (cd $installdir/healthstone/www && ./dashboard.py > /dev/null)" >> /tmp/mycron
	crontab /tmp/mycron
fi
rm -f /tmp/mycron

# Add Apache config
echo "* Adding Apache config..."
ln -s $installdir/healthstone/www $wwwroot/healthstone
sed -i.bak '/AllowOverride None/d' $conf
echo "<Directory />" >> $conf
echo " AllowOverride All" >> $conf
echo " Options FollowSymLinks" >> $conf
echo " Require all granted" >> $conf
echo "</Directory>" >> $conf
a2enmod cgi > /dev/null 2>&1
if ! systemctl restart httpd 2> /dev/null ; then
	systemctl restart apache2
fi

# Done
echo "Done. If no error occurred, Healthstone should be available from http://localhost/healthstone"
echo "Please consult the README for troubleshooting."
