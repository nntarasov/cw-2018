from flask import Flask
from flask import request
from flask import Response

import os
import socket
import json
import time
import urllib2
import random

app = Flask(__name__)
configuration = {}

@app.route("/")
def home():
    global configuration
    return 'app model-comment on {host}\n{config}'.format(host=socket.gethostname(), config=json.dumps(configuration))

@app.route("/viewlist")
def viewlist():
    # wait db 9ms
    time.sleep(9.0 / 1000.0)
    return json.dumps([{'text': 'onetwothreefourfive', 'author': 'vanya'}, \
                      {'text': 'onetwothreefourfivesixseven', 'author': 'zhenya'}])

@app.route("/create")
def create():

    auth_host = {}
    for app in configuration:
        if app['image'] == 'ntarasov/cw-model-user':
            auth_host['port'] = app['actual_port']
            auth_host['ip'] = random.choice(app['actual_ips'])
            break

    auth_url = "http://{ip}:{port}/auth".format(ip=auth_host['ip'],port=auth_host['port'])

    # call auth
    print urllib2.urlopen(auth_url).read()

    # wait db 6ms
    time.sleep(6.0 / 1000.0)
    return json.dumps({'success': True})

@app.route("/configure", methods=['POST'])
def config():
    if not request.is_json:
        msg = json.dumps({'error': 'expected JSON'})
        return Response(msg, status=400, mimetype='application/json')
    config = request.get_json()
    global configuration
    configuration = config
    return json.dumps(configuration)

if __name__ == "__main__":
    app.run(host='0.0.0.0', port=30084)
