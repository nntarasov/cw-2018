from flask import Flask
from flask import request
from flask import Response

import os
import socket
import json
import time
import random
import urllib2

app = Flask(__name__)
configuration = {}

@app.route("/")
def home():
    global configuration
    return 'app model-front on {host}\n{config}'.format(host=socket.gethostname(), config=json.dumps(configuration))

def get_app_host(app_image):
    host = {}
    for app in configuration:
        if app['image'] == app_image:
            host['port'] = app['actual_port']
            host['ip'] = random.choice(app['actual_ips'])
            break
    url = '{ip}:{port}'.format(ip=host['ip'],port=host['port'])
    print 'url is: ' + url
    return url

def make_query(app_image, handle):
    host = get_app_host(app_image)
    url = "http://{host}/{handle}".format(host=host, handle=handle)
    print "Url: {url}".format(url=url)
    resp = urllib2.urlopen(url).read()
    print "Url: {url} resp: {resp}".format(url=url, resp=resp)
    return resp

@app.route("/viewpost")
def viewpost():
    return make_query('ntarasov/cw-model-post', 'view')

@app.route("/feed")
def feed():
    lst = []
    for i in range(0, 5):
        lst.append(make_query('ntarasov/cw-model-post', 'preview'))
    return json.dumps(lst)

@app.route("/viewacc")
def viewacc():
    make_query('ntarasov/cw-model-user', 'auth')
    return make_query('ntarasov/cw-model-user', 'view')

@app.route("/createcomment")
def createcomment():
    return make_query('ntarasov/cw-model-comment', 'create')

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
    app.run(host='0.0.0.0', port=30081)
