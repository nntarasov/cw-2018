from flask import Flask
from flask import request
from flask import Response

import os
import socket
import json
import subprocess
import time
import urllib2
import fcntl
from threading import Lock

from string import Template

app = Flask(__name__)
lock = Lock()

@app.route("/")
def default():
    with lock:
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
    print '++++++++++++++++++++++++++++++++++ /schedule REQUEST RECEIVED'
    with lock:
        response = schedule_internal()
        print response
        return response

def schedule_internal():
    if not request.is_json:
        msg = json.dumps({'error': 'expected JSON'})
        return Response(msg, status=400, mimetype='application/json')

    schedule_file_cleanup()

    args = request.get_json()
    apps = args['applications'];
    unique_order = 0
    pods = []

    for app in apps:

        image = str(app['image'])
        port = str(app['port'])

        for repl in app['replicas']:
            node = str(repl['node'])
            unique_order += 1
            print 'Generating config: image: {image}, node: {node}, ord: {ord}'\
                .format(image=image, node=node, ord=unique_order)
            pod_name = generate_replica_pod(image, node, port, unique_order)
            pods.append(pod_name)
        print 'Generating config for service. image: {image}, port: {port}'\
                .format(image=image, port=port)
        generate_application_service(image, port)

    # Stops all running entities by label 'auto_created'
    stop_running()

    # Wait while all pods will be stopped
    while True:
        out = exec_shell(['kubectl', 'get', 'pods,services'])
        print out
        time.sleep(5)
        if out.find('Terminating') == -1:
            break

    # Starts new
    try:
        print exec_shell(['./run-scheduled.sh'])
    except:
        msg = json.dumps({'error': 'cannot run pods/services'})
        return Response(msg, status=400, mimetype='application/json')

    # Wait for start
    while True:
        try:
            text = exec_shell(['./get-ready-pods.sh'])
        except:
            text = ''
        print text
        ready = True
        # app apps now in play
        for pod in pods:
            #app_name = str(app['image']).split('/')[-1]
            pod_ready = text.find(pod) != -1
            ready = ready and pod_ready

            if pod_ready:
                print 'Pod {name} is ready'.format(name=pod)
            else:
                print 'Waiting for pod {name}.'.format(name=pod)

        if ready:
            print 'All pods now in play'
            break
        time.sleep(5)    

    # Generate runtime config
    hosts = []

    for app in apps:
        app_name = str(app['image']).split('/')[-1]
        print 'get-actual-port.sh ' + app_name

        app['actual_port'] = int(exec_shell(['./get-actual-port.sh', app_name]))
        print 'Actual port ' + app_name + ' -> ' + str(app['actual_port'])

        print './get-actual-ips.sh ' + app_name
        actual_ips_text = exec_shell(['./get-actual-ips.sh', app_name])
        print actual_ips_text

        actual_ips = actual_ips_text.strip('\n').split('\n')
        print 'actual_ips: ' + json.dumps(actual_ips)

        app['actual_ips'] = actual_ips
        for actual_ip in actual_ips:
            hosts.append({'ip': actual_ip, 'port': app['actual_port']})

    runtime_file = open('../.runtime-config.json', 'w')
    runtime_config_text = json.dumps(apps)
    runtime_file.write(runtime_config_text)
    runtime_file.close()

    # Pass runtime config to applications
    for host in hosts:
        url = 'http://{ip}:{port}/configure'.format(ip=host['ip'], port=host['port'])
        print 'curl ' + url
        req = urllib2.Request(url, data=runtime_config_text)
        req.add_header('Content-Type', 'application/json')
        print urllib2.urlopen(req).read()

    msg = json.dumps({'status': 'ok'})
    return Response(msg, status=200, mimetype='application/json')


# Stops all running entities by label 'auto_created'
def stop_running():
    print 'Trying to remove existing pods/services by label'
    while True:
        try:
            print exec_shell(['kubectl', 'delete', 'pods,services', '-l', 'auto_created=true'])
            return;
        except:
            print 'Processing...'

# uses non-blocking execute with timeout
def exec_shell(cmd):
    print 'shell: executing ' + ' '.join(cmd)
    response = ''
    trycnt = 0
    # run the shell as a subprocess:
    p = subprocess.Popen(cmd,
            stdout = subprocess.PIPE, 
            stderr = subprocess.PIPE, 
            shell = False)
    # set the O_NONBLOCK flag of p.stdout file descriptor:
    flags = fcntl.fcntl(p.stdout, fcntl.F_GETFL) # get current p.stdout flags
    fcntl.fcntl(p.stdout, fcntl.F_SETFL, flags | os.O_NONBLOCK)
    # get the output
    while True:
        time.sleep(1 + 5 * max(0, trycnt - 1))
        buf = os.read(p.stdout.fileno(), 1024)
        exit_code = p.poll()
        print 'shell: exit code: ' + str(exit_code)
        response += buf
        if exit_code != None:
            if exit_code != 0:
                print 'Bad exit code ' + str(exit_code)
                p.kill()
                return exec_shell(cmd)
            return response
        if trycnt > 2:
            if len(response) == 0 :
                p.kill()
                return exec_shell(cmd)
            #break
            continue
        elif len(response) > 0 and len(buf) == 0:
            continue
        trycnt += 1
    p.kill()
    print response


def schedule_file_cleanup():
    try:
        print 'Trying to clean .scheduled'
        print exec_shell(['rm', '-r', './.scheduled'])
    except:
        print 'No such directory? Creating .scheduled'
    finally:
        print exec_shell(['mkdir', './.scheduled'])

# returns pod name
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
    return pod_name

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
