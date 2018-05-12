from flask import Flask
from flask import request
from flask import Response

import os
import socket
import json

app = Flask(__name__)
configuration = {}

@app.route("/")
def home():
    return '/ on host: {host}'.format(host=socket.gethostname())

@app.route("/hello")
def hello():
    global configuration
    return '/hello on host: {host}\n '.format(host=socket.gethostname()) + json.dumps(configuration)

@app.route("/world")
def world():
    return '/world on host: {host}'.format(host=socket.gethostname())

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
    app.run(host='0.0.0.0', port=30080)
