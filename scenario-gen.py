#!/usr/bin/python

import json
import random


gen_count = 200
workers = 4

scenarios = [
    {
        # Main page
        'probability': 0.7,
        'actions': [
            {
                'appId': 1,
                'url': 'feed'
            },
            {
                'appId': 1,
                'url': 'viewpost'
            }
        ]
    },
    {
        # Just view some post
        'probability': 0.05,
        'actions': [
            {
                'appId': 1,
                'url': 'viewpost'
            }
        ]
    },
    {
        # Zalogin
        'probability': 0.15,
        'actions': [
            {
                'appId': 1,
                'url': 'feed'
            },
            {
                'appId': 1,
                'url': 'viewacc'
            }
        ]
    },
    {    
        # Comment
        'probability': 0.05,
        'actions': [
            {
                'appId': 1,
                'url': 'viewpost'
            },
            {
                'appId': 1,
                'url': 'viewacc'
            },
            {
                'appId': 1,
                'url': 'createcomment'
            }
        ]
    },
    {    
        # Mimo prohodil
        'probability': 0.05,
        'actions': [
            {
                'appId': 1,
                'url': 'viewpost'
            }
        ]
    }
]

distribution_value = 0.0 # F
for sc in scenarios:
    distribution_value += sc['probability']
    sc['distribution'] = distribution_value


result = {'requestWorkersCount': workers, 'scenarios': []}

for i in range(0, gen_count):
    prob = random.uniform(0, 1)

    scenario = {}
    for sc in scenarios:
        if sc['distribution'] >= prob:
            scenario = sc
            break
    result['scenarios'].append({'actions': scenario['actions']})

print json.dumps(result)
