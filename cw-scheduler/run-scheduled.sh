#!/bin/sh

ls .scheduled | grep .conf | xargs -t -I \{\} kubectl create -f ./.scheduled/\{\}
