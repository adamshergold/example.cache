#/bin/sh -x 

apt-get update
apt-get install -y libgit2-dev 
ln -s /usr/lib/x86_64-linux-gnu/libgit2.so /lib/x86_64-linux-gnu/libgit2-15e1193.so