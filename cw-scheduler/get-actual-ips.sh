#!/bin/bash

APP=$1
kubectl get pods -o wide | grep $APP | awk '{print $6}'
