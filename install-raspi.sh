#!/bin/sh

# Download the latest flic binary
curl https://raw.githubusercontent.com/50ButtonsEach/fliclib-linux-hci/master/bin/armv6l/flicd > /usr/sbin/flicd
chmod a+x /usr/sbin/flicd

# Download the latest init script
#curl https://scscsc > /etc/init.d/flicd
chmod a+x /etc/init.d/flicd

# Disable old bluetooth daemon
sudo systemctl disable bluetooth
sudo systemctl stop bluetooth

# Enable flicd
mkdir /var/lib/flic
sudo systemctl enable flicd
sudo systemctl start flicd

