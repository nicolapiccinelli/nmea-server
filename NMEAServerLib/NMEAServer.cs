﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace NMEAServerLib
{
    public class NMEAServer
    {
        public delegate void ServerStarted();
        public event ServerStarted OnServerStarted;

        public delegate void ServerStop();
        public event ServerStop OnServerStop;

        public delegate void NMEASent(string nmea);
        public event NMEASent OnNMEASent;

        public delegate void ClientConnected(string address);
        public event ClientConnected OnClientConnected;

        public delegate void ServerError(Exception exception);
        public event ServerError OnServerError;

        private int port;
        private Nullable<int> rateMs;
        private TcpListener server;
        private List<TcpClient> clients = new List<TcpClient>();
        private InstrumentsData instrumentsData;
        private CancellationTokenSource cancellationToken;

        private bool _started;

        public NMEAServer(ref InstrumentsData instrumentsData, int port, Nullable<int> sendRateMs = null)
        {
            try
            {

                this.instrumentsData = instrumentsData;
                this.port = port;
                this.rateMs = sendRateMs;

                #pragma warning disable 612, 618
                server = new TcpListener(port);
                OnServerStarted += NMEAServer_OnServerStarted;
                OnServerStop += NMEAServer_OnServerStop;
                OnServerError += NMEAServer_OnServerError;
                OnClientConnected += NMEAServer_OnClientConnected;
                OnNMEASent += NMEAServer_OnNMEASent;
            } catch (Exception e)
            {
                OnServerError(e);
            }
        }

        private void NMEAServer_OnServerStarted()
        {
            Debug.WriteLine("Server started");
        }

        private void NMEAServer_OnServerStop()
        {
            Debug.WriteLine("Server stopped");
        }

        private void NMEAServer_OnServerError(Exception exception)
        {
            Stop();
        }

        private void NMEAServer_OnClientConnected(string address)
        {
            Debug.WriteLine($"Client connected from {address}");
        }

        private void NMEAServer_OnNMEASent(string nmea)
        {
            Debug.WriteLine($"NMEA sent:\r\n{nmea}");
        }

        public bool Started
        {
            get
            {
                return _started;
            }
        }

        public void Start()
        {
            try
            {
                cancellationToken = new CancellationTokenSource();
                server.Start();
                _started = true;
                Thread connsThread = new Thread(ConnectionsLoop);
                connsThread.IsBackground = true;
                connsThread.Start();
                if (rateMs != null)
                {
                    Thread dataThread = new Thread(SendDataLoop);
                    dataThread.IsBackground = true;
                    dataThread.Start();
                }
                if(OnServerStarted != null) OnServerStarted();
            }
            catch (Exception e)
            {
                OnServerError(e);
            }
        }

        public void Stop()
        {
            try
            {
                if (_started)
                {
                    cancellationToken.Cancel();
                    server.Stop();
                    _started = false;
                    if (OnServerStop != null)
                    {
                        OnServerStop();
                    }
                }
            }
            catch (Exception e)
            {
                OnServerError(e);
            }
        }

        private void ConnectionsLoop()
        {
            try
            {
                while (_started && !cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        TcpClient client = server.AcceptTcpClient();
                        clients.Add(client);
                        OnClientConnected(((IPEndPoint)client.Client.RemoteEndPoint).Address.ToString());
                    }
                    catch (SocketException e)
                    {
                        if (e.SocketErrorCode == SocketError.Interrupted)
                        {
                            break;
                        }
                        else
                        {
                            throw e;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                OnServerError(e);
            }
        }

        private void SendDataLoop()
        {
            try
            {
                while (_started)
                {
                    SendData();

                    Thread.Sleep(rateMs ?? 10 * 60 * 60 * 1000);
                }
            }
            catch (Exception e)
            {
                OnServerError(e);
            }
        }

        public void SendData()
        {
            try
            {
                if (_started)
                {
                    for (int i = 0; i < clients.Count; i++)
                    {
                        TcpClient client = clients[i];

                        if (!client.Connected)
                        {
                            clients.Remove(client);
                            i--;
                        }
                        else
                        {
                            try
                            {
                                NetworkStream stream = client.GetStream();
                                string nmea = instrumentsData.generateNMEA();
                                if (OnNMEASent != null) OnNMEASent(nmea);
                                Byte[] data = Encoding.ASCII.GetBytes(instrumentsData.generateNMEA());
                                stream.Write(data, 0, data.Length);
                            }
                            catch (System.IO.IOException e)
                            {
                                // ignore
                            }
                        }
                    }
                }
                else
                {
                    throw new Exception("Server is not started");
                }
            }
            catch (Exception e)
            {
                OnServerError(e);
            }
        }
    }
}
