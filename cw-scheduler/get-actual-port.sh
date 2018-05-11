#!/bin/sh

APP=$1

kubectl get services | grep $APP | awk '{print $5}' | awk -F ':' '{print $2}' | sed 's/\/TCP//g'
