FROM centos:latest
USER root
COPY ./healthstone.zip /root/healthstone.zip
RUN yum update -y
RUN yum install -y httpd unzip python3 crontabs
RUN unzip /root/healthstone.zip -d /usr/share
RUN chown -R apache.apache /usr/share/healthstone/db
RUN chmod g+s /usr/share/healthstone/db
RUN chmod 755 /usr/share/healthstone/www/dashboard.py
RUN echo "*/1 * * * * /usr/share/healthstone/www/dashboard.py > /dev/null" >> /tmp/mycron && crontab /tmp/mycron
RUN ln -s /usr/share/healthstone/www /var/www/html/healthstone
RUN sed -i.bak '/AllowOverride None/d' /etc/httpd/conf/httpd.conf
RUN echo "<Directory />" >> /etc/httpd/conf/httpd.conf
RUN echo " AllowOverride All" >> /etc/httpd/conf/httpd.conf
RUN echo " Options FollowSymLinks" >> /etc/httpd/conf/httpd.conf
RUN echo " Require all granted" >> /etc/httpd/conf/httpd.conf
RUN echo "</Directory>" >> /etc/httpd/conf/httpd.conf
RUN echo "<html><meta http-equiv='refresh' content='0;url=/healthstone'/></html>" > /var/www/html/index.html
EXPOSE 80
CMD ["-D", "FOREGROUND"]
ENTRYPOINT ["/usr/sbin/httpd"]
