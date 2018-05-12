#!/bin/sh

kubectl get pods | grep -P '1/1[ ]+Running'
