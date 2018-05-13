#!/bin/bash

GATLINGPATH=$1

mono $GATLINGPATH ../cw-config.json ../.runtime-config.json > shootreport.log
