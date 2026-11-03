#!/usr/bin/env bash
# Run on the server by every deployment, before the new version starts; changes nothing
# once the server is ready. Brings in what later versions need without anyone having to
# log in: SSH to the server is not reachable from every network.
set -euo pipefail
cd ~/exparter

# Adds a setting to .env unless it already has a value. Secrets are made here, on the
# server, so that they never pass through GitHub or a workstation.
ensure_setting() {
  if ! grep -q "^$1=." .env; then
    sed -i "/^$1=/d" .env
    echo "$1=$2" >> .env
    echo "Added $1 to .env"
  fi
}

# Video calls (ADR 0015). The signalling host sits next to the API host, under the same
# domain; the public address is the one clients send media to.
api_host=$(sed -n 's/^API_HOST=//p' .env)
if [[ "$api_host" != api.* ]]; then
  echo "API_HOST in .env does not start with api.; set RTC_HOST by hand." >&2
  exit 1
fi
ensure_setting RTC_HOST "rtc.${api_host#api.}"
ensure_setting LIVEKIT_API_KEY exparter
ensure_setting LIVEKIT_API_SECRET "$(openssl rand -hex 32)"
ensure_setting LIVEKIT_NODE_IP "$(curl -fsS https://checkip.amazonaws.com)"

# Oracle's Ubuntu image closes every port but SSH in its own firewall, in addition to the
# security list of the network (docs/deployment.md, step 2).
changed=false
for rule in tcp:7881 udp:7882; do
  protocol=${rule%:*}
  port=${rule#*:}
  if ! sudo -n iptables -C INPUT -m state --state NEW -p "$protocol" --dport "$port" -j ACCEPT 2>/dev/null; then
    sudo -n iptables -I INPUT 6 -m state --state NEW -p "$protocol" --dport "$port" -j ACCEPT
    echo "Opened $protocol $port"
    changed=true
  fi
done

if [ "$changed" = true ]; then
  sudo -n netfilter-persistent save
fi
