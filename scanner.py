import sys
import logging
import pymongo
from os import environ
from mcstatus import JavaServer
from socket import timeout

logging.basicConfig()
logging.root.setLevel(logging.INFO)
logger = logging.getLogger(__name__)
logger.setLevel(logging.INFO)
dbClient = pymongo.MongoClient(f"mongodb://{environ["MONGODB_USER"]}:{environ["MONGODB_PASSWORD"]}@{environ["MONGODB_IP"]}/")
database = dbClient['minecraft-scanner']
servers = database['servers']


def todict(obj, classkey=None):
    if isinstance(obj, dict):
        data = {}
        for (k, v) in obj.items():
            data[k] = todict(v, classkey)
        return data
    elif hasattr(obj, "_ast"):
        return todict(obj._ast())
    elif hasattr(obj, "__iter__") and not isinstance(obj, str):
        return [todict(v, classkey) for v in obj]
    elif hasattr(obj, "__dict__"):
        data = dict([(key, todict(value, classkey))
            for key, value in obj.__dict__.items()
            if not callable(value) and not key.startswith('_')])
        if classkey is not None and hasattr(obj, "__class__"):
            data[classkey] = obj.__class__.__name__
        return data
    else:
        return obj


def scan(ip, tryCount=3):
    try:
        server = JavaServer.lookup(f"{ip}:25565", timeout=1)
        status = server.status()

        serverData = {
            "ip": ip,
            "server": todict(server),
            "status": todict(status),
        }

        serverData["users"] = [user for user in status.raw['players']['sample']]

        entry = servers.find_one({"ip": ip})
        if not entry:
            servers.insert_one(serverData)

        servers.update_one({"ip": ip}, {"$set": serverData})
        return True
    except timeout:
        if (tryCount <= 0):
            return scan(ip, tryCount - 1)
        logger.error(f"Scanning Timeout {ip}")
        return False
    except Exception as e:
        logger.error(f"Scanning unexpected error: {e}")
        return False


if __name__ == "__main__":
    logger.info("start scanning")
    while True:
        ip = sys.stdin.readline().rstrip()
        logger.info(f"start scanning: {ip}")

        if not ip:
            print("scanning ended (no ips to scan)")
            exit(0)

        logger.info(f"scanning of {ip}: {scan(ip)}")
