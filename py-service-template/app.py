from flask import Flask
import os
import socket

app = Flask(__name__)

@app.route("/")
def home():
    html = "<h3>Hello {name}!</h3>" \
           "<b>Hostname:</b> {hostname}<br/>"
    return html.format(name=os.getenv("NAME", "world"), hostname=socket.gethostname())

@app.route("/hello")
def hello():
    return 'ehlo'

@app.route("/world")
def world():
    return 'wrld'


if __name__ == "__main__":
    app.run(host='0.0.0.0', port=30080)
