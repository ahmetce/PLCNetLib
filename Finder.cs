﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Net;
using System.Threading;
using ENDAPLCNetLib.Diagnostics;

namespace ENDAPLCNetLib
{
    /// <summary>
    /// <p>This helper class helps you find IP addresses of the PLC devices on your network
    /// by their serial numbers (MAC) in a non-blocking manner.
    /// </p>
    /// <p>
    /// 
    /// </p>
    /// </summary>
    /// <example>
    /// <code>
    /// Finder f = new Finder();
    /// f.Scan();
    /// IPAddress ip = f.GetIp("00:25:fc:00:01:02");
    /// </code>
    /// </example>
    public class Finder
    {
        Logger log = new Logger("Finder");
        List<UdpClient> m_udpClients = new List<UdpClient>();
        IPEndPoint m_remoteEP = new IPEndPoint(IPAddress.Broadcast, 3802);
        Dictionary<string, IPAddress> m_dict = new Dictionary<string, IPAddress>();

        /// <summary>
        /// Handler that will be invoked when a new device is found.
        /// </summary>
        /// <param name="mac">MAC address of the device just found</param>
        /// <param name="ip">IP address of the device</param>
        public delegate void DeviceFoundHandler(string mac, IPAddress ip);

        /// <summary>
        /// This even will be fired when a new device is found.
        /// </summary>
        public event DeviceFoundHandler DeviceFound;

        /// <exception cref="SocketException">Thrown when scanner cannot bind to local port 3802. Can happen when another process is using that local port.</exception>
        public Finder()
        {
            NetworkInterface[] ifs = NetworkInterface.GetAllNetworkInterfaces();
            foreach (NetworkInterface i in ifs)
            {
                if (i.OperationalStatus != OperationalStatus.Up) continue;
                if (!i.Supports(NetworkInterfaceComponent.IPv4)) continue;
                IPInterfaceProperties prop = i.GetIPProperties();

                foreach (UnicastIPAddressInformation ip in prop.UnicastAddresses)
                {

                    if (ip.Address.GetAddressBytes().Length != 4) continue;
                    UdpClient client = new UdpClient(new IPEndPoint(ip.Address, 3802));

                    client.BeginReceive(new AsyncCallback(ReceiveCallback), client);
                    client.EnableBroadcast = true;
                         
                    m_udpClients.Add(client);
                }
            }
        }
        
        private void ReceiveCallback(IAsyncResult ar)
        {
            UdpClient client = (UdpClient)ar.AsyncState;
            byte[] data = client.EndReceive(ar, ref m_remoteEP);

            if (data.Length != 6)
            {
                client.BeginReceive(new AsyncCallback(ReceiveCallback), client);
                return;
            }

            String str = String.Empty;
            for (int i = 0; i < data.Length; i++)
                str += Convert.ToString(data[i], 16).PadLeft(2, '0') + ":";
            str = str.Substring(0, str.Length-1);
            if (!m_dict.ContainsKey(str) || !m_dict[str].Equals(m_remoteEP.Address))
            {
                m_dict[str] = m_remoteEP.Address;
                DeviceFound(str, m_dict[str]);
            }
            client.BeginReceive(new AsyncCallback(ReceiveCallback), client);
        }

        /// <summary>
        /// <p>
        /// Starts a scan. Results will be gathered as PLC devices start sending their informations. This method is not blocking.
        /// </p>
        /// You can wait for <paramref name="DeviceFound"/> event to be fired to be notified about any new devices found.
        /// <p>
        /// </p>
        /// </summary>
        public void Scan()
        {
            m_remoteEP.Address = IPAddress.Broadcast;
            foreach (UdpClient client in m_udpClients)
                client.Send(Encoding.ASCII.GetBytes("PING"), 4, m_remoteEP);
        }

        IPAddress GetIP(string mac)
        {
            return m_dict[mac];
        }
    }
}
