from flask import Flask
from flask import request
from flask import Response

import os
import socket
import json
import time

app = Flask(__name__)
configuration = {}

@app.route("/")
def home():
    return 'app model-user on {host}\n{config}'.format(host=socket.gethostname(), config=json.dumps(configuration))

@app.route("/auth")
def auth():
    # Emulate db response wait 3ms
    time.sleep(3.0 / 1000.0)
    return json.dumps({'name': 'Ivan', 'surname': 'Ivanov', 'login': 'vanya', 'authorized': True})

@app.route("/view")
def view():
    # Emulate db response wait 5ms
    time.sleep(5.0 / 1000.0)
    return json.dumps({'name': 'Ivan', 'surname': 'Ivanov', 'login': 'vanya', 'registered': '01-01-2018'})


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
    app.run(host='0.0.0.0', port=30082)
