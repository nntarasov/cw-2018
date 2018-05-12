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
    html = "<h3>Hello {name}!</h3>" \
           "<b>Hostname:</b> {hostname}<br/>"
    return html.format(name=os.getenv("NAME", "world"), hostname=socket.gethostname())

@app.route("/hello")
def hello():
    global configuration
    return json.dumps(configuration)

@app.route("/world")
def world():
    return 'wrld'

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
