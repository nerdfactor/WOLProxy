# WOLProxy

Simple service that handles Wake-on-Lan packets between different networks.

## Purpose of WOLProxy

In some more complex network setups, it is not possible to send **Wake-on-Lan (WOL)** packets from one network to
another. This service is designed to be run on a server that has access to both networks and can forward the packets
between them.

#### Why WOL Broadcast Packets Might Not Pass Between Networks

Wake-on-LAN packets are typically sent as **broadcast** messages, which are designed to reach all devices within the
local network (subnet). However, broadcast packets generally do not travel across different network segments, VLANs, or
routed networks due to the following reasons:

1. **Broadcast Domain Limitations**:
   Broadcast packets, like WOL messages, are confined to the local network (or subnet) because routers typically do not
   forward broadcast traffic. Routers isolate broadcast domains to prevent unnecessary traffic from flooding the entire
   network, which means broadcast packets won't cross network boundaries unless specifically configured.

2. **VLAN Segmentation**:
   In environments using **Virtual LANs (VLANs)**, each VLAN operates as an isolated broadcast domain. Broadcast packets
   sent in one VLAN will not automatically reach devices in another VLAN. This isolation is common for security and
   efficiency, but it prevents WOL packets from traveling across VLANs.

3. **Subnets and Routing**:
   If the target machine resides in a different **subnet**, WOL packets will not be routed by default because routers do
   not forward Layer 2 (data link layer) broadcast packets. Thus, sending a WoL packet from one subnet to another
   without a relay is not possible without specific configurations.

4. **Firewall Restrictions**:
   **Firewalls** between different network segments often block broadcast traffic for security reasons. Even if devices
   are on routable networks, firewalls may prevent WOL broadcasts from passing through to avoid potential misuse or
   security vulnerabilities.

5. **Multicast/Broadcast Filtering**:
   Some managed network devices, such as **switches**, may have broadcast or multicast packet filtering enabled,
   preventing WOL packets from reaching their destination. This is particularly common in large networks where broadcast
   traffic is limited to improve performance.

## Architecture

This service solves these limitations by acting as a proxy for WOL packets. It listens for incoming WOL packets on one
network interface and forwards them to other network interfaces that connect to different subnets, VLANs, or networks.
This enables administrators to wake up devices across network boundaries without modifying the underlying network
infrastructure (such as routers or firewalls) to forward broadcast traffic.

<kbd><img src="https://github.com/user-attachments/assets/db5edb88-70ea-4d17-8c79-66eaff0e10ec" /></kbd>

## Security Considerations

While WOLProxy provides a useful service for forwarding Wake-on-LAN (WOL) packets between network segments, it is
important to consider the security implications of relaying broadcast packets across network boundaries. Therefor the
service already implements some basic security features like:

- **Access Control**:
  WOLProxy can be configured to only accept WOL packets from specific IP addresses, preventing unauthorized
  devices from sending wake-up commands.
- **Interface Whitelisting**:
  Limit the network interfaces that WOLProxy listens on to trusted interfaces only. This reduces exposure to untrusted
  networks and unintended broadcast messages into specific networks.
- **Packet Validation**:
  WOLProxy validates incoming WOL packets to ensure they meet specific criteria before forwarding them. This can help
  prevent malformed or malicious packets from being relayed.
- **Rate Limiting**:
  WOLProxy implements rate limiting to prevent flooding attacks or excessive wake-up requests from being processed.

In addition to these built-in security features, administrators should also consider implementing additional security
measures on the server running WOLProxy.
