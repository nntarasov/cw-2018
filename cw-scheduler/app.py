from flask import Flask
from flask import request
from flask import Response

import os
import socket
import json
import subprocess
import time
import urllib2

from string import Template

app = Flask(__name__)

@app.route("/")
def default():
    msg = json.dumps({'message': 'Scheduler works on host: ' + socket.gethostname()})
    return Response(msg, status=200, mimetype='application/json')


#   /schedule
#
#   Schema
#       applications : object array of
#           image : application image string
#           port : integer exposing port
#           replicas : object array of
#               node : node name
#   Warning: correct only with Flask syncronious execution

@app.route("/schedule", methods=['POST'])
def schedule():
    if not request.is_json:
        msg = json.dumps({'error': 'expected JSON'})
        return Response(msg, status=400, mimetype='application/json')

    schedule_file_cleanup()

    args = request.get_json()
    apps = args['applications'];
    unique_order = 0

    for app in apps:

        image = str(app['image'])
        port = str(app['port'])

        for repl in app['replicas']:
            node = str(repl['node'])
            unique_order += 1
            print 'Generating config: image: {image}, node: {node}, ord: {ord}'\
                .format(image=image, node=node, ord=unique_order)
            generate_replica_pod(image, node, port, unique_order)
        print 'Generating config for service. image: {image}, port: {port}'\
                .format(image=image, port=port)
        generate_application_service(image, port)

    # Stops all running entities by label
    try:
        print exec_shell(['kubectl', 'delete', 'pods,services', '-l', 'auto_created=true'])
    except:
        msg = json.dumps({'error': 'cannot delete existing pods/services by label'})
        return Response(msg, status=400, mimetype='application/json')

    # Wait while all pods will be stopped
    while True:
        out = exec_shell(['kubectl', 'get', 'pods,services'])
        print out
        time.sleep(10)
        if out.find('Terminating') == -1:
            break

    # Starts new
    try:
        print exec_shell(['./run-scheduled.sh'])
    except:
        msg = json.dumps({'error': 'cannot run pods/services'})
        return Response(msg, status=400, mimetype='application/json')

    # Generate runtime config
    hosts = []

    for app in apps:
        app_name = str(app['image']).split('/')[-1]
        print 'get-actual-port.sh ' + app_name
        try:
            app['actual_port'] = int(exec_shell(['./get-actual-port.sh', app_name]))
            print 'Actual port ' + app_name + ' -> ' + str(app['actual_port'])

            actual_ips = exec_shell(['./get-actual-ips.sh', app_name]).split('\n')
            print 'actual_ips: ' + json.dumps(actual_ips)

            app['actual_ips'] = actual_ips
            for actual_ip in actual_ips:
                hosts.append({ ip: actual_ip, port: app['actual_port']})
        except:
            msg = json.dumps({'error': 'cannot generate runtime config'})
            return Response(msg, status=400, mimetype='application/json')

    runtime_file = open('../.runtime-config.json', 'w')
    runtime_config_text = json.dumps(apps)
    runtime_file.write(runtime_config_text)
    runtime_file.close()

    # Pass runtime config to applications
    for host in hosts:
        url = '{ip}:{port}/configure'.format(ip=host['ip'], port=host[port])
        print 'curl ' + url
        req = urllib2.Request(url, data=runtime_config_text)
        print urllib2.urlopen(req).read()

    msg = json.dumps({'status': 'ok'})
    return Response(msg, status=200, mimetype='application/json')

def exec_shell(cmd):
    return subprocess.check_output(cmd)

def schedule_file_cleanup():
    try:
        print 'Trying to clean .scheduled'
        print exec_shell(['rm', '-r', './.scheduled'])
    except:
        print 'No such directory? Creating .scheduled'
    finally:
        print exec_shell(['mkdir', './.scheduled'])

def generate_replica_pod(image, node, port, unique_order):
    temp_pod_file = open('./templates/pod.json', 'r')
    template = Template(temp_pod_file.read())

    app_name = image.split('/')[-1]
    pod_name = node + '-' + app_name + '-' + str(unique_order)

    contents = template.substitute(pod_name=pod_name,
                        pod_app_label=app_name,
                        container_name=pod_name,
                        image=image,
                        port=str(port),
                        node_name=node,
                        )

    pod_file = open('./.scheduled/' + pod_name  + '.conf', 'w')
    pod_file.write(contents)
    pod_file.close()
    return

def generate_application_service(image, port):
    temp_service_file = open('./templates/service.json', 'r')
    template = Template(temp_service_file.read())

    app_name = image.split('/')[-1]
    service_name = app_name + '-http'

    contents = template.substitute(service_name=service_name,
                                   pod_app_label=app_name,
                                   port=port)

    service_file = open('./.scheduled/' + service_name + '.conf', 'w')
    service_file.write(contents)
    service_file.close()
    return

if __name__ == "__main__":
    app.run(host='0.0.0.0', port=13337)
