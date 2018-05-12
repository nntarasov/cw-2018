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
    return 'app model-post on {host}\n{config}'.format(host=socket.gethostname(), config=json.dumps(configuration))

@app.route("/view")
def view():

    comment_host = {}
    for app in configuration:
        if app['image'] == 'ntarasov/cw-model-comment':
            comment_host['port'] = app['actual_port']
            comment_host['ip'] = random.choice(app['actual_ips'])
            break

    comment_url = "http://{ip}:{port}/viewlist".format(ip=comment_host['ip'],port=comment_host['port'])

    # call auth
    comments = urllib2.urlopen(comment_url).read()
    print comments

    # wait db 15ms
    time.sleep(15.0 / 1000.0)
    return json.dumps({'text': 'blablabla', 'comments': comments})

@app.route("/preview")
def preview():
    # wait db 7ms
    time.sleep(7.0 / 1000.0)
    return json.dumps({'text': 'preview short text'})


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
    app.run(host='0.0.0.0', port=30083)
